"""Presupuesto de tokens, caché de prompt, telemetría de costes e informes diarios (ADS §6, §12.2)."""

from __future__ import annotations

import hashlib
import json
from collections.abc import Callable
from dataclasses import dataclass
from datetime import date, datetime, timezone
from pathlib import Path
from typing import Any

from mente.ensamblador import PromptEnsamblado


@dataclass(frozen=True)
class TarifaModelo:
    """Tarifa por millón de tokens (USD / 1M tokens) de un modelo LLM."""

    modelo: str
    entrada_por_1m: float
    cache_entrada_por_1m: float
    salida_por_1m: float
    proveedor: str = ""

    def __post_init__(self) -> None:
        if self.entrada_por_1m < 0 or self.cache_entrada_por_1m < 0 or self.salida_por_1m < 0:
            raise ValueError("Las tarifas de tokens no pueden ser negativas.")


TARIFAS_MODELOS: dict[str, TarifaModelo] = {
    "claude-sonnet-4-5": TarifaModelo(
        modelo="claude-sonnet-4-5",
        entrada_por_1m=3.00,
        cache_entrada_por_1m=0.30,
        salida_por_1m=15.00,
        proveedor="anthropic",
    ),
    "claude-haiku-4-5": TarifaModelo(
        modelo="claude-haiku-4-5",
        entrada_por_1m=1.00,
        cache_entrada_por_1m=0.10,
        salida_por_1m=5.00,
        proveedor="anthropic",
    ),
    "gpt-4o": TarifaModelo(
        modelo="gpt-4o",
        entrada_por_1m=2.50,
        cache_entrada_por_1m=1.25,
        salida_por_1m=10.00,
        proveedor="openai",
    ),
    "gpt-4o-mini": TarifaModelo(
        modelo="gpt-4o-mini",
        entrada_por_1m=0.15,
        cache_entrada_por_1m=0.075,
        salida_por_1m=0.60,
        proveedor="openai",
    ),
    "gemini-2.5-flash": TarifaModelo(
        modelo="gemini-2.5-flash",
        entrada_por_1m=0.30,
        cache_entrada_por_1m=0.075,
        salida_por_1m=2.50,
        proveedor="gemini",
    ),
    "gemini-2.5-pro": TarifaModelo(
        modelo="gemini-2.5-pro",
        entrada_por_1m=1.25,
        cache_entrada_por_1m=0.3125,
        salida_por_1m=10.00,
        proveedor="gemini",
    ),
    "fake-mente": TarifaModelo(
        modelo="fake-mente",
        entrada_por_1m=0.0,
        cache_entrada_por_1m=0.0,
        salida_por_1m=0.0,
        proveedor="fake",
    ),
    "local": TarifaModelo(
        modelo="local",
        entrada_por_1m=0.0,
        cache_entrada_por_1m=0.0,
        salida_por_1m=0.0,
        proveedor="local",
    ),
}

MODELO_ESTIMACION_POR_DEFECTO = "claude-sonnet-4-5"


def obtener_tarifa(
    modelo: str,
    tarifa: TarifaModelo | None = None,
    *,
    estricto: bool = False,
) -> TarifaModelo:
    """Devuelve la tarifa del modelo por nombre exacto o prefijo, o una nula si no es estricto."""
    if tarifa is not None:
        return tarifa
    clave = (modelo or "").strip().lower()
    if clave in TARIFAS_MODELOS:
        return TARIFAS_MODELOS[clave]
    for nombre, t in TARIFAS_MODELOS.items():
        if clave.startswith(nombre):
            return t
    if clave.startswith(("fake", "local")):
        return TarifaModelo(modelo=modelo or "local", entrada_por_1m=0.0, cache_entrada_por_1m=0.0, salida_por_1m=0.0)
    if estricto:
        raise KeyError(f"No hay tarifa registrada para el modelo '{modelo}'.")
    return TarifaModelo(modelo=modelo or "desconocido", entrada_por_1m=0.0, cache_entrada_por_1m=0.0, salida_por_1m=0.0)


