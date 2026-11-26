"""Servicio y punto de entrada CLI para ejecutar el bucle de la mente (`python -m mente`)."""

from __future__ import annotations

import argparse
import logging
import os
import signal
import threading
import time
from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass
from pathlib import Path

import httpx

from mente.cliente_motor import (
    AlmacenCursor,
    CicloMente,
    ClienteMotor,
    RegistroTelemetria,
)
from mente.costes import PresupuestoTokens, RegistroCostes
from mente.ensamblador import cargar_voz
from mente.modelos import ConfigModelo, ModeloMente, crear_modelo

LOG = logging.getLogger("corazon-pelican.mente")

PROVEEDOR_POR_DEFECTO = "bedrock"
MODELO_BEDROCK_POR_DEFECTO = "eu.amazon.nova-2-lite-v1:0"
RAIZ_REPO_POR_DEFECTO = Path(__file__).resolve().parent.parent


@dataclass(frozen=True)
class ConfigServicioMente:
    """Parámetros resueltos para conectar la mente al motor del juego y al LLM."""

    puerto: int = 25560
    base_url: str | None = None
    token: str | None = None
    token_file: Path | None = None
    personaje: str = "leah"
    voz_path: Path | None = None
    assets_dir: Path | None = None
    proveedor: str = PROVEEDOR_POR_DEFECTO
    modelo: str = MODELO_BEDROCK_POR_DEFECTO
    temperatura: float = 0.7
    max_tokens: int = 1024
    timeout_s: float = 20.0
    limite_tokens_dia: int | None = None
    limite_coste_usd_dia: float | None = None
    estado_dir: Path | None = None
    espera_ms: int = 25000
    pausa_error_s: float = 5.0
    una_vez: bool = False


def _leer_env_opcional(env: Mapping[str, str], clave: str) -> str | None:
    valor = env.get(clave)
    if valor is None:
        return None
    limpio = valor.strip()
    return limpio or None


_VALORES_LIMITE_DESACTIVADO = frozenset({"none", "null", "off", "ilimitado", "sin_limite"})


def _parsear_limite_int_opcional(valor: str | None) -> int | None:
    if valor is None:
        return None
    limpio = valor.strip().lower()
    if not limpio or limpio in _VALORES_LIMITE_DESACTIVADO:
        return None
    numero = int(limpio)
    return numero if numero > 0 else None


def _parsear_limite_float_opcional(valor: str | None) -> float | None:
    if valor is None:
        return None
    limpio = valor.strip().lower()
    if not limpio or limpio in _VALORES_LIMITE_DESACTIVADO:
        return None
    numero = float(limpio)
    return numero if numero > 0 else None


def resolver_ruta_voz(
    personaje: str,
    *,
    voz_path: Path | None = None,
    assets_dir: Path | None = None,
    raiz_repo: Path = RAIZ_REPO_POR_DEFECTO,
) -> Path:
    """Localiza `{personaje}.voz.md` a partir de ruta explícita, directorio de assets o repo."""
    if voz_path is not None:
        return voz_path
    if assets_dir is not None:
        return assets_dir / "personajes" / f"{personaje}.voz.md"

    candidatos = (
        raiz_repo / "CorazonPelican" / "assets" / "personajes" / f"{personaje}.voz.md",
        raiz_repo / "assets" / "personajes" / f"{personaje}.voz.md",
    )
    for candidato in candidatos:
        if candidato.is_file():
            return candidato
    return candidatos[0]


def resolver_token(cfg: ConfigServicioMente) -> str | None:
    """Devuelve el token Bearer leído de `token_file` o de `token`, si existe."""
    if cfg.token_file is not None and cfg.token_file.is_file():
        leido = cfg.token_file.read_text(encoding="utf-8").strip()
        if leido:
            return leido
    if cfg.token:
        return cfg.token.strip() or None
    return None


