"""Contrato motor ↔ mente: validación contra `schemas/`, la única fuente del contrato (ADS §2)."""

from __future__ import annotations

import json
from functools import lru_cache
from pathlib import Path
from typing import Any

from jsonschema import Draft202012Validator
from referencing import Registry, Resource

DIR_ESQUEMAS = Path(__file__).resolve().parent.parent / "schemas"
ESQUEMAS = ("comun", "pulso", "respuesta", "obra", "perfil", "resultado", "salud")


def _ruta(nombre: str) -> Path:
    return DIR_ESQUEMAS / f"{nombre}.schema.json"


def cargar_esquema(nombre: str) -> dict[str, Any]:
    """Devuelve el esquema `nombre` tal cual está en disco (conserva el orden de propiedades)."""
    with _ruta(nombre).open(encoding="utf-8") as f:
        return json.load(f)


@lru_cache(maxsize=1)
def _registro() -> Registry:
    recursos = []
    for nombre in ESQUEMAS:
        esquema = cargar_esquema(nombre)
        recursos.append((esquema["$id"], Resource.from_contents(esquema)))
    return Registry().with_resources(recursos)


@lru_cache(maxsize=None)
def validador(nombre: str) -> Draft202012Validator:
    return Draft202012Validator(cargar_esquema(nombre), registry=_registro())


def errores(nombre: str, datos: Any) -> list[str]:
    """Errores de validación de `datos` contra el esquema `nombre`, legibles y ordenados."""
    return sorted(
        f"{'/'.join(map(str, e.absolute_path)) or '(raíz)'}: {e.message}"
        for e in validador(nombre).iter_errors(datos)
    )


def es_valido(nombre: str, datos: Any) -> bool:
    return not errores(nombre, datos)
