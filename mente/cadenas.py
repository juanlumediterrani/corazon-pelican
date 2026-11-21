"""Ejecución de cadenas de pensamiento multipaso en hitos (GSS §4.1, ADS §6, CP-209)."""

from __future__ import annotations

import copy
import json
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field
from typing import Any

from mente.ensamblador import PromptEnsamblado, ensamblar_prompt
from mente.modelos import (
    ErrorModeloError,
    ModeloMente,
    PresupuestoTokensAgotadoError,
    estrechar_esquema,
    validar_respuesta_estrechada,
)
from mente.reflexion import (
    construir_esquema_reflexion,
    filtrar_reflexiones,
    validar_reflexiones,
)

ESQUEMA_PASO_INTERMEDIO: dict[str, Any] = {
    "type": "object",
    "properties": {
        "pensamiento": {"type": "string", "minLength": 1},
    },
    "required": ["pensamiento"],
    "additionalProperties": False,
}


@dataclass(frozen=True)
class PasoCadena:
    """Definición de un paso individual dentro de una cadena de pensamiento."""

    nombre: str
    instruccion: str


@dataclass(frozen=True)
class PlantillaCadena:
    """Secuencia ordenada de pasos para resolver un hito con `forma: cadena`."""

    hito_id: str
    pasos: tuple[PasoCadena, ...]
    perfil: str | None = None

    def __post_init__(self) -> None:
        if not self.pasos:
            raise ValueError("Una plantilla de cadena debe tener al menos un paso.")


@dataclass(frozen=True)
class ResultadoCadena:
    """Resultado completo o parcial tras ejecutar los pasos de una cadena."""

    estado: str
    respuesta: dict[str, Any] | None
    pasos_completados: list[dict[str, Any]] = field(default_factory=list)
    intentos_modelo: int = 0
    errores: list[str] = field(default_factory=list)
    parcial: bool = False
    descartadas: list[dict[str, Any]] = field(default_factory=list)


PLANTILLAS_POR_DEFECTO: dict[str, PlantillaCadena] = {
    "despertar": PlantillaCadena(
        hito_id="despertar",
        pasos=(
            PasoCadena(
                "ayer_y_pendiente",
                "Paso 1/3 (Ayer y lo pendiente): Repasa tus recuerdos recientes (y sus episodios de origen), "
                "tu intención activa y cualquier compromiso próximo antes de comprometer acciones.",
            ),
            PasoCadena(
                "amanecer_y_animo",
                "Paso 2/3 (Cómo amanece y cómo te sientes): Observa cómo empieza el día, dónde estás, "
                "qué sientes y con qué ánimo encaras la jornada.",
            ),
            PasoCadena(
                "agenda_del_dia",
                "Paso 3/3 (La agenda del día): Emite la respuesta JSON final fijando `agenda` con las entradas "
                "del horario de hoy (respetando R-16 en cualquier cambio de rutina) e incluyendo explícitamente "
                "cada compromiso pendiente (colocado como `pendiente` o descartado como `saltado`) y tu intención "
                "si deseas continuar o revisar lo iniciado.",
            ),
        ),
    ),
    "acostarse": PlantillaCadena(
        hito_id="acostarse",
        pasos=(
            PasoCadena(
                "resumen_del_dia",
                "Paso 1/4 (Qué pasó hoy): Recorre en orden lo vivido hoy en el diario y en el búfer del pulso.",
            ),
            PasoCadena(
                "significado_recuerdos",
                "Paso 2/4 (Qué significó): Identifica qué merece consolidarse en memoria y con qué id exacto "
                "de `episodios_diario` se corresponde.",
            ),
            PasoCadena(
                "relaciones",
                "Paso 3/4 (Quienes viste): Reflexiona sobre las personas con las que coincidiste o hablaste hoy.",
            ),
            PasoCadena(
                "cierre_para_manana",
                "Paso 4/4 (Qué queda para mañana): Emite la respuesta JSON final. Si propones `memoria.reflexiones` "
                "(con `episodios_diario` presentes), cada recuerdo debe citar al menos un `id` válido en `origen_diario`; "
                "anota además lo que queda abierto para mañana.",
            ),
        ),
    ),
}

PLANTILLA_GENERICA = PlantillaCadena(
    hito_id="*",
    pasos=(
        PasoCadena(
            "situacion",
            "Paso 1/2 (Situación): Pondera lo que ocurre ahora, dónde estás y qué recuerdos se conectan con este hito.",
        ),
        PasoCadena(
            "decision",
            "Paso 2/2 (Decisión): Emite la respuesta JSON final cumpliendo el esquema estrechado del pulso.",
        ),
    ),
)