def construir_parser(env: Mapping[str, str] | None = None) -> argparse.ArgumentParser:
    entorno = env if env is not None else os.environ
    proveedor_def = _leer_env_opcional(entorno, "CORAZON_PROVEEDOR") or PROVEEDOR_POR_DEFECTO
    modelo_def = _leer_env_opcional(entorno, "CORAZON_MODELO") or (
        MODELO_BEDROCK_POR_DEFECTO if proveedor_def == "bedrock" else "fake-mente"
    )
    token_file_def = _leer_env_opcional(entorno, "CORAZON_API_TOKEN_FILE")
    voz_def = _leer_env_opcional(entorno, "CORAZON_VOZ_PATH")
    assets_def = _leer_env_opcional(entorno, "CORAZON_ASSETS_DIR")
    estado_def = _leer_env_opcional(entorno, "CORAZON_ESTADO_DIR")
    lim_tok_def = _leer_env_opcional(entorno, "CORAZON_LIMITE_TOKENS_DIA")
    lim_usd_def = _leer_env_opcional(entorno, "CORAZON_LIMITE_COSTE_USD_DIA")

    parser = argparse.ArgumentParser(
        prog="python3 -m mente",
        description="Servicio de la mente sin cuerpo de Corazón Pelican (loop motor ↔ LLM).",
    )
    parser.add_argument(
        "--puerto",
        type=int,
        default=int(_leer_env_opcional(entorno, "CORAZON_API_PUERTO") or 25560),
        help="Puerto HTTP loopback del mod Corazón Pelican (por defecto: 25560).",
    )
    parser.add_argument(
        "--base-url",
        default=_leer_env_opcional(entorno, "CORAZON_API_URL"),
        help="URL base opcional del motor (sobreescribe --puerto).",
    )
    parser.add_argument(
        "--token",
        default=_leer_env_opcional(entorno, "CORAZON_API_TOKEN"),
        help="Token Bearer opcional del motor.",
    )
    parser.add_argument(
        "--token-file",
        type=Path,
        default=Path(token_file_def) if token_file_def else None,
        help="Ruta a un fichero con el token Bearer de la API del motor.",
    )
    parser.add_argument(
        "--personaje",
        default=_leer_env_opcional(entorno, "CORAZON_PERSONAJE") or "leah",
        help="Identificador del personaje gobernado (por defecto: leah).",
    )
    parser.add_argument(
        "--voz",
        type=Path,
        default=Path(voz_def) if voz_def else None,
        help="Ruta explícita al fichero *.voz.md del personaje.",
    )
    parser.add_argument(
        "--assets-dir",
        type=Path,
        default=Path(assets_def) if assets_def else None,
        help="Directorio raíz de assets que contiene personajes/<id>.voz.md.",
    )
    parser.add_argument(
        "--proveedor",
        default=proveedor_def,
        help="Proveedor LLM: bedrock, openai, anthropic, gemini, local o fake.",
    )
    parser.add_argument(
        "--modelo",
        default=modelo_def,
        help=f"Identificador de modelo o perfil de inferencia (por defecto: {MODELO_BEDROCK_POR_DEFECTO}).",
    )
    parser.add_argument(
        "--temperatura",
        type=float,
        default=float(_leer_env_opcional(entorno, "CORAZON_TEMPERATURA") or 0.7),
    )
    parser.add_argument(
        "--max-tokens",
        type=int,
        default=int(_leer_env_opcional(entorno, "CORAZON_MAX_TOKENS") or 1024),
    )
    parser.add_argument(
        "--timeout-s",
        type=float,
        default=float(_leer_env_opcional(entorno, "CORAZON_TIMEOUT_S") or 20.0),
    )
    parser.add_argument(
        "--limite-tokens-dia",
        type=_parsear_limite_int_opcional,
        default=_parsear_limite_int_opcional(lim_tok_def),
        help="Presupuesto diario máximo de tokens para pulsos no interlocutorios.",
    )
    parser.add_argument(
        "--limite-coste-usd-dia",
        type=_parsear_limite_float_opcional,
        default=_parsear_limite_float_opcional(lim_usd_def),
        help="Presupuesto diario máximo en USD para pulsos no interlocutorios.",
    )
    parser.add_argument(
        "--estado-dir",
        type=Path,
        default=Path(estado_def) if estado_def else None,
        help="Directorio para cursor.json, telemetria-mente.jsonl y costes-mente.jsonl.",
    )
    parser.add_argument(
        "--espera-ms",
        type=int,
        default=int(_leer_env_opcional(entorno, "CORAZON_ESPERA_MS") or 25000),
        help="Espera de long-polling GET /api/v1/pulsos en ms.",
    )
    parser.add_argument(
        "--pausa-error-s",
        type=float,
        default=float(_leer_env_opcional(entorno, "CORAZON_PAUSA_ERROR_S") or 5.0),
        help="Pausa en segundos tras error de red/arranque antes de reintentar.",
    )
    parser.add_argument(
        "--una-vez",
        action="store_true",
        help="Ejecuta una sola iteración de sincronización/pulsos y termina.",
    )
    return parser


