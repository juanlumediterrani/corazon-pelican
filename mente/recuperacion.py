"""Recuperación de memoria por recencia × importancia × relevancia (ADS §6, GSS §9).

Trabaja exclusivamente en memoria sobre la instantánea recibida de `GET /api/v1/memoria/{personaje}`
(o listas heredadas de recuerdos en prosa) y nunca escribe ficheros en disco.
"""

from __future__ import annotations

import math
import re
import unicodedata
from collections.abc import Callable, Iterable
from typing import Any

_PATRON_PALABRA = re.compile(r"[a-z0-9_]+")
_PATRON_REF = re.compile(r"\b[a-z][a-z_]*:[^\s,;\"'()\[\]]+")

_STOPWORDS = frozenset(
    {
        "el",
        "la",
        "los",
        "las",
        "un",
        "una",
        "unos",
        "unas",
        "de",
        "del",
        "al",
        "en",
        "con",
        "por",
        "para",
        "sin",
        "sobre",
        "entre",
        "hacia",
        "desde",
        "hasta",
        "que",
        "como",
        "pero",
        "mas",
        "cuando",
        "donde",
        "quien",
        "este",
        "esta",
        "estos",
        "estas",
        "ese",
        "esa",
        "esos",
        "esas",
        "aquel",
        "aquella",
        "hoy",
        "ayer",
        "manana",
        "muy",
        "todo",
        "toda",
        "todos",
        "todas",
        "algo",
        "nada",
        "otra",
        "otro",
        "otros",
        "otras",
        "te",
        "me",
        "se",
        "le",
        "lo",
        "nos",
        "su",
        "sus",
        "mi",
        "mis",
        "tu",
        "tus",
        "es",
        "son",
        "era",
        "fue",
        "ha",
        "han",
        "hay",
        "ya",
        "no",
        "si",
    }
)


