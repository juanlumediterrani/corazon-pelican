"""Estrechamiento de esquema por pulso, interfaz de modelos y FakeLLM (ADS §6, P-11)."""

from __future__ import annotations

import copy
import os
from collections.abc import Callable, Iterable
from dataclasses import dataclass, field
from typing import Any, Protocol, runtime_checkable

from jsonschema import Draft202012Validator

from mente import contrato
from mente.costes import UsoTokens, estimar_uso_prompt, hash_prefijo_sistema
from mente.ensamblador import PromptEnsamblado


def _deduplicar(valores: Iterable[str]) -> list[str]:
    vistos: set[str] = set()
    resultado: list[str] = []
    for valor in valores:
        if valor not in vistos:
            vistos.add(valor)
            resultado.append(valor)
    return resultado


def _reescribir_refs_comun(nodo: Any) -> Any:
    if isinstance(nodo, dict):
        return {
            clave: (
                valor.replace("comun.schema.json#/$defs/", "#/$defs/")
                if clave == "$ref" and isinstance(valor, str)
                else _reescribir_refs_comun(valor)
            )
            for clave, valor in nodo.items()
        }
    if isinstance(nodo, list):
        return [_reescribir_refs_comun(item) for item in nodo]
    return nodo


def _extraer_acciones(pulso: dict[str, Any], acciones_base: list[str]) -> list[str]:
    permitidas = set(acciones_base)
    candidatas: list[str] = []
    for entrada in pulso.get("acciones_disponibles") or []:
        if isinstance(entrada, str) and entrada:
            nombre = entrada.split("(", 1)[0]
            if nombre in permitidas:
                candidatas.append(nombre)
    return _deduplicar(candidatas)


def _extraer_lugares(pulso: dict[str, Any]) -> list[str]:
    return _deduplicar(
        item["id"]
        for item in (pulso.get("lugares") or [])
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    )


def _extraer_entidades(pulso: dict[str, Any]) -> list[str]:
    refs: list[str] = []
    hechos = pulso.get("hechos") or {}
    for ent in hechos.get("cerca") or []:
        if isinstance(ent, dict) and isinstance(ent.get("ref"), str):
            refs.append(ent["ref"])
    huella = pulso.get("huella") or {}
    for ent in huella.get("entidades") or []:
        if isinstance(ent, dict) and isinstance(ent.get("ref"), str):
            refs.append(ent["ref"])
    interlocutor = pulso.get("interlocutor")
    if isinstance(interlocutor, dict) and isinstance(interlocutor.get("ref"), str):
        refs.append(interlocutor["ref"])
    return _deduplicar(refs)


def _extraer_hitos(pulso: dict[str, Any]) -> list[str]:
    return _deduplicar(
        item["hito"]
        for item in (pulso.get("agenda") or [])
        if isinstance(item, dict) and isinstance(item.get("hito"), str)
    )


def _extraer_hitos_agenda(pulso: dict[str, Any]) -> list[str]:
    ids: list[str] = list(_extraer_hitos(pulso))
    for comp in pulso.get("compromisos_proximos") or []:
        if isinstance(comp, dict) and isinstance(comp.get("id"), str) and comp["id"]:
            ids.append(comp["id"])
    intencion_activa = pulso.get("intencion_activa")
    if isinstance(intencion_activa, dict) and isinstance(intencion_activa.get("id"), str) and intencion_activa["id"]:
        ids.append(intencion_activa["id"])
    pulso_id = pulso.get("id")
    if isinstance(pulso_id, int) and not isinstance(pulso_id, bool) and pulso_id >= 1:
        ids.append(f"int-{pulso_id}")
    return _deduplicar(ids)


def _extraer_obras(pulso: dict[str, Any]) -> list[str]:
    return _deduplicar(
        item["id"]
        for item in (pulso.get("obras") or [])
        if isinstance(item, dict) and isinstance(item.get("id"), str)
    )


def _extraer_episodios(pulso: dict[str, Any]) -> list[str]:
    episodios = pulso.get("episodios_diario")
    if not isinstance(episodios, dict):
        return []
    return _deduplicar(k for k in episodios.keys() if isinstance(k, str))


