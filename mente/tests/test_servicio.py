"""Pruebas unitarias del servicio CLI/daemon `python -m mente`."""

from __future__ import annotations

import logging
import threading
from pathlib import Path
from typing import Any

import httpx

from mente import contrato
from mente.cliente_motor import ClienteMotor
from mente.modelos import FakeLLM
from mente.servicio import (
    MODELO_BEDROCK_POR_DEFECTO,
    PROVEEDOR_POR_DEFECTO,
    cargar_config,
    construir_ciclo,
    ejecutar_bucle,
    resolver_token,
)
from mente.tests.test_mente import MotorSimulado, RAIZ


def test_config_por_defecto_apunta_a_bedrock_nova_2_lite() -> None:
    cfg = cargar_config([], env={})
    assert cfg.proveedor == PROVEEDOR_POR_DEFECTO == "bedrock"
    assert cfg.modelo == MODELO_BEDROCK_POR_DEFECTO == "eu.amazon.nova-2-lite-v1:0"
    assert cfg.puerto == 25560
    assert cfg.personaje == "leah"


def test_config_lee_entorno_y_token_file(tmp_path: Path) -> None:
    tok = tmp_path / "corazon.token"
    tok.write_text("  secreto-loopback-123 \n", encoding="utf-8")

    cfg = cargar_config(
        ["--una-vez"],
        env={
            "CORAZON_API_PUERTO": "25561",
            "CORAZON_API_TOKEN_FILE": str(tok),
            "CORAZON_LIMITE_TOKENS_DIA": "50000",
            "CORAZON_ESTADO_DIR": str(tmp_path / "estado"),
        },
    )
    assert cfg.puerto == 25561
    assert cfg.limite_tokens_dia == 50000
    assert cfg.estado_dir == tmp_path / "estado"
    assert cfg.una_vez is True
    assert resolver_token(cfg) == "secreto-loopback-123"


def test_construir_ciclo_y_ejecutar_una_vez_persiste_cursor_y_costes(
    tmp_path: Path, caplog: Any
) -> None:
    pulso = {
        "id": 1,
        "epoca": "ep-nova-1",
        "personaje": "leah",
        "motivo": "deriva",
        "tiempo_juego": {"dia": 1, "hora": 900},
        "tiempo_real_utc": "2026-11-23T01:00:00Z",
        "escena": "La luz de la mañana entra en el estudio.",
        "hechos": {"energia": "alta", "cerca": [], "entidades": []},
        "acciones_disponibles": ["mirar", "volver_a_rutina"],
        "lugares": [{"id": "tu_casa", "prosa": "tu casa"}],
        "bufer": [],
        "huella": {"desde_ultimo_pensamiento_min_juego": 5, "prosa": "Silencio.", "entidades": []},
        "agenda": [],
        "idioma": "es",
    }
    motor = MotorSimulado(epoca="ep-nova-1", token="tok-1")
    motor.pulsos = [pulso]
    cliente = ClienteMotor(transport=httpx.MockTransport(motor.handler), token="tok-1")
    llm = FakeLLM(
        respuestas=[{"pensamiento": "La luz sobre la madera pide un boceto lento.", "epoca": "ep-nova-1", "basada_en": 1}],
        modelo=MODELO_BEDROCK_POR_DEFECTO,
    )

    cfg = cargar_config(
        ["--una-vez", "--estado-dir", str(tmp_path), "--token", "tok-1"],
        env={},
    )
    with caplog.at_level(logging.INFO, logger="corazon-pelican.mente"):
        ciclo, _ = construir_ciclo(cfg, modelo=llm, cliente=cliente, raiz_repo=RAIZ)
        rc = ejecutar_bucle(cfg, ciclo=ciclo, cliente=cliente)

    assert rc == 0
    assert not [r for r in caplog.records if r.levelno >= logging.ERROR]
    assert len(motor.respuestas_recibidas) == 1
    assert (tmp_path / "cursor.json").is_file()
    assert (tmp_path / "costes-mente.jsonl").is_file()
    assert (tmp_path / "telemetria-mente.jsonl").is_file()


def test_bucle_sobrevive_a_caida_temporal_del_motor(tmp_path: Path) -> None:
    intentos = {"n": 0}
    motor = MotorSimulado(epoca="ep-recup")

    def handler_con_caida_inicial(request: httpx.Request) -> httpx.Response:
        intentos["n"] += 1
        if intentos["n"] == 1:
            raise httpx.ConnectError("motor reiniciando", request=request)
        return motor.handler(request)

    cliente = ClienteMotor(transport=httpx.MockTransport(handler_con_caida_inicial))
    cfg = cargar_config(["--pausa-error-s", "0", "--estado-dir", str(tmp_path)], env={})
    ciclo, _ = construir_ciclo(cfg, modelo=FakeLLM(), cliente=cliente, raiz_repo=RAIZ)

    dormidas: list[float] = []
    stop = threading.Event()
    rc = ejecutar_bucle(
        cfg,
        ciclo=ciclo,
        cliente=cliente,
        parar=stop,
        max_iteraciones=2,
        dormir=dormidas.append,
    )

    assert rc == 0
    assert intentos["n"] >= 2
    assert ciclo.cursor.epoca == "ep-recup"
