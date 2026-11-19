"""Reflexión del día: diario → recuerdos con `origen_diario` verificado (ADS §6, CP-206, CP-211).

Trabaja exclusivamente en memoria y garantiza que, cuando el pulso ofrece `episodios_diario`,
nunca se emita una reflexión sin `origen_diario` válido y existente en el pulso.
"""

from __future__ import annotations

import copy
import json
from dataclasses import dataclass, field
from typing import Any

from mente.ensamblador import ensamblar_prompt
from mente.modelos import ModeloMente, estrechar_esquema, validar_respuesta_estrechada


@dataclass(frozen=True)
class ResultadoReflexion:
    """Resultado de generar y validar la reflexión del día."""

    valida: bool
    respuesta: dict[str, Any] | None
    reflexiones: list[dict[str, Any]] = field(default_factory=list)
    intentos: int = 0
    errores: list[str] = field(default_factory=list)
    descartadas: list[dict[str, Any]] = field(default_factory=list)


def construir_esquema_reflexion(pulso: dict[str, Any]) -> dict[str, Any]:
    """Estrecha el esquema de respuesta para la reflexión del día.

    Si el pulso ofrece `episodios_diario`, exige que `memoria` utilice `reflexiones`
    citando exclusivamente ids existentes en `episodios_diario` (y descarta `recordar`).
    """
    esquema = estrechar_esquema(pulso)
    if pulso.get("episodios_diario") is not None:
        memoria_schema = esquema.get("properties", {}).get("memoria")
        if isinstance(memoria_schema, dict):
            props = memoria_schema.get("properties")
            if isinstance(props, dict):
                props.pop("recordar", None)
            memoria_schema.pop("oneOf", None)
            memoria_schema["required"] = ["reflexiones"]
    return esquema


def _normalizar_salida_reflexion(bruto: dict[str, Any] | str, pulso: dict[str, Any]) -> tuple[dict[str, Any] | None, str | None]:
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
            ordenada[clave] = copy.deepcopy(valor)
    return ordenada, None


def validar_reflexiones(pulso: dict[str, Any], respuesta: dict[str, Any]) -> list[dict[str, str]]:
    """Valida que las reflexiones citen exclusivamente episodios existentes en `pulso["episodios_diario"]`."""
    motivos: list[dict[str, str]] = []

    if respuesta.get("epoca") != pulso.get("epoca"):
        motivos.append(
            {
                "codigo": "epoca_obsoleta",
                "campo": "epoca",
                "prosa": "ese recuerdo pertenece a otra época de la partida",
            }
        )
    if respuesta.get("basada_en") != pulso.get("id"):
        motivos.append(
            {
                "codigo": "pulso_distinto",
                "campo": "basada_en",
                "prosa": "esos episodios pertenecen a otro momento",
            }
        )

    memoria = respuesta.get("memoria")
    if memoria is None:
        return motivos
    if not isinstance(memoria, dict):
        motivos.append(
            {
                "codigo": "memoria_invalida",
                "campo": "memoria",
                "prosa": "el bloque de memoria debe ser un objeto JSON",
            }
        )
        return motivos

    episodios = pulso.get("episodios_diario")
    if episodios is not None:
        if not isinstance(episodios, dict):
            episodios = {}

        if memoria.get("recordar"):
            motivos.append(
                {
                    "codigo": "origen_ausente",
                    "campo": "memoria.recordar",
                    "prosa": "recuerda qué episodio te dejó esa impresión",
                }
            )

        reflexiones = memoria.get("reflexiones")
        if reflexiones is not None:
            if not isinstance(reflexiones, list):
                motivos.append(
                    {
                        "codigo": "reflexiones_invalidas",
                        "campo": "memoria.reflexiones",
                        "prosa": "las reflexiones deben ser una lista",
                    }
                )
                return motivos

            for i, ref_item in enumerate(reflexiones):
                if not isinstance(ref_item, dict):
                    motivos.append(
                        {
                            "codigo": "reflexion_invalida",
                            "campo": f"memoria.reflexiones[{i}]",
                            "prosa": "cada reflexión debe ser un objeto con texto y origen_diario",
                        }
                    )
                    continue
                texto = ref_item.get("texto")
                if not isinstance(texto, str) or not texto.strip():
                    motivos.append(
                        {
                            "codigo": "texto_vacio",
                            "campo": f"memoria.reflexiones[{i}].texto",
                            "prosa": "la reflexión debe describir qué recuerdas",
                        }
                    )
                origen = ref_item.get("origen_diario")
                if not isinstance(origen, list) or len(origen) == 0:
                    motivos.append(
                        {
                            "codigo": "origen_ausente",
                            "campo": f"memoria.reflexiones[{i}].origen_diario",
                            "prosa": "recuerda qué episodio te dejó esa impresión",
                        }
                    )
                    continue
                vistos: set[str] = set()
                for j, ep_id in enumerate(origen):
                    if not isinstance(ep_id, str) or ep_id not in episodios:
                        motivos.append(
                            {
                                "codigo": "episodio_desconocido",
                                "campo": f"memoria.reflexiones[{i}].origen_diario[{j}]",
                                "prosa": "ese episodio no figura entre los que recuerdas ahora",
                            }
                        )
                    elif ep_id in vistos:
                        motivos.append(
                            {
                                "codigo": "origen_duplicado",
                                "campo": f"memoria.reflexiones[{i}].origen_diario[{j}]",
                                "prosa": "no repitas el mismo episodio en origen_diario",
                            }
                        )
                    else:
                        vistos.add(ep_id)
    else:
        reflexiones = memoria.get("reflexiones")
        if isinstance(reflexiones, list) and len(reflexiones) > 0:
            motivos.append(
                {
                    "codigo": "episodio_desconocido",
                    "campo": "memoria.reflexiones[0].origen_diario[0]",
                    "prosa": "ese episodio no figura entre los que recuerdas ahora",
                }
            )

    return motivos