def estrechar_esquema(
    pulso: dict[str, Any],
    esquema_base: dict[str, Any] | None = None,
) -> dict[str, Any]:
    """Construye un JSON Schema válido, subconjunto estricto de `respuesta.schema.json`,
    cerrando dinámicamente acciones, lugares, referencias de entidades, hitos, obras y episodios
    al contenido del propio `pulso` (P-11).
    """
    base = copy.deepcopy(esquema_base) if esquema_base is not None else contrato.cargar_esquema("respuesta")
    esquema = _reescribir_refs_comun(base)

    comun_defs = copy.deepcopy(contrato.cargar_esquema("comun")["$defs"])
    defs = esquema.setdefault("$defs", {})
    for nombre_def, definicion in comun_defs.items():
        defs.setdefault(nombre_def, definicion)

    pulso_id = pulso.get("id", "sin-id")
    esquema["$id"] = f"https://dr4co.local/corazon-pelican/schemas/pulso-{pulso_id}/respuesta.schema.json"

    props = esquema.get("properties", {})
    if "pensamiento" in props and next(iter(props)) != "pensamiento":
        esquema["properties"] = {"pensamiento": props["pensamiento"], **{k: v for k, v in props.items() if k != "pensamiento"}}

    acciones_base = list(comun_defs["accion"]["enum"])
    defs["accion"]["enum"] = _extraer_acciones(pulso, acciones_base)
    defs["idLexico"]["enum"] = _extraer_lugares(pulso)
    defs["ref"]["enum"] = _extraer_entidades(pulso)
    defs["idHito"]["enum"] = _extraer_hitos(pulso)
    defs["idObra"]["enum"] = _extraer_obras(pulso)
    defs["idEpisodio"]["enum"] = _extraer_episodios(pulso)

    agenda_prop = esquema.get("properties", {}).get("agenda")
    if isinstance(agenda_prop, dict):
        items_prop = agenda_prop.get("items")
        if isinstance(items_prop, dict) and isinstance(items_prop.get("properties"), dict):
            if "hito" in items_prop["properties"]:
                items_prop["properties"]["hito"] = {
                    "type": "string",
                    "enum": _extraer_hitos_agenda(pulso),
                }

    return esquema


estrechar = estrechar_esquema


def validar_respuesta_estrechada(
    pulso: dict[str, Any],
    respuesta: Any,
    esquema: dict[str, Any] | None = None,
) -> list[str]:
    """Valida `respuesta` contra el esquema estrechado del pulso (y devuelve errores legibles)."""
    esquema_efectivo = esquema if esquema is not None else estrechar_esquema(pulso)
    validador = Draft202012Validator(esquema_efectivo)
    return sorted(
        f"{'/'.join(map(str, e.absolute_path)) or '(raíz)'}: {e.message}"
        for e in validador.iter_errors(respuesta)
    )


class ErrorModeloError(RuntimeError):
    """Error al invocar o configurar el proveedor del modelo."""


class PresupuestoTokensAgotadoError(ErrorModeloError):
    """Se agotó el presupuesto diario de tokens configurado para la mente."""


@dataclass(frozen=True)
class ConfigModelo:
    """Configuración declarativa de modelos y presupuesto (ADS §6)."""

    proveedor: str = "fake"
    modelo: str = "fake-mente"
    modelo_reflejos: str | None = None
    temperatura: float = 0.7
    max_tokens: int = 1024
    presupuesto_tokens_dia: int | None = None
    api_key_env: str | None = None


@dataclass(frozen=True)
class LlamadaModelo:
    """Registro de una invocación al modelo para pruebas e inspección."""

    prompt: PromptEnsamblado
    esquema: dict[str, Any]
    respuesta: dict[str, Any] | str
    tokens_estimados: int = 0
    uso: UsoTokens | None = None
    en_cache: bool = False
    modelo: str = "fake-mente"


@runtime_checkable
class ModeloMente(Protocol):
    """Interfaz común de modelos de la mente."""

    def generar(self, prompt: PromptEnsamblado, esquema: dict[str, Any]) -> dict[str, Any] | str: ...


GeneradorFake = Callable[[PromptEnsamblado, dict[str, Any]], dict[str, Any] | str]