@dataclass(frozen=True)
class UsoTokens:
    """Desglose de tokens de una llamada al modelo: entrada sin caché, entrada en caché y salida."""

    tokens_entrada: int = 0
    tokens_cache: int = 0
    tokens_salida: int = 0

    def __post_init__(self) -> None:
        if self.tokens_entrada < 0 or self.tokens_cache < 0 or self.tokens_salida < 0:
            raise ValueError("Los contadores de tokens no pueden ser negativos.")

    @property
    def tokens_entrada_totales(self) -> int:
        return self.tokens_entrada + self.tokens_cache

    @property
    def total_tokens(self) -> int:
        return self.tokens_entrada + self.tokens_cache + self.tokens_salida

    @property
    def en_cache(self) -> bool:
        return self.tokens_cache > 0

    def coste_usd(self, tarifa: TarifaModelo) -> float:
        return calcular_coste_usd(self, tarifa)


def calcular_coste_usd(uso: UsoTokens, tarifa: TarifaModelo) -> float:
    """Calcula el coste estimado en USD para un uso de tokens y una tarifa de modelo."""
    coste = (
        uso.tokens_entrada * tarifa.entrada_por_1m
        + uso.tokens_cache * tarifa.cache_entrada_por_1m
        + uso.tokens_salida * tarifa.salida_por_1m
    ) / 1_000_000.0
    return round(coste, 10)