def filtrar_reflexiones(
    pulso: dict[str, Any],
    respuesta: dict[str, Any],
) -> tuple[dict[str, Any], list[dict[str, Any]]]:
    """Filtra o depura citas ajenas de `memoria.reflexiones` para no emitir jamás recuerdos sin origen válido."""
    limpia = copy.deepcopy(respuesta)
    descartadas: list[dict[str, Any]] = []

    memoria = limpia.get("memoria")
    if not isinstance(memoria, dict):
        return limpia, descartadas

    episodios = pulso.get("episodios_diario")
    if episodios is not None:
        episodios_validos = episodios if isinstance(episodios, dict) else {}

        if "recordar" in memoria:
            recordar_desc = memoria.pop("recordar")
            if recordar_desc:
                descartadas.append({"motivo": "origen_ausente", "recordar": recordar_desc})

        reflexiones = memoria.get("reflexiones")
        validas: list[dict[str, Any]] = []
        if isinstance(reflexiones, list):
            for item in reflexiones:
                if not isinstance(item, dict):
                    descartadas.append({"motivo": "formato_invalido", "item": item})
                    continue
                texto = item.get("texto")
                if not isinstance(texto, str) or not texto.strip():
                    descartadas.append({"motivo": "texto_vacio", "item": item})
                    continue
                origen_raw = item.get("origen_diario")
                if not isinstance(origen_raw, list):
                    descartadas.append({"motivo": "origen_ausente", "item": item})
                    continue

                origen_valido: list[str] = []
                origen_ajeno: list[Any] = []
                vistos: set[str] = set()
                for ep_id in origen_raw:
                    if isinstance(ep_id, str) and ep_id in episodios_validos:
                        if ep_id not in vistos:
                            vistos.add(ep_id)
                            origen_valido.append(ep_id)
                    else:
                        origen_ajeno.append(ep_id)

                if origen_ajeno:
                    descartadas.append(
                        {
                            "motivo": "episodio_desconocido",
                            "texto": texto,
                            "ids_descartados": origen_ajeno,
                        }
                    )

                if origen_valido:
                    validas.append({"texto": texto, "origen_diario": origen_valido})
                elif not origen_ajeno:
                    descartadas.append({"motivo": "origen_ausente", "item": item})

        if validas:
            memoria["reflexiones"] = validas
        else:
            limpia.pop("memoria", None)
    else:
        if "reflexiones" in memoria:
            ref_desc = memoria.pop("reflexiones")
            if ref_desc:
                descartadas.append({"motivo": "sin_episodios_diario", "reflexiones": ref_desc})
            if not memoria.get("recordar"):
                limpia.pop("memoria", None)

    return limpia, descartadas


