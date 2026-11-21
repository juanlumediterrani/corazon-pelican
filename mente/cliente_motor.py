"""Cliente HTTP del motor, persistencia de cursor descartable y bucle de la mente (ADS §5, §6, P-1)."""

from __future__ import annotations

import json
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import httpx

from mente.costes import (
    PresupuestoTokens,
    RegistroCostes,
    UsoTokens,
    calcular_coste_usd,
    estimar_uso_prompt,
    hash_prefijo_sistema,
    obtener_tarifa,
)
from mente.ensamblador import PromptEnsamblado, ensamblar_prompt
from mente.modelos import (
    ModeloMente,
    PresupuestoTokensAgotadoError,
    estrechar_esquema,
    validar_respuesta_estrechada,
)
from mente.recuperacion import recuperar_recuerdos


class ConflictoEpocaError(RuntimeError):
    """El motor respondió 409 porque la época consultada ya no es la vigente (P-1)."""

    def __init__(
        self,
        epoca_actual: str | None,
        *,
        mundo_cargado: bool = True,
        error: str = "epoca_obsoleta",
    ) -> None:
        self.epoca_actual = epoca_actual
        self.mundo_cargado = mundo_cargado
        self.error = error
        super().__init__(f"{error} (epoca_actual={epoca_actual!r}, mundo_cargado={mundo_cargado})")


@dataclass(frozen=True)
class CursorMente:
    """Cursor descartable calificado por época de mundo (P-1)."""

    epoca: str | None = None
    since: int = 0