def _normalizar_plantilla(hito_id: str, valor: PlantillaCadena | Sequence[PasoCadena | tuple[str, str]], perfil: str | None = None) -> PlantillaCadena:
    if isinstance(valor, PlantillaCadena):
        return valor
    pasos = tuple(
        item if isinstance(item, PasoCadena) else PasoCadena(str(item[0]), str(item[1]))
        for item in valor
    )
    return PlantillaCadena(hito_id=hito_id, pasos=pasos, perfil=perfil)


def resolver_plantilla_cadena(
    pulso: dict[str, Any],
    plantillas: Mapping[Any, PlantillaCadena | Sequence[PasoCadena | tuple[str, str]]] | None = None,
) -> PlantillaCadena:
    """Resuelve la plantilla de cadena por `(perfil, hito)` con reserva a `(None, hito)` y genérica."""
    personaje = str(pulso.get("personaje") or "").strip()
    hito = pulso.get("hito")
    hito_id = str(hito.get("id") or "") if isinstance(hito, dict) else ""

    if plantillas is not None:
        if personaje and isinstance(plantillas.get(personaje), Mapping):
            por_perfil = plantillas[personaje]
            if hito_id in por_perfil:
                return _normalizar_plantilla(hito_id, por_perfil[hito_id], perfil=personaje)
        if personaje and (personaje, hito_id) in plantillas:
            return _normalizar_plantilla(hito_id, plantillas[(personaje, hito_id)], perfil=personaje)
        clave_compuesta = f"{personaje}:{hito_id}" if personaje and hito_id else ""
        if clave_compuesta and clave_compuesta in plantillas:
            return _normalizar_plantilla(hito_id, plantillas[clave_compuesta], perfil=personaje)
        if (None, hito_id) in plantillas:
            return _normalizar_plantilla(hito_id, plantillas[(None, hito_id)])
        if hito_id in plantillas:
            return _normalizar_plantilla(hito_id, plantillas[hito_id])

    if hito_id in PLANTILLAS_POR_DEFECTO:
        return PLANTILLAS_POR_DEFECTO[hito_id]
    return PlantillaCadena(hito_id=hito_id or "*", pasos=PLANTILLA_GENERICA.pasos)


def _normalizar_objeto_modelo(bruto: dict[str, Any] | str) -> tuple[dict[str, Any] | None, str | None]:
    if isinstance(bruto, str):
        try:
            datos = json.loads(bruto)
        except json.JSONDecodeError as exc:
            return None, f"JSON inválido del modelo: {exc.msg}"
    else:
        datos = bruto

    if not isinstance(datos, dict):
        return None, f"La salida del modelo debe ser un objeto JSON, no {type(datos).__name__}."
    return copy.deepcopy(datos), None


def _es_acostarse(pulso: dict[str, Any]) -> bool:
    hito = pulso.get("hito")
    return isinstance(hito, dict) and hito.get("id") == "acostarse"


def _ordenar_respuesta_pulso(datos: dict[str, Any], pulso: dict[str, Any]) -> dict[str, Any]:
    ordenada: dict[str, Any] = {}
    if "pensamiento" in datos:
        ordenada["pensamiento"] = datos["pensamiento"]
    ordenada["epoca"] = pulso["epoca"]
    ordenada["basada_en"] = pulso["id"]
    for clave, valor in datos.items():
        if clave not in {"pensamiento", "epoca", "basada_en"}:
            ordenada[clave] = valor
    return ordenada


def _resumir_pensamientos_parciales(pasos: list[dict[str, Any]]) -> str:
    fragmentos = [str(p.get("pensamiento") or "").strip() for p in pasos if str(p.get("pensamiento") or "").strip()]
    if not fragmentos:
        return "Me quedo con lo vivido hoy y sigo adelante con calma."
    return " ".join(fragmentos)


def _esquema_final_cadena(
    pulso: dict[str, Any],
    episodios_adicionales: Sequence[str] | None = None,
) -> dict[str, Any]:
    if _es_acostarse(pulso) or pulso.get("episodios_diario") is not None:
        return construir_esquema_reflexion(pulso)
    try:
        return estrechar_esquema(pulso, episodios_adicionales=episodios_adicionales)
    except TypeError:
        return estrechar_esquema(pulso)