def generar_reflexion(
    pulso: dict[str, Any],
    modelo: ModeloMente,
    voz: str,
    *,
    recuerdos: list[Any] | None = None,
    max_reintentos: int = 1,
    filtrar_citas_ajenas: bool = True,
    motivos_rechazo: list[Any] | None = None,
) -> ResultadoReflexion:
    """Genera la reflexión del día usando `ModeloMente` y el esquema estrechado del pulso.

    Reintenta ante citas inválidas y, si `filtrar_citas_ajenas` está activo, depura o descarta
    cualquier reflexión sin `origen_diario` válido en `pulso["episodios_diario"]`.
    """
    esquema = construir_esquema_reflexion(pulso)
    motivos_actuales = list(motivos_rechazo or [])
    reintentos = max(0, max_reintentos)

    ultima_candidata: dict[str, Any] | None = None
    ultimos_errores: list[str] = []

    for intento in range(reintentos + 1):
        prompt = ensamblar_prompt(
            voz,
            pulso,
            recuerdos=recuerdos,
            motivos_rechazo=motivos_actuales or None,
        )
        bruto = modelo.generar(prompt, esquema)
        candidata, error_parseo = _normalizar_salida_reflexion(bruto, pulso)

        if error_parseo is not None or candidata is None:
            errores = [error_parseo or "Salida inválida del modelo."]
            motivos_nuevos = [
                {
                    "codigo": "esquema_invalido",
                    "campo": "respuesta",
                    "prosa": err,
                }
                for err in errores
            ]
        else:
            ultima_candidata = candidata
            errores_esquema = validar_respuesta_estrechada(pulso, candidata, esquema)
            motivos_origen = validar_reflexiones(pulso, candidata)
            errores = [
                *errores_esquema,
                *[f"{m['campo']}: {m['prosa']} ({m['codigo']})" for m in motivos_origen],
            ]
            motivos_nuevos = [
                *[
                    {
                        "codigo": "esquema_invalido",
                        "campo": "respuesta",
                        "prosa": f"La respuesta no cumple el esquema del pulso: {err}",
                    }
                    for err in errores_esquema
                ],
                *motivos_origen,
            ]

        ultimos_errores = errores
        if not errores and candidata is not None:
            reflexiones_val = list((candidata.get("memoria") or {}).get("reflexiones") or [])
            return ResultadoReflexion(
                valida=True,
                respuesta=candidata,
                reflexiones=reflexiones_val,
                intentos=intento + 1,
                errores=[],
                descartadas=[],
            )

        if intento < reintentos:
            motivos_actuales = [*list(motivos_rechazo or []), *motivos_nuevos]

    if filtrar_citas_ajenas and ultima_candidata is not None:
        limpia, descartadas = filtrar_reflexiones(pulso, ultima_candidata)
        errores_post = validar_respuesta_estrechada(pulso, limpia, estrechar_esquema(pulso))
        motivos_post = validar_reflexiones(pulso, limpia)
        if not errores_post and not motivos_post:
            reflexiones_limpias = list((limpia.get("memoria") or {}).get("reflexiones") or [])
            return ResultadoReflexion(
                valida=True,
                respuesta=limpia,
                reflexiones=reflexiones_limpias,
                intentos=reintentos + 1,
                errores=[],
                descartadas=descartadas,
            )
        ultimos_errores = [
            *ultimos_errores,
            *errores_post,
            *[f"{m['campo']}: {m['prosa']}" for m in motivos_post],
        ]

    return ResultadoReflexion(
        valida=False,
        respuesta=None,
        reflexiones=[],
        intentos=reintentos + 1,
        errores=ultimos_errores,
        descartadas=[],
    )


reflexionar_dia = generar_reflexion