def estimar_tokens(texto: str) -> int:
    """Estimación determinista y sin dependencias externas (~4 caracteres por token)."""
    if not texto:
        return 0
    return max(1, (len(texto) + 3) // 4)


def _normalizar_texto(texto: str) -> str:
    descompuesto = unicodedata.normalize("NFKD", texto.lower())
    return "".join(ch for ch in descompuesto if not unicodedata.combining(ch))


def _tokenizar(texto: str) -> set[str]:
    if not texto:
        return set()
    norm = _normalizar_texto(texto)
    return {
        tok
        for tok in _PATRON_PALABRA.findall(norm)
        if len(tok) >= 2 and tok not in _STOPWORDS and not tok.isdigit()
    }


def _extraer_refs_texto(texto: str) -> set[str]:
    if not texto:
        return set()
    return {m.group(0).lower() for m in _PATRON_REF.finditer(texto)}


def _minutos_juego(dia: int | None, hora: int | None) -> int | None:
    if dia is None:
        return None
    hora_eff = 600 if hora is None else int(hora)
    return (int(dia) - 1) * 1440 + (hora_eff // 100) * 60 + (hora_eff % 100)


def _recoger_textos_valor(valor: Any, salida: list[str], refs: set[str]) -> None:
    if isinstance(valor, str):
        salida.append(valor)
        refs.update(_extraer_refs_texto(valor))
    elif isinstance(valor, dict):
        for clave, subvalor in valor.items():
            if clave in {"ref", "id", "con", "hito", "obra"} and isinstance(subvalor, str) and subvalor:
                refs.add(subvalor.lower())
                if ":" in subvalor:
                    for parte in subvalor.split(":"):
                        if len(parte) >= 2:
                            salida.append(parte)
            _recoger_textos_valor(subvalor, salida, refs)
    elif isinstance(valor, list):
        for item in valor:
            _recoger_textos_valor(item, salida, refs)


def _extraer_consulta_pulso(pulso: dict[str, Any]) -> tuple[set[str], set[str], int | None]:
    textos: list[str] = []
    refs: set[str] = set()

    for campo in ("escena", "hechos", "lugares", "obras", "huella", "intencion_activa", "interlocutor", "bufer", "compromisos_proximos"):
        if campo in pulso and pulso[campo] is not None:
            _recoger_textos_valor(pulso[campo], textos, refs)

    palabras: set[str] = set()
    for texto in textos:
        palabras.update(_tokenizar(texto))

    tj = pulso.get("tiempo_juego")
    minutos_pulso: int | None = None
    if isinstance(tj, dict) and isinstance(tj.get("dia"), (int, float)):
        hora_val = tj.get("hora")
        minutos_pulso = _minutos_juego(int(tj["dia"]), int(hora_val) if isinstance(hora_val, (int, float)) else None)

    return refs, palabras, minutos_pulso


def _construir_candidato(
    *,
    indice: int,
    fuente: str,
    texto: str,
    item_id: str | None,
    origen_diario: list[str],
    importancia: float,
    dia_juego: int | None,
    hora: int | None,
    entidades: Iterable[str],
) -> dict[str, Any]:
    refs = _extraer_refs_texto(texto)
    textos_entidades: list[str] = []
    for ent in entidades:
        if isinstance(ent, str) and ent:
            refs.add(ent.lower())
            textos_entidades.append(ent.replace(":", " ").replace("_", " "))

    palabras = _tokenizar(texto)
    for t_ent in textos_entidades:
        palabras.update(_tokenizar(t_ent))

    return {
        "indice": indice,
        "fuente": fuente,
        "id": item_id,
        "texto": texto,
        "origen_diario": list(origen_diario),
        "importancia": max(0.0, min(1.0, float(importancia))),
        "dia_juego": dia_juego,
        "hora": hora,
        "refs": refs,
        "palabras": palabras,
    }


def _extraer_candidatos(memoria: dict[str, Any] | list[Any] | None, *, incluir_diario: bool) -> list[dict[str, Any]]:
    if not memoria:
        return []

    candidatos: list[dict[str, Any]] = []
    indice = 0

    if isinstance(memoria, list):
        for item in memoria:
            if isinstance(item, str):
                texto = item.strip()
                if texto:
                    candidatos.append(
                        _construir_candidato(
                            indice=indice,
                            fuente="recuerdo",
                            texto=texto,
                            item_id=None,
                            origen_diario=[],
                            importancia=0.5,
                            dia_juego=None,
                            hora=None,
                            entidades=(),
                        )
                    )
                    indice += 1
            elif isinstance(item, dict):
                texto = str(item.get("texto") or item.get("prosa") or item.get("resumen_prosa") or "").strip()
                if not texto:
                    continue
                origen = [e for e in (item.get("origen_diario") or []) if isinstance(e, str) and e]
                imp_bruta = item.get("importancia", item.get("saliencia", 0.5))
                dia = item.get("dia_juego", item.get("dia"))
                hora = item.get("hora")
                candidatos.append(
                    _construir_candidato(
                        indice=indice,
                        fuente="recuerdo",
                        texto=texto,
                        item_id=item.get("id") if isinstance(item.get("id"), str) else None,
                        origen_diario=origen,
                        importancia=float(imp_bruta) if isinstance(imp_bruta, (int, float)) else 0.5,
                        dia_juego=int(dia) if isinstance(dia, (int, float)) else None,
                        hora=int(hora) if isinstance(hora, (int, float)) else None,
                        entidades=item.get("entidades") or (),
                    )
                )
                indice += 1
        return candidatos

    if not isinstance(memoria, dict):
        return []

    diario = memoria.get("diario") or []
    episodios_por_id: dict[str, dict[str, Any]] = {}
    for ep in diario:
        if isinstance(ep, dict) and isinstance(ep.get("id"), str) and ep["id"]:
            episodios_por_id[ep["id"]] = ep

    episodios_cubiertos: set[str] = set()

    for rec in memoria.get("recuerdos") or []:
        if isinstance(rec, str):
            texto = rec.strip()
            if texto:
                candidatos.append(
                    _construir_candidato(
                        indice=indice,
                        fuente="recuerdo",
                        texto=texto,
                        item_id=None,
                        origen_diario=[],
                        importancia=0.5,
                        dia_juego=None,
                        hora=None,
                        entidades=(),
                    )
                )
                indice += 1
            continue

        if not isinstance(rec, dict):
            continue

        texto = str(rec.get("texto") or rec.get("prosa") or "").strip()
        if not texto:
            continue

        origen = [eid for eid in (rec.get("origen_diario") or []) if isinstance(eid, str) and eid]
        episodios_cubiertos.update(origen)

        entidades: list[str] = [e for e in (rec.get("entidades") or []) if isinstance(e, str)]
        dia_juego = rec.get("dia_juego", rec.get("dia"))
        hora = rec.get("hora")
        mejor_minuto = _minutos_juego(
            int(dia_juego) if isinstance(dia_juego, (int, float)) else None,
            int(hora) if isinstance(hora, (int, float)) else None,
        )
        dia_eff = int(dia_juego) if isinstance(dia_juego, (int, float)) else None
        hora_eff = int(hora) if isinstance(hora, (int, float)) else None
        max_saliencia: float | None = None

        for eid in origen:
            ep = episodios_por_id.get(eid)
            if not ep:
                continue
            entidades.extend(e for e in (ep.get("entidades") or []) if isinstance(e, str))
            ep_sal = ep.get("saliencia")
            if isinstance(ep_sal, (int, float)):
                max_saliencia = float(ep_sal) if max_saliencia is None else max(max_saliencia, float(ep_sal))
            if dia_eff is None and isinstance(ep.get("dia_juego"), (int, float)):
                ep_dia = int(ep["dia_juego"])
                ep_hora = int(ep["hora"]) if isinstance(ep.get("hora"), (int, float)) else 600
                ep_min = _minutos_juego(ep_dia, ep_hora)
                if mejor_minuto is None or (ep_min is not None and ep_min > mejor_minuto):
                    mejor_minuto = ep_min
                    dia_eff = ep_dia
                    hora_eff = ep_hora

        imp_bruta = rec.get("importancia", rec.get("saliencia"))
        importancia = (
            float(imp_bruta)
            if isinstance(imp_bruta, (int, float))
            else (max_saliencia if max_saliencia is not None else 0.5)
        )

        candidatos.append(
            _construir_candidato(
                indice=indice,
                fuente="recuerdo",
                texto=texto,
                item_id=rec.get("id") if isinstance(rec.get("id"), str) else None,
                origen_diario=origen,
                importancia=importancia,
                dia_juego=dia_eff,
                hora=hora_eff,
                entidades=entidades,
            )
        )
        indice += 1

    if incluir_diario:
        for ep in diario:
            if not isinstance(ep, dict):
                continue
            ep_id = ep.get("id") if isinstance(ep.get("id"), str) else None
            if ep_id and ep_id in episodios_cubiertos:
                continue
            texto = str(ep.get("resumen_prosa") or ep.get("texto") or ep.get("prosa") or "").strip()
            if not texto:
                continue
            sal = ep.get("saliencia", 0.5)
            dia = ep.get("dia_juego")
            hora = ep.get("hora")
            candidatos.append(
                _construir_candidato(
                    indice=indice,
                    fuente="diario",
                    texto=texto,
                    item_id=ep_id,
                    origen_diario=[ep_id] if ep_id else [],
                    importancia=float(sal) if isinstance(sal, (int, float)) else 0.5,
                    dia_juego=int(dia) if isinstance(dia, (int, float)) else None,
                    hora=int(hora) if isinstance(hora, (int, float)) else None,
                    entidades=ep.get("entidades") or (),
                )
            )
            indice += 1

    return candidatos


def _calcular_recencia(
    dia_juego: int | None,
    hora: int | None,
    minutos_pulso: int | None,
    *,
    semivida_dias: float,
    recencia_sin_fecha: float,
) -> float:
    if dia_juego is None or minutos_pulso is None:
        return recencia_sin_fecha
    minutos_cand = _minutos_juego(dia_juego, hora)
    if minutos_cand is None:
        return recencia_sin_fecha
    delta_dias = max(0.0, (minutos_pulso - minutos_cand) / 1440.0)
    return max(0.01, min(1.0, 0.5 ** (delta_dias / semivida_dias)))


def _calcular_relevancia(
    refs_cand: set[str],
    palabras_cand: set[str],
    refs_pulso: set[str],
    palabras_pulso: set[str],
) -> float:
    solape_refs = 0.0
    if refs_cand and refs_pulso:
        inter_refs = len(refs_cand & refs_pulso)
        if inter_refs > 0:
            solape_refs = inter_refs / len(refs_cand)

    solape_lexico = 0.0
    if palabras_cand and palabras_pulso:
        inter_pal = len(palabras_cand & palabras_pulso)
        if inter_pal > 0:
            cobertura = inter_pal / len(palabras_cand)
            coseno = inter_pal / math.sqrt(len(palabras_cand) * len(palabras_pulso))
            solape_lexico = 0.5 * cobertura + 0.5 * coseno

    return min(1.0, max(0.0, 0.65 * solape_lexico + 0.75 * solape_refs))


def recuperar_recuerdos(
    memoria: dict[str, Any] | list[Any] | None,
    pulso: dict[str, Any],
    presupuesto_tokens: int = 400,
    *,
    incluir_diario: bool = True,
    semivida_recencia_dias: float = 7.0,
    recencia_sin_fecha: float = 0.5,
    piso_relevancia: float = 0.1,
    max_recuerdos: int | None = None,
    contador_tokens: Callable[[str], int] | None = None,
) -> list[dict[str, Any]]:
    """Selecciona y ordena recuerdos por `recencia × importancia × relevancia` dentro de `presupuesto_tokens`.

    Acepta tanto la instantánea de `GET /api/v1/memoria/{personaje}` (con `recuerdos` y `diario`)
    como listas heredadas de recuerdos en prosa sin `origen_diario`.
    """
    if presupuesto_tokens <= 0:
        return []
    if semivida_recencia_dias <= 0:
        raise ValueError("semivida_recencia_dias debe ser mayor que 0.")
    if not (0.0 <= piso_relevancia <= 1.0):
        raise ValueError("piso_relevancia debe estar entre 0 y 1.")
    if max_recuerdos is not None and max_recuerdos <= 0:
        return []

    candidatos = _extraer_candidatos(memoria, incluir_diario=incluir_diario)
    if not candidatos:
        return []

    refs_pulso, palabras_pulso, minutos_pulso = _extraer_consulta_pulso(pulso)
    medir = contador_tokens or estimar_tokens

    puntuados: list[tuple[tuple[float, float, float, float, int], dict[str, Any]]] = []
    for cand in candidatos:
        recencia = _calcular_recencia(
            cand["dia_juego"],
            cand["hora"],
            minutos_pulso,
            semivida_dias=semivida_recencia_dias,
            recencia_sin_fecha=recencia_sin_fecha,
        )
        importancia = cand["importancia"]
        relevancia_cruda = _calcular_relevancia(cand["refs"], cand["palabras"], refs_pulso, palabras_pulso)
        relevancia_ponderada = piso_relevancia + (1.0 - piso_relevancia) * relevancia_cruda
        puntuacion = recencia * importancia * relevancia_ponderada

        tokens_item = medir(f"- {cand['texto']}")
        entrada: dict[str, Any] = {
            "texto": cand["texto"],
            "fuente": cand["fuente"],
            "puntuacion": round(puntuacion, 6),
            "recencia": round(recencia, 6),
            "importancia": round(importancia, 6),
            "relevancia": round(relevancia_cruda, 6),
            "tokens": tokens_item,
        }
        if cand["id"] is not None:
            entrada["id"] = cand["id"]
        if cand["origen_diario"]:
            entrada["origen_diario"] = cand["origen_diario"]
        if cand["dia_juego"] is not None:
            entrada["dia_juego"] = cand["dia_juego"]

        clave_orden = (-puntuacion, -relevancia_cruda, -recencia, -importancia, cand["indice"])
        puntuados.append((clave_orden, entrada))

    puntuados.sort(key=lambda par: par[0])

    seleccion: list[dict[str, Any]] = []
    tokens_usados = 0
    for _, entrada in puntuados:
        if max_recuerdos is not None and len(seleccion) >= max_recuerdos:
            break
        coste = int(entrada["tokens"])
        if tokens_usados + coste > presupuesto_tokens:
            continue
        seleccion.append(entrada)
        tokens_usados += coste

    return seleccion