def estimar_tokens_texto(texto: str) -> int:
    """Estimación determinista (~4 caracteres por token) alineada con `FakeLLM`."""
    if not texto:
        return 0
    return max(1, len(texto) // 4)


def hash_prefijo_sistema(sistema: str) -> str:
    """Huella SHA-256 del prefijo de sistema estable para detectar aciertos de caché."""
    return hashlib.sha256(sistema.encode("utf-8")).hexdigest()


def estimar_uso_prompt(
    prompt: PromptEnsamblado,
    respuesta: dict[str, Any] | str | None = None,
    *,
    cache_activa: bool = False,
) -> UsoTokens:
    """Estima `UsoTokens` a partir de un `PromptEnsamblado` y su respuesta."""
    tok_sistema = estimar_tokens_texto(prompt.sistema)
    tok_usuario = estimar_tokens_texto(prompt.usuario)

    if cache_activa and tok_sistema > 0:
        tokens_entrada = tok_usuario
        tokens_cache = tok_sistema
    else:
        tokens_entrada = tok_sistema + tok_usuario
        tokens_cache = 0

    if respuesta is None:
        tokens_salida = 0
    elif isinstance(respuesta, str):
        tokens_salida = estimar_tokens_texto(respuesta)
    else:
        tokens_salida = estimar_tokens_texto(json.dumps(respuesta, ensure_ascii=False))

    return UsoTokens(
        tokens_entrada=tokens_entrada,
        tokens_cache=tokens_cache,
        tokens_salida=tokens_salida,
    )


class RegistroCostes:
    """Registro JSONL descartable de llamadas al modelo y su coste (`costes.jsonl`, ADS §12.2)."""

    def __init__(self, ruta: str | Path | None = None) -> None:
        self.ruta = Path(ruta) if ruta is not None else None
        self.entradas: list[dict[str, Any]] = []

    def registrar_llamada(
        self,
        *,
        epoca: str,
        pulso_id: int,
        motivo: str,
        modelo: str,
        uso: UsoTokens,
        coste_usd: float,
        es_cadena: bool = False,
        en_cache: bool | None = None,
        timestamp_utc: datetime | None = None,
    ) -> dict[str, Any]:
        ahora = timestamp_utc or datetime.now(timezone.utc)
        entrada: dict[str, Any] = {
            "timestamp_utc": ahora.isoformat(),
            "epoca": str(epoca),
            "pulso_id": int(pulso_id),
            "motivo": str(motivo),
            "es_cadena": bool(es_cadena),
            "modelo": str(modelo),
            "tokens_entrada": int(uso.tokens_entrada),
            "tokens_cache": int(uso.tokens_cache),
            "tokens_salida": int(uso.tokens_salida),
            "coste_usd": float(coste_usd),
            "en_cache": bool(uso.en_cache if en_cache is None else en_cache),
        }
        self.entradas.append(entrada)
        if self.ruta is not None:
            self.ruta.parent.mkdir(parents=True, exist_ok=True)
            with self.ruta.open("a", encoding="utf-8") as f:
                f.write(json.dumps(entrada, ensure_ascii=False) + "\n")
        return entrada


class PresupuestoTokens:
    """Segunda barrera de coste: presupuesto diario de tokens en tiempo real (ADS §6).

    Separa la contabilidad de pulsos ordinarios, llamadas de cadenas (despertar/acostarse)
    e interlocución. Al agotarse el presupuesto ordinario, bloquea pulsos no interlocutorios
    antes de llamar al LLM mientras deja pasar `interlocucion`.
    """

    def __init__(
        self,
        *,
        limite_tokens_dia: int | None = None,
        limite_tokens_cadenas: int | None = None,
        limite_coste_usd_dia: float | None = None,
        modelo: str = "fake-mente",
        tarifa: TarifaModelo | None = None,
        registro: RegistroCostes | None = None,
        ruta_costes: str | Path | None = None,
        reloj_utc: Callable[[], datetime] | None = None,
    ) -> None:
        if limite_tokens_dia is not None and limite_tokens_dia < 0:
            raise ValueError("limite_tokens_dia no puede ser negativo.")
        if limite_tokens_cadenas is not None and limite_tokens_cadenas < 0:
            raise ValueError("limite_tokens_cadenas no puede ser negativo.")
        if limite_coste_usd_dia is not None and limite_coste_usd_dia < 0:
            raise ValueError("limite_coste_usd_dia no puede ser negativo.")

        self.limite_tokens_dia = limite_tokens_dia
        self.limite_tokens_cadenas = limite_tokens_cadenas
        self.limite_coste_usd_dia = limite_coste_usd_dia
        self.modelo = modelo
        self._tarifa_explicita = tarifa
        self.tarifa = obtener_tarifa(modelo, tarifa)
        self.registro = registro if registro is not None else RegistroCostes(ruta_costes)
        self._reloj_utc = reloj_utc or (lambda: datetime.now(timezone.utc))

        self._fecha_dia: date = self._reloj_utc().date()
        self._prefijos_cache: set[str] = set()
        self._reiniciar_contadores()

    def _reiniciar_contadores(self) -> None:
        self.llamadas_ordinarias = 0
        self.llamadas_cadenas = 0
        self.llamadas_interlocucion = 0
        self.llamadas_en_cache = 0
        self.tokens_ordinarios = 0
        self.tokens_cadenas = 0
        self.tokens_interlocucion = 0
        self.tokens_entrada = 0
        self.tokens_cache = 0
        self.tokens_salida = 0
        self.coste_ordinario_usd = 0.0
        self.coste_cadenas_usd = 0.0
        self.coste_interlocucion_usd = 0.0

    def reiniciar_dia(self, fecha: date | None = None) -> None:
        """Reinicia los contadores diarios conservando el histórico escrito en `costes.jsonl`."""
        self._fecha_dia = fecha or self._reloj_utc().date()
        self._reiniciar_contadores()

    def _sincronizar_dia(self) -> None:
        hoy = self._reloj_utc().date()
        if hoy != self._fecha_dia:
            self.reiniciar_dia(hoy)

    @property
    def llamadas_totales(self) -> int:
        return self.llamadas_ordinarias + self.llamadas_cadenas + self.llamadas_interlocucion

    @property
    def tokens_totales(self) -> int:
        return self.tokens_ordinarios + self.tokens_cadenas + self.tokens_interlocucion

    @property
    def coste_total_usd(self) -> float:
        return round(self.coste_ordinario_usd + self.coste_cadenas_usd + self.coste_interlocucion_usd, 10)

    def _resolver_modelo_y_tarifa(self, modelo: str | None) -> tuple[str, TarifaModelo]:
        if self._tarifa_explicita is not None:
            nombre = modelo if (modelo and modelo != "fake-mente") else (self.modelo or self._tarifa_explicita.modelo)
            return nombre, self._tarifa_explicita
        if modelo and (modelo != "fake-mente" or self.modelo == "fake-mente"):
            nombre = modelo
        else:
            nombre = self.modelo
        return nombre, obtener_tarifa(nombre)

    def estimar_uso_para_prompt(
        self,
        prompt: PromptEnsamblado,
        respuesta: dict[str, Any] | str | None = None,
    ) -> UsoTokens:
        """Estima `UsoTokens` detectando aciertos de caché por hash de `prompt.sistema`."""
        huella = hash_prefijo_sistema(prompt.sistema) if prompt.sistema else ""
        cache_activa = bool(huella and huella in self._prefijos_cache)
        uso = estimar_uso_prompt(prompt, respuesta, cache_activa=cache_activa)
        if huella:
            self._prefijos_cache.add(huella)
        return uso

    def registrar_prefijo_en_cache(self, prompt: PromptEnsamblado) -> None:
        if prompt.sistema:
            self._prefijos_cache.add(hash_prefijo_sistema(prompt.sistema))

    def puede_gastar(self, motivo: str, *, es_cadena: bool = False) -> bool:
        """Comprueba si hay saldo para un pulso; `interlocucion` siempre pasa."""
        self._sincronizar_dia()
        if motivo == "interlocucion":
            return True
        if self.limite_coste_usd_dia is not None and self.coste_total_usd >= self.limite_coste_usd_dia:
            return False
        if es_cadena:
            if self.limite_tokens_cadenas is not None:
                return self.tokens_cadenas < self.limite_tokens_cadenas
            if self.limite_tokens_dia is not None:
                return self.tokens_ordinarios < self.limite_tokens_dia
            return True
        if self.limite_tokens_dia is not None:
            return self.tokens_ordinarios < self.limite_tokens_dia
        return True

    def esta_agotado(self, *, es_cadena: bool = False) -> bool:
        """Indica si el presupuesto no interlocutorio está agotado."""
        return not self.puede_gastar("deriva", es_cadena=es_cadena)

    def registrar_llamada(
        self,
        *,
        epoca: str,
        pulso_id: int,
        motivo: str,
        uso: UsoTokens,
        modelo: str | None = None,
        es_cadena: bool = False,
        en_cache: bool | None = None,
    ) -> dict[str, Any]:
        """Contabiliza una llamada real al LLM y la persiste en `costes.jsonl`."""
        self._sincronizar_dia()
        ahora = self._reloj_utc()
        modelo_efectivo, tarifa_efectiva = self._resolver_modelo_y_tarifa(modelo)
        coste = calcular_coste_usd(uso, tarifa_efectiva)

        self.tokens_entrada += uso.tokens_entrada
        self.tokens_cache += uso.tokens_cache
        self.tokens_salida += uso.tokens_salida
        en_cache_efectivo = uso.en_cache if en_cache is None else bool(en_cache)
        if en_cache_efectivo:
            self.llamadas_en_cache += 1

        if motivo == "interlocucion":
            self.llamadas_interlocucion += 1
            self.tokens_interlocucion += uso.total_tokens
            self.coste_interlocucion_usd = round(self.coste_interlocucion_usd + coste, 10)
        elif es_cadena:
            self.llamadas_cadenas += 1
            self.tokens_cadenas += uso.total_tokens
            self.coste_cadenas_usd = round(self.coste_cadenas_usd + coste, 10)
        else:
            self.llamadas_ordinarias += 1
            self.tokens_ordinarios += uso.total_tokens
            self.coste_ordinario_usd = round(self.coste_ordinario_usd + coste, 10)

        return self.registro.registrar_llamada(
            epoca=epoca,
            pulso_id=pulso_id,
            motivo=motivo,
            modelo=modelo_efectivo,
            uso=uso,
            coste_usd=coste,
            es_cadena=es_cadena,
            en_cache=en_cache_efectivo,
            timestamp_utc=ahora,
        )

    def resumen(self) -> dict[str, Any]:
        """Resumen diario de consumo separado en ordinario, cadenas e interlocución."""
        self._sincronizar_dia()
        return {
            "fecha_utc": self._fecha_dia.isoformat(),
            "modelo": self.modelo,
            "llamadas_totales": self.llamadas_totales,
            "llamadas_ordinarias": self.llamadas_ordinarias,
            "llamadas_cadenas": self.llamadas_cadenas,
            "llamadas_interlocucion": self.llamadas_interlocucion,
            "llamadas_en_cache": self.llamadas_en_cache,
            "tokens_ordinarios": self.tokens_ordinarios,
            "tokens_cadenas": self.tokens_cadenas,
            "tokens_interlocucion": self.tokens_interlocucion,
            "tokens_totales": self.tokens_totales,
            "tokens_entrada": self.tokens_entrada,
            "tokens_cache": self.tokens_cache,
            "tokens_salida": self.tokens_salida,
            "coste_ordinario_usd": self.coste_ordinario_usd,
            "coste_cadenas_usd": self.coste_cadenas_usd,
            "coste_interlocucion_usd": self.coste_interlocucion_usd,
            "coste_total_usd": self.coste_total_usd,
            "limite_tokens_dia": self.limite_tokens_dia,
            "limite_tokens_cadenas": self.limite_tokens_cadenas,
            "limite_coste_usd_dia": self.limite_coste_usd_dia,
            "ordinario_agotado": self.esta_agotado(),
        }


@dataclass(frozen=True)
class ParametrosCadenciaDia:
    """Parámetros de cadencia por modo de reloj (docs/notas-juego.md §3.2 y §7)."""

    modo: str
    pulsos_ordinarios_dia: int
    cadenas_dia: int
    llamadas_por_cadena: int
    interlocuciones_dia: int
    duracion_real_min: float


CADENCIA_POR_MODO: dict[str, ParametrosCadenciaDia] = {
    "vanilla": ParametrosCadenciaDia(
        modo="vanilla",
        pulsos_ordinarios_dia=5,
        cadenas_dia=2,
        llamadas_por_cadena=4,
        interlocuciones_dia=2,
        duracion_real_min=14.0,
    ),
    "pelican": ParametrosCadenciaDia(
        modo="pelican",
        pulsos_ordinarios_dia=5,
        cadenas_dia=2,
        llamadas_por_cadena=4,
        interlocuciones_dia=6,
        duracion_real_min=1440.0,
    ),
}


@dataclass(frozen=True)
class EstimacionCosteDiario:
    """Resultado de coste estimado para un día de juego en un modo de reloj concreto."""

    modo: str
    modelo: str
    llamadas_ordinarias: int
    llamadas_cadenas: int
    llamadas_interlocucion: int
    llamadas_totales: int
    tokens_entrada: int
    tokens_cache: int
    tokens_salida: int
    tokens_totales: int
    coste_usd: float
    coste_sin_cache_usd: float
    ahorro_cache_usd: float
    duracion_real_min: float
    coste_por_hora_real_usd: float


def estimar_coste_diario(
    modo: str,
    *,
    modelo: str = MODELO_ESTIMACION_POR_DEFECTO,
    tarifa: TarifaModelo | None = None,
    pulsos_ordinarios_dia: int | None = None,
    cadenas_dia: int | None = None,
    llamadas_por_cadena: int | None = None,
    interlocuciones_dia: int | None = None,
    tokens_sistema: int = 902,
    tokens_usuario_medio: int = 250,
    tokens_salida_medio: int = 200,
    tasa_cache: float = 0.85,
) -> EstimacionCosteDiario:
    """Estima el coste de tokens y USD por día de juego en `vanilla` o `pelican`."""
    clave_modo = modo.strip().lower()
    if clave_modo not in CADENCIA_POR_MODO:
        raise ValueError(f"Modo desconocido '{modo}'; usa 'vanilla' o 'pelican'.")
    if not (0.0 <= tasa_cache <= 1.0):
        raise ValueError("tasa_cache debe estar entre 0.0 y 1.0.")

    base = CADENCIA_POR_MODO[clave_modo]
    tarifa_efectiva = obtener_tarifa(modelo, tarifa, estricto=True)

    n_ord = base.pulsos_ordinarios_dia if pulsos_ordinarios_dia is None else pulsos_ordinarios_dia
    n_cad_pulsos = base.cadenas_dia if cadenas_dia is None else cadenas_dia
    n_llam_por_cad = base.llamadas_por_cadena if llamadas_por_cadena is None else llamadas_por_cadena
    n_cad = n_cad_pulsos * n_llam_por_cad
    n_inter = base.interlocuciones_dia if interlocuciones_dia is None else interlocuciones_dia
    n_total = n_ord + n_cad + n_inter

    llamadas_cache = min(n_total, max(0, round(n_total * tasa_cache)))
    llamadas_sin_cache = n_total - llamadas_cache

    tok_cache = llamadas_cache * tokens_sistema
    tok_entrada = n_total * tokens_usuario_medio + llamadas_sin_cache * tokens_sistema
    tok_salida = n_total * tokens_salida_medio

    uso_con_cache = UsoTokens(tokens_entrada=tok_entrada, tokens_cache=tok_cache, tokens_salida=tok_salida)
    uso_sin_cache = UsoTokens(
        tokens_entrada=n_total * (tokens_sistema + tokens_usuario_medio),
        tokens_cache=0,
        tokens_salida=tok_salida,
    )

    coste_con_cache = calcular_coste_usd(uso_con_cache, tarifa_efectiva)
    coste_sin_cache = calcular_coste_usd(uso_sin_cache, tarifa_efectiva)
    ahorro = round(coste_sin_cache - coste_con_cache, 10)
    por_hora_real = round(coste_con_cache * (60.0 / base.duracion_real_min), 10)

    return EstimacionCosteDiario(
        modo=clave_modo,
        modelo=tarifa_efectiva.modelo,
        llamadas_ordinarias=n_ord,
        llamadas_cadenas=n_cad,
        llamadas_interlocucion=n_inter,
        llamadas_totales=n_total,
        tokens_entrada=tok_entrada,
        tokens_cache=tok_cache,
        tokens_salida=tok_salida,
        tokens_totales=uso_con_cache.total_tokens,
        coste_usd=coste_con_cache,
        coste_sin_cache_usd=coste_sin_cache,
        ahorro_cache_usd=ahorro,
        duracion_real_min=base.duracion_real_min,
        coste_por_hora_real_usd=por_hora_real,
    )


def generar_informe_coste_diario(
    modelo: str = MODELO_ESTIMACION_POR_DEFECTO,
    *,
    tarifa: TarifaModelo | None = None,
    **kwargs: Any,
) -> dict[str, EstimacionCosteDiario]:
    """Genera la comparativa de coste diario estimado para `vanilla` y `pelican`."""
    return {
        "vanilla": estimar_coste_diario("vanilla", modelo=modelo, tarifa=tarifa, **kwargs),
        "pelican": estimar_coste_diario("pelican", modelo=modelo, tarifa=tarifa, **kwargs),
    }


def formatear_informe_coste_diario(informe: dict[str, EstimacionCosteDiario]) -> str:
    """Formatea en Markdown el informe comparativo de coste vanilla vs Pelican."""
    lineas = [
        "| Modo | Modelo | Llamadas/día (ord/cad/inter) | Tokens totales | Con caché (USD/día) | Sin caché (USD/día) | USD / hora real |",
        "|---|---|---|---:|---:|---:|---:|",
    ]
    for clave in ("vanilla", "pelican"):
        est = informe[clave]
        desglose = f"{est.llamadas_totales} ({est.llamadas_ordinarias}/{est.llamadas_cadenas}/{est.llamadas_interlocucion})"
        lineas.append(
            f"| {est.modo} | `{est.modelo}` | {desglose} | {est.tokens_totales:,} | "
            f"${est.coste_usd:.4f} | ${est.coste_sin_cache_usd:.4f} | ${est.coste_por_hora_real_usd:.4f} |"
        )
    return "\n".join(lineas)