class FakeLLM:
    """Modelo determinista para pruebas sin red ni claves reales."""

    def __init__(
        self,
        respuestas: Iterable[dict[str, Any] | str | GeneradorFake | Exception] | None = None,
        *,
        generador: GeneradorFake | None = None,
        pensamiento_por_defecto: str = "Observo la escena en silencio antes de decidir.",
        presupuesto_tokens_dia: int | None = None,
        modelo: str = "fake-mente",
        usos: Iterable[UsoTokens] | None = None,
        simular_cache: bool = True,
    ) -> None:
        self._cola: list[dict[str, Any] | str | GeneradorFake | Exception] = list(respuestas or [])
        self._generador = generador
        self.pensamiento_por_defecto = pensamiento_por_defecto
        self.presupuesto_tokens_dia = presupuesto_tokens_dia
        self.modelo = modelo
        self._usos: list[UsoTokens] = list(usos or [])
        self.simular_cache = simular_cache
        self._prefijos_cache: set[str] = set()
        self.tokens_usados = 0
        self.ultimo_uso: UsoTokens | None = None
        self.llamadas: list[LlamadaModelo] = []

    def generar(self, prompt: PromptEnsamblado, esquema: dict[str, Any]) -> dict[str, Any] | str:
        tokens = max(1, (len(prompt.sistema) + len(prompt.usuario)) // 4)
        if self.presupuesto_tokens_dia is not None and self.tokens_usados + tokens > self.presupuesto_tokens_dia:
            raise PresupuestoTokensAgotadoError(
                f"Presupuesto diario de tokens agotado ({self.tokens_usados}/{self.presupuesto_tokens_dia})."
            )

        if self._cola:
            siguiente = self._cola.pop(0)
            if isinstance(siguiente, Exception):
                raise siguiente
            salida = siguiente(prompt, esquema) if callable(siguiente) else copy.deepcopy(siguiente)
        elif self._generador is not None:
            salida = self._generador(prompt, esquema)
        else:
            salida = {"pensamiento": self.pensamiento_por_defecto}

        if self._usos:
            uso = self._usos.pop(0)
        else:
            huella = hash_prefijo_sistema(prompt.sistema) if (self.simular_cache and prompt.sistema) else ""
            cache_activa = bool(huella and huella in self._prefijos_cache)
            uso = estimar_uso_prompt(prompt, salida, cache_activa=cache_activa)
            if huella:
                self._prefijos_cache.add(huella)

        self.ultimo_uso = uso
        self.tokens_usados += uso.total_tokens
        self.llamadas.append(
            LlamadaModelo(
                prompt=prompt,
                esquema=esquema,
                respuesta=copy.deepcopy(salida) if isinstance(salida, dict) else salida,
                tokens_estimados=uso.total_tokens,
                uso=uso,
                en_cache=uso.en_cache,
                modelo=self.modelo,
            )
        )
        return salida


_ENV_POR_PROVEEDOR = {
    "openai": "OPENAI_API_KEY",
    "anthropic": "ANTHROPIC_API_KEY",
    "gemini": "GEMINI_API_KEY",
    "google": "GEMINI_API_KEY",
}


class ModeloProveedorConfigurado:
    """Adaptador diferido para proveedores reales; no exige clave al instanciar."""

    def __init__(self, config: ConfigModelo) -> None:
        self.config = config
        self.modelo = config.modelo
        self.ultimo_uso: UsoTokens | None = None

    def generar(self, prompt: PromptEnsamblado, esquema: dict[str, Any]) -> dict[str, Any] | str:
        env_var = self.config.api_key_env or _ENV_POR_PROVEEDOR.get(self.config.proveedor)
        if env_var and not os.environ.get(env_var):
            raise ErrorModeloError(
                f"Falta la variable de entorno {env_var} para el proveedor '{self.config.proveedor}'."
            )
        raise ErrorModeloError(
            f"El transporte de red para el proveedor '{self.config.proveedor}' "
            f"(modelo '{self.config.modelo}') requiere un adaptador activo."
        )


def obtener_modelo_y_uso(
    modelo: ModeloMente,
    prompt: PromptEnsamblado,
    respuesta: dict[str, Any] | str | None,
    *,
    cache_activa: bool = False,
) -> tuple[str, UsoTokens]:
    """Extrae el nombre de modelo y `UsoTokens` reportado o los estima si el proveedor no informa."""
    nombre_modelo = getattr(modelo, "modelo", None)
    if not nombre_modelo:
        cfg = getattr(modelo, "config", None)
        nombre_modelo = getattr(cfg, "modelo", None) or "desconocido"

    uso = getattr(modelo, "ultimo_uso", None)
    if not isinstance(uso, UsoTokens):
        uso = estimar_uso_prompt(prompt, respuesta, cache_activa=cache_activa)

    return str(nombre_modelo), uso


def crear_modelo(
    config: ConfigModelo | None = None,
    *,
    fake_respuestas: Iterable[dict[str, Any] | str | GeneradorFake | Exception] | None = None,
    fake_generador: GeneradorFake | None = None,
) -> ModeloMente:
    """Enruta la configuración de modelo sin exigir claves reales para pruebas."""
    cfg = config or ConfigModelo()
    proveedor = cfg.proveedor.lower().strip()
    if proveedor == "fake":
        return FakeLLM(
            respuestas=fake_respuestas,
            generador=fake_generador,
            presupuesto_tokens_dia=cfg.presupuesto_tokens_dia,
            modelo=cfg.modelo,
        )
    if proveedor in {"openai", "anthropic", "gemini", "google", "local"}:
        return ModeloProveedorConfigurado(cfg)
    raise ErrorModeloError(f"Proveedor de modelo desconocido: '{cfg.proveedor}'.")