class AlmacenCursor:
    """Persistencia descartable del cursor (`epoca`, `since`) entre reinicios de la mente."""

    def __init__(self, ruta: str | Path | None = None) -> None:
        self.ruta = Path(ruta) if ruta is not None else None
        self._memoria = CursorMente()

    def cargar(self) -> CursorMente:
        if self.ruta is None:
            return self._memoria
        if not self.ruta.exists():
            return CursorMente()
        try:
            datos = json.loads(self.ruta.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return CursorMente()
        if not isinstance(datos, dict):
            return CursorMente()
        epoca = datos.get("epoca")
        since = datos.get("since", 0)
        return CursorMente(
            epoca=epoca if isinstance(epoca, str) and epoca else None,
            since=int(since) if isinstance(since, int) and since >= 0 else 0,
        )

    def guardar(self, cursor: CursorMente) -> None:
        self._memoria = cursor
        if self.ruta is None:
            return
        self.ruta.parent.mkdir(parents=True, exist_ok=True)
        contenido = json.dumps({"epoca": cursor.epoca, "since": cursor.since}, ensure_ascii=False)
        tmp = self.ruta.with_suffix(self.ruta.suffix + ".tmp")
        tmp.write_text(contenido, encoding="utf-8")
        tmp.replace(self.ruta)

    def reiniciar(self, nueva_epoca: str | None = None) -> CursorMente:
        cursor = CursorMente(epoca=nueva_epoca, since=0)
        self.guardar(cursor)
        return cursor


class RegistroTelemetria:
    """Registro JSONL descartable de pensamientos y rechazos; nunca es memoria del personaje."""

    def __init__(self, ruta: str | Path | None = None) -> None:
        self.ruta = Path(ruta) if ruta is not None else None
        self.eventos: list[dict[str, Any]] = []

    def registrar(self, evento: dict[str, Any]) -> None:
        entrada = {
            "timestamp_utc": datetime.now(timezone.utc).isoformat(),
            **evento,
        }
        self.eventos.append(entrada)
        if self.ruta is not None:
            self.ruta.parent.mkdir(parents=True, exist_ok=True)
            with self.ruta.open("a", encoding="utf-8") as f:
                f.write(json.dumps(entrada, ensure_ascii=False) + "\n")


class ClienteMotor:
    """Cliente HTTP (`httpx`) contra `http://127.0.0.1:{puerto}/api/v1/` (ADS §5)."""

    def __init__(
        self,
        puerto: int = 25560,
        *,
        base_url: str | None = None,
        token: str | None = None,
        transport: httpx.BaseTransport | None = None,
        client: httpx.Client | None = None,
        timeout: float = 35.0,
    ) -> None:
        url = (base_url or f"http://127.0.0.1:{puerto}/api/v1").rstrip("/")
        headers: dict[str, str] = {}
        if token:
            headers["Authorization"] = f"Bearer {token}"

        self._propio = client is None
        self._client = client or httpx.Client(
            base_url=url,
            headers=headers,
            transport=transport,
            timeout=timeout,
        )

    def __enter__(self) -> ClienteMotor:
        return self

    def __exit__(self, *_: Any) -> None:
        self.close()

    def close(self) -> None:
        if self._propio:
            self._client.close()

    def salud(self) -> dict[str, Any]:
        resp = self._client.get("/salud")
        resp.raise_for_status()
        return resp.json()

    def obtener_pulsos(
        self,
        epoca: str | None = None,
        since: int = 0,
        espera_ms: int = 25000,
    ) -> dict[str, Any]:
        params: dict[str, Any] = {"since": since, "espera_ms": espera_ms}
        if epoca is not None:
            params["epoca"] = epoca
        timeout = max(5.0, espera_ms / 1000.0 + 5.0)
        resp = self._client.get("/pulsos", params=params, timeout=timeout)
        if resp.status_code == 409:
            cuerpo = resp.json()
            raise ConflictoEpocaError(
                cuerpo.get("epoca"),
                mundo_cargado=bool(cuerpo.get("mundo_cargado", False)),
                error=str(cuerpo.get("error") or "epoca_obsoleta"),
            )
        resp.raise_for_status()
        return resp.json()

    def enviar_respuesta(self, respuesta: dict[str, Any]) -> dict[str, Any]:
        resp = self._client.post("/respuestas", json=respuesta)
        resp.raise_for_status()
        return resp.json()

    def obtener_memoria(self, personaje: str) -> dict[str, Any]:
        resp = self._client.get(f"/memoria/{personaje}")
        resp.raise_for_status()
        return resp.json()


@dataclass(frozen=True)
class ResultadoPulso:
    """Resultado de procesar un pulso en la mente."""

    pulso_id: int
    epoca: str
    estado: str
    respuesta: dict[str, Any] | None = None
    intentos_modelo: int = 0
    motivos_rechazo: list[dict[str, Any]] = field(default_factory=list)
    errores_locales: list[str] = field(default_factory=list)


def _ordenar_prioridad_pulsos(pulsos: list[dict[str, Any]]) -> list[dict[str, Any]]:
    """Interlocución primero; después en orden creciente de id (ADS §6)."""
    return sorted(
        pulsos,
        key=lambda p: (0 if p.get("motivo") == "interlocucion" else 1, int(p.get("id", 0))),
    )


def _normalizar_respuesta(bruto: dict[str, Any] | str, pulso: dict[str, Any]) -> tuple[dict[str, Any] | None, str | None]:
    if isinstance(bruto, str):
        try:
            datos = json.loads(bruto)
        except json.JSONDecodeError as exc:
            return None, f"JSON inválido del modelo: {exc.msg}"
    else:
        datos = bruto

    if not isinstance(datos, dict):
        return None, f"La salida del modelo debe ser un objeto JSON, no {type(datos).__name__}."

    ordenada: dict[str, Any] = {}
    if "pensamiento" in datos:
        ordenada["pensamiento"] = datos["pensamiento"]
    ordenada["epoca"] = pulso["epoca"]
    ordenada["basada_en"] = pulso["id"]
    for clave, valor in datos.items():
        if clave not in {"pensamiento", "epoca", "basada_en"}:
            ordenada[clave] = valor
    return ordenada, None


def _extraer_pensamiento(bruto: Any) -> str | None:
    if isinstance(bruto, dict) and isinstance(bruto.get("pensamiento"), str):
        return bruto["pensamiento"]
    if isinstance(bruto, str):
        try:
            datos = json.loads(bruto)
        except json.JSONDecodeError:
            return None
        if isinstance(datos, dict) and isinstance(datos.get("pensamiento"), str):
            return datos["pensamiento"]
    return None


def _es_pulso_cadena(pulso: dict[str, Any], es_cadena: bool | None = None) -> bool:
    if es_cadena is not None:
        return bool(es_cadena)
    hito = pulso.get("hito")
    if isinstance(hito, dict) and hito.get("forma") == "cadena":
        return True
    return bool(pulso.get("es_cadena", False))


def _tiene_contenido_memoria(memoria: dict[str, Any] | None) -> bool:
    if not isinstance(memoria, dict):
        return False
    for clave in ("diario", "recuerdos", "relaciones", "compromisos", "portafolio", "humor"):
        if memoria.get(clave):
            return True
    return False


class CicloMente:
    """Bucle de la mente sin cuerpo propio (ADS §6): consulta pulsos, piensa, valida y responde."""

    def __init__(
        self,
        cliente: ClienteMotor,
        modelo: ModeloMente,
        voz: str,
        *,
        almacen_cursor: AlmacenCursor | None = None,
        telemetria: RegistroTelemetria | None = None,
        presupuesto_tokens: PresupuestoTokens | None = None,
        registro_costes: RegistroCostes | None = None,
        max_reintentos_locales: int = 1,
        max_reformulaciones: int = 1,
        presupuesto_tokens_recuerdos: int = 400,
        max_recuerdos_contexto: int | None = None,
        recuperar_memoria_automatica: bool = True,
    ) -> None:
        self.cliente = cliente
        self.modelo = modelo
        self.voz = voz
        self.almacen_cursor = almacen_cursor or AlmacenCursor()
        self.telemetria = telemetria or RegistroTelemetria()
        self.presupuesto_tokens = presupuesto_tokens
        self.registro_costes = registro_costes
        self.max_reintentos_locales = max(0, max_reintentos_locales)
        self.max_reformulaciones = max(0, max_reformulaciones)
        self.presupuesto_tokens_recuerdos = max(0, int(presupuesto_tokens_recuerdos))
        self.max_recuerdos_contexto = max_recuerdos_contexto
        self.recuperar_memoria_automatica = bool(recuperar_memoria_automatica)
        self.cursor = self.almacen_cursor.cargar()
        self.cache_memoria: dict[str, dict[str, Any]] = {}
        self._prefijos_cache_costes: set[str] = set()

    def _reiniciar_por_cambio_epoca(self, nueva_epoca: str | None) -> None:
        self.cache_memoria.clear()
        self.cursor = self.almacen_cursor.reiniciar(nueva_epoca)

    def sincronizar_epoca(self) -> bool:
        """Comprueba `GET salud`; si la época vigente cambió, reinicia cursor a 0 y descarta caché."""
        salud = self.cliente.salud()
        if not salud.get("mundo_cargado"):
            if self.cursor.epoca is not None or self.cursor.since != 0 or self.cache_memoria:
                self._reiniciar_por_cambio_epoca(None)
                return True
            return False

        epoca_vigente = salud.get("epoca")
        if epoca_vigente != self.cursor.epoca:
            self._reiniciar_por_cambio_epoca(epoca_vigente)
            return True
        return False

    def cargar_memoria(self, personaje: str) -> dict[str, Any]:
        """Consulta la instantánea de memoria en el motor para la caché de trabajo descartable."""
        memoria = self.cliente.obtener_memoria(personaje)
        epoca_mem = memoria.get("epoca")
        if epoca_mem and self.cursor.epoca and epoca_mem != self.cursor.epoca:
            self._reiniciar_por_cambio_epoca(epoca_mem)
        self.cache_memoria[personaje] = memoria
        return memoria

    def _obtener_memoria_vigente(self, personaje: str, epoca_pulso: str) -> dict[str, Any] | None:
        if not personaje or not self.recuperar_memoria_automatica:
            return None

        en_cache = self.cache_memoria.get(personaje)
        if en_cache is not None:
            epoca_cache = en_cache.get("epoca")
            if not epoca_pulso or not epoca_cache or epoca_cache == epoca_pulso:
                return en_cache

        try:
            memoria = self.cliente.obtener_memoria(personaje)
        except httpx.HTTPStatusError as exc:
            if exc.response.status_code == 404:
                return None
            raise

        epoca_mem = memoria.get("epoca")
        if epoca_mem and self.cursor.epoca and epoca_mem != self.cursor.epoca:
            self._reiniciar_por_cambio_epoca(epoca_mem)

        if _tiene_contenido_memoria(memoria):
            self.cache_memoria[personaje] = memoria
        return memoria

    def _resolver_recuerdos_para_pulso(
        self,
        pulso: dict[str, Any],
        recuerdos_explicitos: list[Any] | None,
    ) -> list[Any] | None:
        if recuerdos_explicitos is not None:
            return recuerdos_explicitos

        personaje = str(pulso.get("personaje") or "")
        epoca = str(pulso.get("epoca") or "")
        memoria = self._obtener_memoria_vigente(personaje, epoca)
        if memoria is not None and _tiene_contenido_memoria(memoria) and self.presupuesto_tokens_recuerdos > 0:
            seleccionados = recuperar_recuerdos(
                memoria,
                pulso,
                presupuesto_tokens=self.presupuesto_tokens_recuerdos,
                max_recuerdos=self.max_recuerdos_contexto,
            )
            if seleccionados:
                return seleccionados

        return pulso.get("recuerdos")

    def _puede_llamar_modelo(self, motivo: str, es_cadena: bool) -> bool:
        if self.presupuesto_tokens is None:
            return True
        return self.presupuesto_tokens.puede_gastar(motivo, es_cadena=es_cadena)

    def _registrar_salto_presupuesto(
        self,
        *,
        pulso: dict[str, Any],
        es_cadena: bool,
        reformulacion: bool = False,
    ) -> None:
        self.telemetria.registrar(
            {
                "tipo": "pulso_omitido_presupuesto",
                "pulso_id": pulso.get("id"),
                "epoca": pulso.get("epoca"),
                "motivo_pulso": pulso.get("motivo"),
                "es_cadena": es_cadena,
                "reformulacion": reformulacion,
            }
        )

    def _registrar_coste_llamada(
        self,
        *,
        pulso: dict[str, Any],
        prompt: PromptEnsamblado,
        respuesta: dict[str, Any] | str,
        es_cadena: bool,
    ) -> None:
        if self.presupuesto_tokens is None and self.registro_costes is None:
            return

        modelo_nombre = getattr(self.modelo, "modelo", None) or getattr(
            getattr(self.modelo, "config", None), "modelo", None
        )
        ultimo_uso = getattr(self.modelo, "ultimo_uso", None)
        if isinstance(ultimo_uso, UsoTokens):
            uso = ultimo_uso
            if self.presupuesto_tokens is not None:
                self.presupuesto_tokens.registrar_prefijo_en_cache(prompt)
            if prompt.sistema:
                self._prefijos_cache_costes.add(hash_prefijo_sistema(prompt.sistema))
        elif self.presupuesto_tokens is not None:
            uso = self.presupuesto_tokens.estimar_uso_para_prompt(prompt, respuesta)
        else:
            huella = hash_prefijo_sistema(prompt.sistema) if prompt.sistema else ""
            cache_activa = bool(huella and huella in self._prefijos_cache_costes)
            uso = estimar_uso_prompt(prompt, respuesta, cache_activa=cache_activa)
            if huella:
                self._prefijos_cache_costes.add(huella)

        epoca = str(pulso.get("epoca") or "")
        pulso_id = int(pulso["id"])
        motivo = str(pulso.get("motivo") or "")

        if self.presupuesto_tokens is not None:
            entrada = self.presupuesto_tokens.registrar_llamada(
                epoca=epoca,
                pulso_id=pulso_id,
                motivo=motivo,
                uso=uso,
                modelo=str(modelo_nombre) if modelo_nombre else None,
                es_cadena=es_cadena,
            )
            if self.registro_costes is not None and self.registro_costes is not self.presupuesto_tokens.registro:
                self.registro_costes.registrar_llamada(
                    epoca=entrada["epoca"],
                    pulso_id=entrada["pulso_id"],
                    motivo=entrada["motivo"],
                    modelo=entrada["modelo"],
                    uso=uso,
                    coste_usd=entrada["coste_usd"],
                    es_cadena=entrada["es_cadena"],
                    en_cache=entrada["en_cache"],
                )
        elif self.registro_costes is not None:
            nombre_efectivo = str(modelo_nombre or "desconocido")
            coste = calcular_coste_usd(uso, obtener_tarifa(nombre_efectivo))
            self.registro_costes.registrar_llamada(
                epoca=epoca,
                pulso_id=pulso_id,
                motivo=motivo,
                modelo=nombre_efectivo,
                uso=uso,
                coste_usd=coste,
                es_cadena=es_cadena,
            )

    def _generar_y_validar(
        self,
        pulso: dict[str, Any],
        esquema: dict[str, Any],
        *,
        recuerdos: list[Any] | None,
        motivos_rechazo: list[Any] | None,
        es_reformulacion: bool,
        es_cadena: bool,
    ) -> tuple[dict[str, Any] | None, list[str], int, bool]:
        intentos = 0
        motivos_actuales = list(motivos_rechazo or [])
        motivo = str(pulso.get("motivo") or "")

        for intento in range(self.max_reintentos_locales + 1):
            if intento > 0 and not self._puede_llamar_modelo(motivo, es_cadena):
                self._registrar_salto_presupuesto(pulso=pulso, es_cadena=es_cadena, reformulacion=es_reformulacion)
                return None, ["Presupuesto diario de tokens agotado antes del reintento."], intentos, True

            prompt = ensamblar_prompt(
                self.voz,
                pulso,
                recuerdos=recuerdos,
                motivos_rechazo=motivos_actuales or None,
            )
            try:
                bruto = self.modelo.generar(prompt, esquema)
            except PresupuestoTokensAgotadoError as exc:
                self._registrar_salto_presupuesto(pulso=pulso, es_cadena=es_cadena, reformulacion=es_reformulacion)
                return None, [str(exc)], intentos, True

            intentos += 1
            self._registrar_coste_llamada(
                pulso=pulso,
                prompt=prompt,
                respuesta=bruto,
                es_cadena=es_cadena,
            )

            candidata, error_parseo = _normalizar_respuesta(bruto, pulso)
            if error_parseo is not None or candidata is None:
                errores = [error_parseo or "Salida inválida del modelo."]
            else:
                errores = validar_respuesta_estrechada(pulso, candidata, esquema)

            if not errores and candidata is not None:
                return candidata, [], intentos, False

            pensamiento = _extraer_pensamiento(bruto)
            if intento < self.max_reintentos_locales:
                self.telemetria.registrar(
                    {
                        "tipo": "reintento_local",
                        "pulso_id": pulso.get("id"),
                        "epoca": pulso.get("epoca"),
                        "motivo_pulso": pulso.get("motivo"),
                        "reformulacion": es_reformulacion,
                        "pensamiento": pensamiento,
                        "errores": errores,
                    }
                )
                motivos_actuales = [
                    *list(motivos_rechazo or []),
                    *[
                        {
                            "codigo": "esquema_invalido",
                            "campo": "respuesta",
                            "prosa": f"La respuesta no cumple el esquema del pulso: {err}",
                        }
                        for err in errores
                    ],
                ]
            else:
                self.telemetria.registrar(
                    {
                        "tipo": "rechazo_local",
                        "pulso_id": pulso.get("id"),
                        "epoca": pulso.get("epoca"),
                        "motivo_pulso": pulso.get("motivo"),
                        "reformulacion": es_reformulacion,
                        "pensamiento": pensamiento,
                        "errores": errores,
                    }
                )
                return None, errores, intentos, False

        return None, ["Error inesperado de validación."], intentos, False

    def procesar_pulso(
        self,
        pulso: dict[str, Any],
        *,
        recuerdos: list[Any] | None = None,
        es_cadena: bool | None = None,
    ) -> ResultadoPulso:
        """Procesa un pulso individual: estrecha esquema, valida localmente, envía y reformula una vez si hay rechazo."""
        pulso_id = int(pulso["id"])
        epoca = str(pulso["epoca"])
        motivo = str(pulso.get("motivo") or "")
        cadena = _es_pulso_cadena(pulso, es_cadena)

        if not self._puede_llamar_modelo(motivo, cadena):
            self._registrar_salto_presupuesto(pulso=pulso, es_cadena=cadena, reformulacion=False)
            return ResultadoPulso(
                pulso_id=pulso_id,
                epoca=epoca,
                estado="presupuesto_agotado",
                respuesta=None,
                intentos_modelo=0,
            )

        esquema = estrechar_esquema(pulso)
        recuerdos_efectivos = self._resolver_recuerdos_para_pulso(pulso, recuerdos)

        respuesta, errores, intentos, agotado = self._generar_y_validar(
            pulso,
            esquema,
            recuerdos=recuerdos_efectivos,
            motivos_rechazo=None,
            es_reformulacion=False,
            es_cadena=cadena,
        )
        if respuesta is None:
            return ResultadoPulso(
                pulso_id=pulso_id,
                epoca=epoca,
                estado="presupuesto_agotado" if agotado else "rechazada_local",
                respuesta=None,
                intentos_modelo=intentos,
                errores_locales=errores,
            )

        resultado_motor = self.cliente.enviar_respuesta(respuesta)
        estado_motor = resultado_motor.get("estado")
        if estado_motor == "aceptada":
            if respuesta.get("memoria") or respuesta.get("humor"):
                self.cache_memoria.pop(str(pulso.get("personaje") or ""), None)
            self.telemetria.registrar(
                {
                    "tipo": "pensamiento_aceptado",
                    "pulso_id": pulso_id,
                    "epoca": epoca,
                    "motivo_pulso": pulso.get("motivo"),
                    "reformulacion": False,
                    "pensamiento": respuesta["pensamiento"],
                }
            )
            return ResultadoPulso(
                pulso_id=pulso_id,
                epoca=epoca,
                estado="aceptada",
                respuesta=respuesta,
                intentos_modelo=intentos,
            )

        motivos = list(resultado_motor.get("motivos") or [])
        self.telemetria.registrar(
            {
                "tipo": "rechazo_motor",
                "pulso_id": pulso_id,
                "epoca": epoca,
                "motivo_pulso": pulso.get("motivo"),
                "reformulacion": False,
                "pensamiento": respuesta["pensamiento"],
                "motivos": motivos,
            }
        )

        if self.max_reformulaciones <= 0 or not self._puede_llamar_modelo(motivo, cadena):
            if self.max_reformulaciones > 0:
                self._registrar_salto_presupuesto(pulso=pulso, es_cadena=cadena, reformulacion=True)
            return ResultadoPulso(
                pulso_id=pulso_id,
                epoca=epoca,
                estado="rechazada_motor",
                respuesta=respuesta,
                intentos_modelo=intentos,
                motivos_rechazo=motivos,
            )

        respuesta_ref, errores_ref, intentos_ref, agotado_ref = self._generar_y_validar(
            pulso,
            esquema,
            recuerdos=recuerdos_efectivos,
            motivos_rechazo=motivos,
            es_reformulacion=True,
            es_cadena=cadena,
        )
        total_intentos = intentos + intentos_ref
        if respuesta_ref is None:
            return ResultadoPulso(
                pulso_id=pulso_id,
                epoca=epoca,
                estado="presupuesto_agotado" if agotado_ref else "rechazada_local",
                respuesta=None,
                intentos_modelo=total_intentos,
                motivos_rechazo=motivos,
                errores_locales=errores_ref,
            )

        resultado_ref = self.cliente.enviar_respuesta(respuesta_ref)
        if resultado_ref.get("estado") == "aceptada":
            if respuesta_ref.get("memoria") or respuesta_ref.get("humor"):
                self.cache_memoria.pop(str(pulso.get("personaje") or ""), None)
            self.telemetria.registrar(
                {
                    "tipo": "pensamiento_aceptado",
                    "pulso_id": pulso_id,
                    "epoca": epoca,
                    "motivo_pulso": pulso.get("motivo"),
                    "reformulacion": True,
                    "pensamiento": respuesta_ref["pensamiento"],
                }
            )
            return ResultadoPulso(
                pulso_id=pulso_id,
                epoca=epoca,
                estado="aceptada",
                respuesta=respuesta_ref,
                intentos_modelo=total_intentos,
            )

        motivos_ref = list(resultado_ref.get("motivos") or [])
        self.telemetria.registrar(
            {
                "tipo": "rechazo_motor",
                "pulso_id": pulso_id,
                "epoca": epoca,
                "motivo_pulso": pulso.get("motivo"),
                "reformulacion": True,
                "pensamiento": respuesta_ref["pensamiento"],
                "motivos": motivos_ref,
            }
        )
        return ResultadoPulso(
            pulso_id=pulso_id,
            epoca=epoca,
            estado="rechazada_motor",
            respuesta=respuesta_ref,
            intentos_modelo=total_intentos,
            motivos_rechazo=motivos_ref,
        )

    def ejecutar_paso(
        self,
        espera_ms: int = 25000,
        *,
        verificar_salud: bool = True,
    ) -> list[ResultadoPulso]:
        """Ejecuta una iteración del bucle: consulta pulsos, procesa (interlocución primero) y guarda el cursor."""
        if verificar_salud or self.cursor.epoca is None:
            self.sincronizar_epoca()
            if self.cursor.epoca is None:
                return []

        try:
            lote = self.cliente.obtener_pulsos(
                epoca=self.cursor.epoca,
                since=self.cursor.since,
                espera_ms=espera_ms,
            )
        except ConflictoEpocaError as exc:
            if exc.mundo_cargado and exc.epoca_actual:
                self._reiniciar_por_cambio_epoca(exc.epoca_actual)
                lote = self.cliente.obtener_pulsos(
                    epoca=self.cursor.epoca,
                    since=0,
                    espera_ms=espera_ms,
                )
            else:
                self._reiniciar_por_cambio_epoca(None)
                return []

        epoca_lote = lote.get("epoca")
        if isinstance(epoca_lote, str) and epoca_lote and epoca_lote != self.cursor.epoca:
            self._reiniciar_por_cambio_epoca(epoca_lote)

        pulsos = list(lote.get("pulsos") or [])
        if not pulsos:
            return []

        resultados = [self.procesar_pulso(pulso) for pulso in _ordenar_prioridad_pulsos(pulsos)]

        nuevo_since = max(self.cursor.since, *(int(p["id"]) for p in pulsos))
        self.cursor = CursorMente(epoca=self.cursor.epoca, since=nuevo_since)
        self.almacen_cursor.guardar(self.cursor)
        return resultados