def cargar_config(
    argv: Sequence[str] | None = None,
    *,
    env: Mapping[str, str] | None = None,
) -> ConfigServicioMente:
    """Parsea argumentos CLI y variables de entorno devolviendo `ConfigServicioMente`."""
    ns = construir_parser(env).parse_args(list(argv) if argv is not None else None)
    return ConfigServicioMente(
        puerto=ns.puerto,
        base_url=ns.base_url,
        token=ns.token,
        token_file=ns.token_file,
        personaje=ns.personaje,
        voz_path=ns.voz,
        assets_dir=ns.assets_dir,
        proveedor=ns.proveedor,
        modelo=ns.modelo,
        temperatura=ns.temperatura,
        max_tokens=ns.max_tokens,
        timeout_s=ns.timeout_s,
        limite_tokens_dia=ns.limite_tokens_dia,
        limite_coste_usd_dia=ns.limite_coste_usd_dia,
        estado_dir=ns.estado_dir,
        espera_ms=ns.espera_ms,
        pausa_error_s=ns.pausa_error_s,
        una_vez=ns.una_vez,
    )


def construir_ciclo(
    cfg: ConfigServicioMente,
    *,
    modelo: ModeloMente | None = None,
    cliente: ClienteMotor | None = None,
    raiz_repo: Path = RAIZ_REPO_POR_DEFECTO,
) -> tuple[CicloMente, ClienteMotor]:
    """Construye `ClienteMotor`, `ModeloMente` y `CicloMente` listos para ejecutar pasos."""
    ruta_voz = resolver_ruta_voz(
        cfg.personaje,
        voz_path=cfg.voz_path,
        assets_dir=cfg.assets_dir,
        raiz_repo=raiz_repo,
    )
    voz = cargar_voz(ruta_voz)

    cliente_motor = cliente or ClienteMotor(
        puerto=cfg.puerto,
        base_url=cfg.base_url,
        token=resolver_token(cfg),
        timeout=max(35.0, (cfg.espera_ms / 1000.0) + 10.0),
    )

    modelo_mente = modelo or crear_modelo(
        ConfigModelo(
            proveedor=cfg.proveedor,
            modelo=cfg.modelo,
            temperatura=cfg.temperatura,
            max_tokens=cfg.max_tokens,
            timeout_s=cfg.timeout_s,
        )
    )

    ruta_cursor = (cfg.estado_dir / "cursor.json") if cfg.estado_dir else None
    ruta_telemetria = (cfg.estado_dir / "telemetria-mente.jsonl") if cfg.estado_dir else None
    ruta_costes = (cfg.estado_dir / "costes-mente.jsonl") if cfg.estado_dir else None

    registro_costes = RegistroCostes(ruta_costes)
    presupuesto = PresupuestoTokens(
        limite_tokens_dia=cfg.limite_tokens_dia,
        limite_coste_usd_dia=cfg.limite_coste_usd_dia,
        modelo=cfg.modelo,
        registro=registro_costes,
    )

    ciclo = CicloMente(
        cliente_motor,
        modelo_mente,
        voz,
        almacen_cursor=AlmacenCursor(ruta_cursor),
        telemetria=RegistroTelemetria(ruta_telemetria),
        presupuesto_tokens=presupuesto,
        registro_costes=registro_costes,
    )
    return ciclo, cliente_motor