def _validar_paso_intermedio(datos: dict[str, Any]) -> list[str]:
    errores: list[str] = []
    extras = [k for k in datos.keys() if k != "pensamiento"]
    if extras:
        errores.append(f"El paso intermedio solo admite `pensamiento`, sobran: {', '.join(extras)}.")
    pens = datos.get("pensamiento")
    if not isinstance(pens, str) or not pens.strip():
        errores.append("Falta un `pensamiento` en prosa no vacío.")
    return errores


def ejecutar_cadena(
    pulso: dict[str, Any],
    modelo: ModeloMente,
    voz: str,
    *,
    recuerdos: list[Any] | None = None,
    motivos_rechazo: list[Any] | None = None,
    plantillas: Mapping[Any, PlantillaCadena | Sequence[PasoCadena | tuple[str, str]]] | None = None,
    max_reintentos_locales: int = 1,
    episodios_adicionales: Sequence[str] | None = None,
    puede_llamar: Callable[[], bool] | None = None,
    al_llamar_modelo: Callable[[PromptEnsamblado, dict[str, Any] | str], None] | None = None,
    al_registrar_evento: Callable[[dict[str, Any]], None] | None = None,
) -> ResultadoCadena:
    """Ejecuta todos los pasos de una cadena manteniendo la salida previa entre llamadas."""
    plantilla = resolver_plantilla_cadena(pulso, plantillas)
    total_pasos = len(plantilla.pasos)
    esquema_final = _esquema_final_cadena(pulso, episodios_adicionales=episodios_adicionales)
    es_acostarse = _es_acostarse(pulso)

    pasos_completados: list[dict[str, Any]] = []
    intentos_totales = 0
    reintentos = max(0, max_reintentos_locales)

    for indice, paso in enumerate(plantilla.pasos):
        es_ultimo = indice == total_pasos - 1
        esquema_paso = esquema_final if es_ultimo else copy.deepcopy(ESQUEMA_PASO_INTERMEDIO)
        motivos_paso = list(motivos_rechazo or []) if es_ultimo else []
        motivos_actuales = list(motivos_paso)

        paso_resuelto = False
        errores_paso: list[str] = []
        descartadas_final: list[dict[str, Any]] = []
        candidata_final: dict[str, Any] | None = None

        for intento in range(reintentos + 1):
            if puede_llamar is not None and not puede_llamar():
                errores_paso = ["Presupuesto diario de tokens agotado durante la cadena."]
                if al_registrar_evento is not None:
                    al_registrar_evento(
                        {
                            "tipo": "pulso_omitido_presupuesto",
                            "pulso_id": pulso.get("id"),
                            "epoca": pulso.get("epoca"),
                            "motivo_pulso": pulso.get("motivo"),
                            "es_cadena": True,
                            "paso_cadena": paso.nombre,
                        }
                    )
                if not pasos_completados:
                    return ResultadoCadena(
                        estado="presupuesto_agotado",
                        respuesta=None,
                        pasos_completados=[],
                        intentos_modelo=intentos_totales,
                        errores=errores_paso,
                    )
                break

            prompt = ensamblar_prompt(
                voz,
                pulso,
                recuerdos=recuerdos,
                motivos_rechazo=motivos_actuales or None,
                pasos_cadena=pasos_completados or None,
                instruccion_paso=paso.instruccion,
            )

            try:
                bruto = modelo.generar(prompt, esquema_paso)
            except PresupuestoTokensAgotadoError as exc:
                errores_paso = [str(exc)]
                if not pasos_completados:
                    return ResultadoCadena(
                        estado="presupuesto_agotado",
                        respuesta=None,
                        pasos_completados=[],
                        intentos_modelo=intentos_totales,
                        errores=errores_paso,
                    )
                break
            except ErrorModeloError as exc:
                errores_paso = [str(exc)]
                if al_registrar_evento is not None:
                    al_registrar_evento(
                        {
                            "tipo": "error_proveedor",
                            "pulso_id": pulso.get("id"),
                            "epoca": pulso.get("epoca"),
                            "motivo_pulso": pulso.get("motivo"),
                            "es_cadena": True,
                            "paso_cadena": paso.nombre,
                            "error": str(exc),
                        }
                    )
                if not pasos_completados:
                    return ResultadoCadena(
                        estado="error_proveedor",
                        respuesta=None,
                        pasos_completados=[],
                        intentos_modelo=intentos_totales,
                        errores=errores_paso,
                    )
                break
            except Exception as exc:
                errores_paso = [f"Fallo en paso '{paso.nombre}': {exc}"]
                break

            intentos_totales += 1
            if al_llamar_modelo is not None:
                al_llamar_modelo(prompt, bruto)

            datos, error_parseo = _normalizar_objeto_modelo(bruto)
            if error_parseo is not None or datos is None:
                errores_paso = [error_parseo or "Salida inválida del modelo."]
            elif not es_ultimo:
                errores_paso = _validar_paso_intermedio(datos)
                if not errores_paso:
                    pasos_completados.append(
                        {"paso": paso.nombre, "pensamiento": str(datos["pensamiento"]).strip()}
                    )
                    paso_resuelto = True
                    break
            else:
                candidata = _ordenar_respuesta_pulso(datos, pulso)
                errores_esquema = validar_respuesta_estrechada(pulso, candidata, esquema_paso)
                motivos_origen = validar_reflexiones(pulso, candidata) if es_acostarse else []
                errores_paso = [
                    *errores_esquema,
                    *[f"{m['campo']}: {m['prosa']} ({m['codigo']})" for m in motivos_origen],
                ]
                if not errores_paso:
                    pasos_completados.append(
                        {"paso": paso.nombre, "pensamiento": str(candidata.get("pensamiento") or "").strip()}
                    )
                    candidata_final = candidata
                    paso_resuelto = True
                    break

                # Si falló solo por citas ajenas de reflexión y estamos en el último intento, depuramos citas.
                if intento == reintentos and es_acostarse and pulso.get("episodios_diario") is not None:
                    limpia, descartadas = filtrar_reflexiones(pulso, candidata)
                    err_post = validar_respuesta_estrechada(pulso, limpia, esquema_paso)
                    mot_post = validar_reflexiones(pulso, limpia)
                    if not err_post and not mot_post:
                        pasos_completados.append(
                            {"paso": paso.nombre, "pensamiento": str(limpia.get("pensamiento") or "").strip()}
                        )
                        candidata_final = limpia
                        descartadas_final = descartadas
                        paso_resuelto = True
                        break

            if intento < reintentos:
                motivos_actuales = [
                    *motivos_paso,
                    *[
                        {
                            "codigo": "esquema_invalido",
                            "campo": "respuesta",
                            "prosa": f"La respuesta no cumple el paso '{paso.nombre}': {err}",
                        }
                        for err in errores_paso
                    ],
                ]

        if paso_resuelto:
            if es_ultimo and candidata_final is not None:
                return ResultadoCadena(
                    estado="completa",
                    respuesta=candidata_final,
                    pasos_completados=pasos_completados,
                    intentos_modelo=intentos_totales,
                    errores=[],
                    parcial=False,
                    descartadas=descartadas_final,
                )
            continue

        # Fallo de este paso tras agotar reintentos: si ya hay pasos previos, devolver cierre seguro parcial.
        if al_registrar_evento is not None:
            al_registrar_evento(
                {
                    "tipo": "cadena_parcial",
                    "pulso_id": pulso.get("id"),
                    "epoca": pulso.get("epoca"),
                    "motivo_pulso": pulso.get("motivo"),
                    "hito_id": plantilla.hito_id,
                    "paso_fallido": paso.nombre,
                    "pasos_completados": [p["paso"] for p in pasos_completados],
                    "errores": errores_paso,
                }
            )

        if pasos_completados:
            respuesta_segura = {
                "pensamiento": _resumir_pensamientos_parciales(pasos_completados),
                "epoca": pulso["epoca"],
                "basada_en": pulso["id"],
            }
            return ResultadoCadena(
                estado="parcial",
                respuesta=respuesta_segura,
                pasos_completados=pasos_completados,
                intentos_modelo=intentos_totales,
                errores=errores_paso,
                parcial=True,
                descartadas=[],
            )

        return ResultadoCadena(
            estado="rechazada_local",
            respuesta=None,
            pasos_completados=[],
            intentos_modelo=intentos_totales,
            errores=errores_paso,
            parcial=False,
            descartadas=[],
        )

    return ResultadoCadena(
        estado="rechazada_local",
        respuesta=None,
        pasos_completados=pasos_completados,
        intentos_modelo=intentos_totales,
        errores=["La cadena terminó sin paso final."],
        parcial=bool(pasos_completados),
    )
