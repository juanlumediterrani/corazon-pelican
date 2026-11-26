"""Estrechamiento de esquema por pulso, interfaz de modelos y FakeLLM (ADS §6, P-11)."""

from __future__ import annotations

import copy
import json
import os
import subprocess
import tempfile
from collections.abc import Callable, Iterable
from dataclasses import dataclass, field
from typing import Any, Protocol, runtime_checkable

import httpx
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


def _extraer_episodios(
    pulso: dict[str, Any],
    episodios_adicionales: Iterable[str] | None = None,
) -> list[str]:
    episodios = pulso.get("episodios_diario")
    if isinstance(episodios, dict):
        return _deduplicar(k for k in episodios.keys() if isinstance(k, str))
    ids: list[str] = []
    if episodios_adicionales is not None:
        ids.extend(ep for ep in episodios_adicionales if isinstance(ep, str) and ep)
    humor = pulso.get("humor")
    if isinstance(humor, dict):
        for ep in humor.get("origen_diario") or []:
            if isinstance(ep, str) and ep:
                ids.append(ep)
    return _deduplicar(ids)


def estrechar_esquema(
    pulso: dict[str, Any],
    esquema_base: dict[str, Any] | None = None,
    *,
    episodios_adicionales: Iterable[str] | None = None,
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
    defs["idEpisodio"]["enum"] = _extraer_episodios(pulso, episodios_adicionales)

    if pulso.get("episodios_diario") is None:
        memoria_schema = props.get("memoria")
        if isinstance(memoria_schema, dict):
            mem_props = memoria_schema.get("properties")
            if isinstance(mem_props, dict):
                mem_props.pop("reflexiones", None)
            memoria_schema.pop("oneOf", None)
            memoria_schema["required"] = ["recordar"]

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
    base_url: str | None = None
    timeout_s: float = 30.0


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

_URL_BASE_POR_PROVEEDOR = {
    "openai": "https://api.openai.com/v1",
    "local": "http://127.0.0.1:11434/v1",
    "anthropic": "https://api.anthropic.com/v1",
    "gemini": "https://generativelanguage.googleapis.com/v1beta",
    "google": "https://generativelanguage.googleapis.com/v1beta",
    "bedrock": "bedrock-runtime",
}


def _bedrock_cache_point_habilitado(sistema_texto: str) -> bool:
    flag = str(os.environ.get("CORAZON_BEDROCK_PROMPT_CACHE") or "").strip().lower()
    if flag in {"0", "false", "no", "off"}:
        return False
    return len(sistema_texto) >= 1500


class ModeloProveedorConfigurado:
    """Adaptador HTTP/Bedrock para proveedores OpenAI-compatibles, Anthropic, Gemini y AWS Bedrock."""

    def __init__(
        self,
        config: ConfigModelo,
        *,
        transport: httpx.BaseTransport | None = None,
        client: httpx.Client | None = None,
        bedrock_invoker: Callable[[str, dict[str, Any]], dict[str, Any]] | None = None,
    ) -> None:
        self.config = config
        self.modelo = config.modelo
        self.ultimo_uso: UsoTokens | None = None
        self._transport = transport
        self._client = client
        self._bedrock_invoker = bedrock_invoker

    def _resolver_credenciales(self) -> tuple[str, str | None, str]:
        proveedor = self.config.proveedor.lower().strip()
        if proveedor not in _URL_BASE_POR_PROVEEDOR:
            raise ErrorModeloError(f"Proveedor de modelo desconocido: '{self.config.proveedor}'.")

        env_var = self.config.api_key_env or _ENV_POR_PROVEEDOR.get(proveedor)
        api_key: str | None = None
        if env_var:
            api_key = os.environ.get(env_var)
            if not api_key:
                raise ErrorModeloError(
                    f"Falta la variable de entorno {env_var} para el proveedor '{self.config.proveedor}'."
                )

        base_url = (self.config.base_url or _URL_BASE_POR_PROVEEDOR[proveedor]).rstrip("/")
        return proveedor, api_key, base_url

    def _post_json(
        self,
        *,
        base_url: str,
        ruta: str,
        headers: dict[str, str],
        payload: dict[str, Any],
    ) -> dict[str, Any]:
        try:
            if self._client is not None:
                url = ruta if self._client.base_url else f"{base_url}{ruta}"
                resp = self._client.post(url, headers=headers, json=payload, timeout=self.config.timeout_s)
            else:
                with httpx.Client(
                    base_url=base_url,
                    transport=self._transport,
                    timeout=self.config.timeout_s,
                ) as cliente:
                    resp = cliente.post(ruta, headers=headers, json=payload)
        except httpx.HTTPError as exc:
            raise ErrorModeloError(
                f"Fallo de comunicación con el proveedor '{self.config.proveedor}' ({self.config.modelo}): {exc}"
            ) from exc

        if resp.status_code >= 400:
            detalle = resp.text.strip().replace("\n", " ")[:240]
            raise ErrorModeloError(
                f"El proveedor '{self.config.proveedor}' devolvió HTTP {resp.status_code}: {detalle}"
            )

        try:
            cuerpo = resp.json()
        except ValueError as exc:
            raise ErrorModeloError(
                f"El proveedor '{self.config.proveedor}' devolvió un cuerpo no JSON válido."
            ) from exc

        if not isinstance(cuerpo, dict):
            raise ErrorModeloError(
                f"La respuesta HTTP de '{self.config.proveedor}' debe ser un objeto JSON."
            )
        return cuerpo

    def _invocar_openai_compatible(
        self,
        prompt: PromptEnsamblado,
        esquema: dict[str, Any],
        base_url: str,
        api_key: str | None,
    ) -> dict[str, Any] | str:
        headers: dict[str, str] = {"Content-Type": "application/json"}
        if api_key:
            headers["Authorization"] = f"Bearer {api_key}"

        payload = {
            "model": self.config.modelo,
            "temperature": self.config.temperatura,
            "max_tokens": self.config.max_tokens,
            "messages": [
                {"role": "system", "content": prompt.sistema},
                {"role": "user", "content": prompt.usuario},
            ],
            "response_format": {
                "type": "json_schema",
                "json_schema": {
                    "name": "respuesta_pulso",
                    "strict": False,
                    "schema": esquema,
                },
            },
        }
        cuerpo = self._post_json(base_url=base_url, ruta="/chat/completions", headers=headers, payload=payload)
        usage = cuerpo.get("usage")
        if isinstance(usage, dict):
            prompt_tokens = int(usage.get("prompt_tokens") or 0)
            detalles = usage.get("prompt_tokens_details") or {}
            cached = int(detalles.get("cached_tokens") or 0) if isinstance(detalles, dict) else 0
            tokens_entrada = max(0, prompt_tokens - cached)
            tokens_salida = max(0, int(usage.get("completion_tokens") or 0))
            self.ultimo_uso = UsoTokens(
                tokens_entrada=tokens_entrada,
                tokens_cache=max(0, cached),
                tokens_salida=tokens_salida,
            )

        choices = cuerpo.get("choices")
        if not isinstance(choices, list) or not choices:
            raise ErrorModeloError("El proveedor OpenAI-compatible devolvió una respuesta sin `choices`.")
        mensaje = choices[0].get("message") if isinstance(choices[0], dict) else None
        contenido = mensaje.get("content") if isinstance(mensaje, dict) else None
        if isinstance(contenido, (dict, str)) and contenido != "":
            return contenido
        raise ErrorModeloError("El proveedor OpenAI-compatible no incluyó contenido en `message.content`.")

    def _invocar_anthropic(
        self,
        prompt: PromptEnsamblado,
        esquema: dict[str, Any],
        base_url: str,
        api_key: str,
    ) -> dict[str, Any] | str:
        headers = {
            "Content-Type": "application/json",
            "x-api-key": api_key,
            "anthropic-version": "2023-06-01",
        }
        payload = {
            "model": self.config.modelo,
            "max_tokens": self.config.max_tokens,
            "temperature": self.config.temperatura,
            "system": [
                {
                    "type": "text",
                    "text": prompt.sistema,
                    "cache_control": {"type": "ephemeral"},
                }
            ],
            "messages": [{"role": "user", "content": prompt.usuario}],
            "tools": [
                {
                    "name": "responder_pulso",
                    "description": "Responde al pulso del motor siguiendo exactamente el esquema del personaje.",
                    "input_schema": esquema,
                }
            ],
            "tool_choice": {"type": "tool", "name": "responder_pulso"},
        }
        cuerpo = self._post_json(base_url=base_url, ruta="/messages", headers=headers, payload=payload)
        usage = cuerpo.get("usage")
        if isinstance(usage, dict):
            self.ultimo_uso = UsoTokens(
                tokens_entrada=max(0, int(usage.get("input_tokens") or 0)),
                tokens_cache=max(0, int(usage.get("cache_read_input_tokens") or 0)),
                tokens_salida=max(0, int(usage.get("output_tokens") or 0)),
            )

        content = cuerpo.get("content")
        if not isinstance(content, list) or not content:
            raise ErrorModeloError("El proveedor Anthropic devolvió una respuesta sin bloques `content`.")
        for bloque in content:
            if isinstance(bloque, dict) and bloque.get("type") == "tool_use" and isinstance(bloque.get("input"), dict):
                return bloque["input"]
        for bloque in content:
            if isinstance(bloque, dict) and isinstance(bloque.get("text"), str) and bloque["text"]:
                return bloque["text"]
        raise ErrorModeloError("El proveedor Anthropic no devolvió ningún bloque útil en `content`.")

    def _invocar_gemini(
        self,
        prompt: PromptEnsamblado,
        esquema: dict[str, Any],
        base_url: str,
        api_key: str,
    ) -> dict[str, Any] | str:
        headers = {
            "Content-Type": "application/json",
            "x-goog-api-key": api_key,
        }
        payload = {
            "systemInstruction": {"parts": [{"text": prompt.sistema}]},
            "contents": [{"role": "user", "parts": [{"text": prompt.usuario}]}],
            "generationConfig": {
                "temperature": self.config.temperatura,
                "maxOutputTokens": self.config.max_tokens,
                "responseMimeType": "application/json",
                "responseJsonSchema": esquema,
            },
        }
        cuerpo = self._post_json(
            base_url=base_url,
            ruta=f"/models/{self.config.modelo}:generateContent",
            headers=headers,
            payload=payload,
        )
        usage = cuerpo.get("usageMetadata")
        if isinstance(usage, dict):
            prompt_tokens = int(usage.get("promptTokenCount") or 0)
            cached = int(usage.get("cachedContentTokenCount") or 0)
            self.ultimo_uso = UsoTokens(
                tokens_entrada=max(0, prompt_tokens - cached),
                tokens_cache=max(0, cached),
                tokens_salida=max(0, int(usage.get("candidatesTokenCount") or 0)),
            )

        candidates = cuerpo.get("candidates")
        if not isinstance(candidates, list) or not candidates:
            raise ErrorModeloError("El proveedor Gemini devolvió una respuesta sin `candidates`.")
        contenido = candidates[0].get("content") if isinstance(candidates[0], dict) else None
        partes = contenido.get("parts") if isinstance(contenido, dict) else None
        if isinstance(partes, list) and partes and isinstance(partes[0], dict):
            texto = partes[0].get("text")
            if isinstance(texto, str) and texto:
                return texto
        raise ErrorModeloError("El proveedor Gemini no devolvió texto en `candidates[0].content.parts`.")

    def _ejecutar_bedrock(self, model_id: str, body: dict[str, Any]) -> dict[str, Any]:
        if self._bedrock_invoker is not None:
            return self._bedrock_invoker(model_id, body)
        return self._llamar_bedrock_nativo(model_id, body)

    def _invocar_bedrock(
        self,
        prompt: PromptEnsamblado,
        esquema: dict[str, Any],
    ) -> dict[str, Any] | str:
        instruccion_json = (
            "Responde ÚNICAMENTE con un objeto JSON válido que cumpla exactamente "
            f"este esquema JSON (sin bloques Markdown ni texto extra):\n"
            f"{json.dumps(esquema, ensure_ascii=False, separators=(',', ':'))}"
        )
        sistema_blocks: list[dict[str, Any]] = [{"text": prompt.sistema}]
        usar_cache = _bedrock_cache_point_habilitado(prompt.sistema)
        if usar_cache:
            sistema_blocks.append({"cachePoint": {"type": "default"}})

        user_texto = f"{prompt.usuario}\n\n{instruccion_json}"
        body = {
            "schemaVersion": "messages-v1",
            "system": sistema_blocks,
            "messages": [{"role": "user", "content": [{"text": user_texto}]}],
            "inferenceConfig": {
                "maxTokens": self.config.max_tokens,
                "temperature": self.config.temperatura,
            },
        }
        model_id = self.config.modelo or "eu.amazon.nova-2-lite-v1:0"

        try:
            payload = self._ejecutar_bedrock(model_id, body)
        except Exception as exc:
            mensaje = str(exc).lower()
            if usar_cache and "validationexception" in mensaje and "cachepoint" in mensaje:
                body_sin_cache = {**body, "system": [{"text": prompt.sistema}]}
                payload = self._ejecutar_bedrock(model_id, body_sin_cache)
            else:
                raise

        usage = payload.get("usage")
        if isinstance(usage, dict):
            cached = int(usage.get("cacheReadInputTokens") or 0)
            cache_write = int(usage.get("cacheWriteInputTokens") or 0)
            inp = int(usage.get("inputTokens") or 0)
            out = int(usage.get("outputTokens") or 0)
            self.ultimo_uso = UsoTokens(
                tokens_entrada=max(0, inp) + max(0, cache_write),
                tokens_cache=max(0, cached),
                tokens_salida=max(0, out),
            )

        output = payload.get("output")
        msg = output.get("message") if isinstance(output, dict) else None
        parts = msg.get("content") if isinstance(msg, dict) else None
        if not isinstance(parts, list) or not parts:
            raise ErrorModeloError("El proveedor Bedrock devolvió una respuesta sin `output.message.content`.")
        texto = "".join(part.get("text", "") for part in parts if isinstance(part, dict)).strip()
        if not texto:
            raise ErrorModeloError("El proveedor Bedrock devolvió texto vacío.")
        # Limpiar posibles fences ```json si el modelo los incluyó
        if texto.startswith("```"):
            lineas = texto.splitlines()
            if len(lineas) >= 3 and lineas[-1].strip().startswith("```"):
                texto = "\n".join(lineas[1:-1]).strip()
        return texto

    def _llamar_bedrock_nativo(self, model_id: str, body: dict[str, Any]) -> dict[str, Any]:
        try:
            import boto3  # type: ignore[import-not-found]
        except ImportError:
            boto3 = None

        if boto3 is not None:
            try:
                client = boto3.client("bedrock-runtime")
                resp = client.invoke_model(
                    modelId=model_id,
                    body=json.dumps(body),
                    contentType="application/json",
                    accept="application/json",
                )
                return json.loads(resp["body"].read())
            except Exception as exc:
                raise ErrorModeloError(f"Fallo de AWS Bedrock ({model_id}): {exc}") from exc

        # Fallback oficial usando AWS CLI (`aws bedrock-runtime invoke-model`) cuando boto3 no está instalado.
        tmp_path: str | None = None
        try:
            with tempfile.NamedTemporaryFile(prefix="cp-bedrock-", suffix=".json", delete=False) as tmp:
                tmp_path = tmp.name
            proc = subprocess.run(
                [
                    "aws",
                    "bedrock-runtime",
                    "invoke-model",
                    "--model-id",
                    model_id,
                    "--content-type",
                    "application/json",
                    "--accept",
                    "application/json",
                    "--cli-binary-format",
                    "raw-in-base64-out",
                    "--body",
                    json.dumps(body),
                    tmp_path,
                ],
                capture_output=True,
                text=True,
                timeout=self.config.timeout_s,
                check=False,
            )
            if proc.returncode != 0:
                detalle = (proc.stderr or proc.stdout or "").strip().replace("\n", " ")[:240]
                raise ErrorModeloError(f"AWS CLI bedrock-runtime devolvió código {proc.returncode}: {detalle}")
            with open(tmp_path, encoding="utf-8") as fh:
                return json.load(fh)
        except ErrorModeloError:
            raise
        except Exception as exc:
            raise ErrorModeloError(f"Fallo invocando AWS Bedrock CLI ({model_id}): {exc}") from exc
        finally:
            if tmp_path and os.path.exists(tmp_path):
                try:
                    os.unlink(tmp_path)
                except OSError:
                    pass

    def generar(self, prompt: PromptEnsamblado, esquema: dict[str, Any]) -> dict[str, Any] | str:
        self.ultimo_uso = None
        proveedor, api_key, base_url = self._resolver_credenciales()
        if proveedor == "bedrock":
            return self._invocar_bedrock(prompt, esquema)
        if proveedor in {"openai", "local"}:
            return self._invocar_openai_compatible(prompt, esquema, base_url, api_key)
        if proveedor == "anthropic":
            assert api_key is not None
            return self._invocar_anthropic(prompt, esquema, base_url, api_key)
        assert api_key is not None
        return self._invocar_gemini(prompt, esquema, base_url, api_key)


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
    transport: httpx.BaseTransport | None = None,
    client: httpx.Client | None = None,
    bedrock_invoker: Callable[[str, dict[str, Any]], dict[str, Any]] | None = None,
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
    if proveedor in {"openai", "anthropic", "gemini", "google", "local", "bedrock"}:
        return ModeloProveedorConfigurado(cfg, transport=transport, client=client, bedrock_invoker=bedrock_invoker)
    raise ErrorModeloError(f"Proveedor de modelo desconocido: '{cfg.proveedor}'.")