def _esperar_parada(parar: threading.Event, segundos: float, dormir: Callable[[float], None] | None) -> None:
    if segundos <= 0:
        return
    if dormir is None:
        parar.wait(segundos)
    elif not parar.is_set():
        dormir(segundos)


def ejecutar_bucle(
    cfg: ConfigServicioMente,
    *,
    ciclo: CicloMente | None = None,
    cliente: ClienteMotor | None = None,
    parar: threading.Event | None = None,
    max_iteraciones: int | None = None,
    dormir: Callable[[float], None] | None = None,
) -> int:
    """Ejecuta el bucle continuo de polling motor ↔ modelo tolerando reinicios del host."""
    stop_event = parar or threading.Event()
    ciclo_activo = ciclo
    cliente_propio: ClienteMotor | None = None

    if ciclo_activo is None:
        ciclo_activo, cliente_propio = construir_ciclo(cfg, cliente=cliente)
    elif cliente is None:
        cliente_propio = getattr(ciclo_activo, "cliente", None)

    iteraciones = 0
    try:
        while not stop_event.is_set():
            if max_iteraciones is not None and iteraciones >= max_iteraciones:
                break
            iteraciones += 1

            try:
                resultados = ciclo_activo.ejecutar_paso(espera_ms=cfg.espera_ms)
                for res in resultados:
                    LOG.info(
                        "Pulso #%s (%s) -> estado=%s intentos=%s",
                        res.pulso_id,
                        res.epoca,
                        res.estado,
                        res.intentos_modelo,
                    )
                if ciclo_activo.cursor.epoca is None:
                    _esperar_parada(stop_event, cfg.pausa_error_s, dormir)
            except (httpx.HTTPError, OSError) as exc:
                LOG.warning("Motor no disponible (%s); reintento en %.1fs.", exc, cfg.pausa_error_s)
                _esperar_parada(stop_event, cfg.pausa_error_s, dormir)
            except Exception as exc:  # noqa: BLE001 — un fallo puntual nunca debe tumbar el daemon
                LOG.exception("Error inesperado en iteración de la mente: %s", exc)
                _esperar_parada(stop_event, cfg.pausa_error_s, dormir)

            if cfg.una_vez:
                break
    finally:
        if cliente_propio is not None and cliente is None and hasattr(cliente_propio, "close"):
            cliente_propio.close()

    return 0


def main(argv: Sequence[str] | None = None) -> int:
    """Punto de entrada principal con configuración de logging y señales POSIX."""
    logging.basicConfig(
        level=logging.INFO,
        format="%(asctime)s %(levelname)s [%(name)s] %(message)s",
    )
    cfg = cargar_config(argv)
    stop_event = threading.Event()

    def _detener(signum: int, _frame: object) -> None:
        LOG.info("Señal %s recibida; cerrando servicio de la mente.", signum)
        stop_event.set()

    signal.signal(signal.SIGTERM, _detener)
    signal.signal(signal.SIGINT, _detener)

    LOG.info(
        "Iniciando mente de %s -> proveedor=%s modelo=%s puerto=%s (limite_tokens_dia=%s limite_coste_usd_dia=%s)",
        cfg.personaje,
        cfg.proveedor,
        cfg.modelo,
        cfg.puerto,
        cfg.limite_tokens_dia if cfg.limite_tokens_dia is not None else "sin_limite",
        f"{cfg.limite_coste_usd_dia:.4f}" if cfg.limite_coste_usd_dia is not None else "sin_limite",
    )
    return ejecutar_bucle(cfg, parar=stop_event)
