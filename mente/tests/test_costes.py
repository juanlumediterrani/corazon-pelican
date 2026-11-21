"""Pruebas de presupuesto de tokens, caché de prompt, costes.jsonl e informe diario (CP-207)."""

from __future__ import annotations

import json
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

import httpx
import pytest

from mente.cliente_motor import CicloMente, ClienteMotor, RegistroTelemetria
from mente.costes import (
    PresupuestoTokens,
    RegistroCostes,
    TarifaModelo,
    UsoTokens,
    calcular_coste_usd,
    estimar_coste_diario,
    formatear_informe_coste_diario,
    generar_informe_coste_diario,
    obtener_tarifa,
)
from mente.ensamblador import PromptEnsamblado, cargar_voz, ensamblar_prompt
from mente.modelos import ConfigModelo, FakeLLM, crear_modelo, estrechar_esquema
from mente.tests.test_mente import EJEMPLOS_VALIDOS, MotorSimulado, VOZ_LEAH


def _cargar_ejemplo(nombre: str) -> dict[str, Any]:
    return json.loads((EJEMPLOS_VALIDOS / nombre).read_text(encoding="utf-8"))


class TestTarifasYCache:
    def test_tokens_cache_reducen_el_coste_estimado(self):
        tarifa = obtener_tarifa("claude-sonnet-4-5")
        sin_cache = UsoTokens(tokens_entrada=1200, tokens_cache=0, tokens_salida=200)
        con_cache = UsoTokens(tokens_entrada=300, tokens_cache=900, tokens_salida=200)

        assert sin_cache.tokens_entrada_totales == con_cache.tokens_entrada_totales == 1200
        assert not sin_cache.en_cache
        assert con_cache.en_cache

        coste_sin = calcular_coste_usd(sin_cache, tarifa)
        coste_con = calcular_coste_usd(con_cache, tarifa)
        assert coste_con < coste_sin
        # 1200*3/1e6 + 200*15/1e6 = 0.0066; 300*3/1e6 + 900*0.3/1e6 + 200*15/1e6 = 0.00417
        assert coste_sin == pytest.approx(0.0066)
        assert coste_con == pytest.approx(0.00417)

    def test_fakellm_reutiliza_prefijo_sistema_y_reporta_cache_en_segunda_llamada(self):
        pulso_1 = _cargar_ejemplo("pulso--hito.json")
        pulso_2 = _cargar_ejemplo("pulso--interlocucion.json")
        modelo = FakeLLM(modelo="gpt-4o-mini")

        p1 = ensamblar_prompt(VOZ_LEAH, pulso_1)
        p2 = ensamblar_prompt(VOZ_LEAH, pulso_2)

        modelo.generar(p1, estrechar_esquema(pulso_1))
        uso_1 = modelo.ultimo_uso
        assert uso_1 is not None
        assert uso_1.tokens_cache == 0
        assert not uso_1.en_cache

        modelo.generar(p2, estrechar_esquema(pulso_2))
        uso_2 = modelo.ultimo_uso
        assert uso_2 is not None
        assert uso_2.tokens_cache > 0
        assert uso_2.en_cache
        assert modelo.llamadas[0].en_cache is False
        assert modelo.llamadas[1].en_cache is True

        tarifa = obtener_tarifa("gpt-4o-mini")
        coste_si_sin_cache = calcular_coste_usd(
            UsoTokens(tokens_entrada=uso_2.tokens_entrada_totales, tokens_cache=0, tokens_salida=uso_2.tokens_salida),
            tarifa,
        )
        assert calcular_coste_usd(uso_2, tarifa) < coste_si_sin_cache


class TestRegistroCostesYPresupuesto:
    def test_costes_jsonl_registra_epoca_pulso_modelo_cadenas_y_reintentos(self, tmp_path: Path):
        ruta_costes = tmp_path / "costes.jsonl"
        pulso_ord = {**_cargar_ejemplo("pulso--hito.json"), "id": 10, "epoca": "ep-costes-1"}
        pulso_cad = {
            **_cargar_ejemplo("pulso--hito.json"),
            "id": 11,
            "epoca": "ep-costes-1",
            "hito": {"id": "despertar", "origen": "rutina", "forma": "cadena"},
        }
        pulso_inter = {**_cargar_ejemplo("pulso--interlocucion.json"), "id": 12, "epoca": "ep-costes-1"}

        motor = MotorSimulado(epoca="ep-costes-1")
        motor.pulsos = [pulso_ord, pulso_cad, pulso_inter]

        # Se procesa primero interlocución (id=12), luego ordinario (id=10, con fallo y reintento), luego cadena despertar en 3 pasos (id=11).
        modelo = FakeLLM(
            modelo="claude-sonnet-4-5",
            respuestas=[
                {"pensamiento": "Saludo a Ana junto al lago.", "frase": "Hola, Ana."},
                "{json-roto",
                {"pensamiento": "Vuelvo a casa tras el fallo.", "intencion": {"objetivo": "volver", "porque": "noche", "plan": [{"accion": "ir_a", "lugar": "tu_casa"}]}},
                {"pensamiento": "Repaso lo pendiente de ayer."},
                {"pensamiento": "Observo cómo amanece la mañana."},
                {"pensamiento": "Empiezo el día en calma."},
            ],
        )
        presupuesto = PresupuestoTokens(
            limite_tokens_dia=50_000,
            modelo="claude-sonnet-4-5",
            ruta_costes=ruta_costes,
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH, presupuesto_tokens=presupuesto)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in resultados] == ["aceptada", "aceptada", "aceptada"]
        assert len(modelo.llamadas) == 6

        registros = [json.loads(linea) for linea in ruta_costes.read_text(encoding="utf-8").splitlines()]
        assert len(registros) == 6

        campos_obligatorios = {
            "timestamp_utc",
            "epoca",
            "pulso_id",
            "motivo",
            "es_cadena",
            "modelo",
            "tokens_entrada",
            "tokens_cache",
            "tokens_salida",
            "coste_usd",
            "en_cache",
        }
        for reg in registros:
            assert campos_obligatorios.issubset(reg.keys())
            assert reg["epoca"] == "ep-costes-1"
            assert reg["modelo"] == "claude-sonnet-4-5"
            assert reg["tokens_entrada"] > 0
            assert reg["tokens_salida"] > 0
            assert reg["coste_usd"] > 0

        # Interlocución se procesó primero (id=12), luego ordinario con 1 reintento (id=10 x2), luego cadena despertar (id=11 x3 pasos).
        assert [r["pulso_id"] for r in registros] == [12, 10, 10, 11, 11, 11]
        assert [r["es_cadena"] for r in registros] == [False, False, False, True, True, True]
        assert registros[0]["en_cache"] is False
        assert registros[1]["en_cache"] is True
        assert registros[2]["en_cache"] is True
        assert registros[3]["en_cache"] is True
        assert registros[4]["en_cache"] is True
        assert registros[5]["en_cache"] is True

        resumen = presupuesto.resumen()
        assert resumen["llamadas_totales"] == 6
        assert resumen["llamadas_interlocucion"] == 1
        assert resumen["llamadas_ordinarias"] == 2
        assert resumen["llamadas_cadenas"] == 3
        assert resumen["llamadas_en_cache"] == 5
        assert resumen["tokens_cadenas"] > 0
        assert resumen["tokens_ordinarios"] > 0
        assert resumen["coste_total_usd"] == pytest.approx(
            resumen["coste_ordinario_usd"] + resumen["coste_cadenas_usd"] + resumen["coste_interlocucion_usd"]
        )

    def test_reformulacion_tras_rechazo_motor_y_cadenas_con_presupuesto_separado(self, tmp_path: Path):
        ruta_costes = tmp_path / "costes.jsonl"
        pulso_cad = {
            **_cargar_ejemplo("pulso--hito.json"),
            "id": 20,
            "epoca": "ep-sep",
            "hito": {"id": "acostarse", "origen": "rutina", "forma": "cadena"},
        }
        pulso_ord = {**_cargar_ejemplo("pulso--hito.json"), "id": 21, "epoca": "ep-sep"}

        motor = MotorSimulado(epoca="ep-sep")
        intentos = 0

        def responder_motor(_cuerpo: dict[str, Any]) -> dict[str, Any]:
            nonlocal intentos
            intentos += 1
            if intentos == 2:  # El primer intento del pulso ordinario (id=21) es rechazado por el motor.
                return {"estado": "rechazada", "motivos": [{"codigo": "lugar_ocupado", "campo": "intencion.plan[0]", "prosa": "Hay alguien allí."}]}
            return {"estado": "aceptada"}

        motor.al_recibir_respuesta = responder_motor
        motor.pulsos = [pulso_cad, pulso_ord]

        modelo = FakeLLM(modelo="gemini-2.5-flash")
        presupuesto = PresupuestoTokens(
            limite_tokens_dia=5_000,
            limite_tokens_cadenas=10_000,
            modelo="gemini-2.5-flash",
            ruta_costes=ruta_costes,
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH, presupuesto_tokens=presupuesto)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in resultados] == ["aceptada", "aceptada"]
        registros = [json.loads(l) for l in ruta_costes.read_text(encoding="utf-8").splitlines()]
        # 4 pasos para la cadena acostarse (id=20) + 2 llamadas para el ordinario reformulado (id=21)
        assert [(r["pulso_id"], r["es_cadena"]) for r in registros] == [
            (20, True),
            (20, True),
            (20, True),
            (20, True),
            (21, False),
            (21, False),
        ]
        resumen = presupuesto.resumen()
        assert resumen["llamadas_cadenas"] == 4
        assert resumen["llamadas_ordinarias"] == 2
        assert resumen["tokens_cadenas"] < 10_000
        assert resumen["tokens_ordinarios"] < 5_000
        assert presupuesto.puede_gastar("deriva")

    def test_modelo_sin_ultimo_uso_estima_tokens_y_cache_en_registro_costes(self, tmp_path: Path):
        class ModeloMinimo:
            def generar(self, prompt: PromptEnsamblado, esquema: dict[str, Any]) -> dict[str, Any]:
                return {"pensamiento": "Observo sin telemetría propia."}

        ruta_costes = tmp_path / "costes.jsonl"
        pulso_1 = {**_cargar_ejemplo("pulso--hito.json"), "id": 1, "epoca": "ep-min"}
        pulso_2 = {**_cargar_ejemplo("pulso--hito.json"), "id": 2, "epoca": "ep-min"}
        motor = MotorSimulado(epoca="ep-min")
        motor.pulsos = [pulso_1, pulso_2]

        registro = RegistroCostes(ruta_costes)
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, ModeloMinimo(), VOZ_LEAH, registro_costes=registro)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert len(resultados) == 2
        assert len(registro.entradas) == 2
        assert registro.entradas[0]["en_cache"] is False
        assert registro.entradas[0]["tokens_cache"] == 0
        assert registro.entradas[1]["en_cache"] is True
        assert registro.entradas[1]["tokens_cache"] > 0

    @pytest.mark.parametrize("motivo_no_inter", ["deriva", "hito", "pico", "fin_intencion", "compromiso", "transicion"])
    def test_presupuesto_agotado_bloquea_no_interlocutorios_y_permite_interlocucion(
        self, tmp_path: Path, motivo_no_inter: str
    ):
        ruta_costes = tmp_path / "costes.jsonl"
        pulso_primero = {**_cargar_ejemplo("pulso--hito.json"), "id": 1, "epoca": "ep-lim", "motivo": "deriva"}
        pulso_primero.pop("hito", None)
        pulso_bloqueado = {**_cargar_ejemplo("pulso--hito.json"), "id": 2, "epoca": "ep-lim", "motivo": motivo_no_inter}
        if motivo_no_inter != "hito":
            pulso_bloqueado.pop("hito", None)
        pulso_inter = {**_cargar_ejemplo("pulso--interlocucion.json"), "id": 3, "epoca": "ep-lim"}

        motor = MotorSimulado(epoca="ep-lim")
        motor.pulsos = [pulso_primero]

        modelo = FakeLLM(modelo="gpt-4o-mini")
        telemetria = RegistroTelemetria()
        # Límite muy bajo: tras el primer pulso ordinario queda agotado.
        presupuesto = PresupuestoTokens(
            limite_tokens_dia=500,
            modelo="gpt-4o-mini",
            ruta_costes=ruta_costes,
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(
                cliente,
                modelo,
                VOZ_LEAH,
                telemetria=telemetria,
                presupuesto_tokens=presupuesto,
            )
            res_inicial = ciclo.ejecutar_paso(espera_ms=0)
            assert [r.estado for r in res_inicial] == ["aceptada"]
            assert presupuesto.esta_agotado()
            assert not presupuesto.puede_gastar(motivo_no_inter)
            assert presupuesto.puede_gastar("interlocucion")
            llamadas_tras_primero = len(modelo.llamadas)
            assert llamadas_tras_primero == 1

            # Llegan un pulso no interlocutorio y una interlocución con el presupuesto ya agotado.
            motor.pulsos.extend([pulso_bloqueado, pulso_inter])
            res_siguientes = ciclo.ejecutar_paso(espera_ms=0)

        # Interlocución se procesa primero y llega al modelo; el no interlocutorio se omite sin llamar al LLM.
        assert [(r.pulso_id, r.estado) for r in res_siguientes] == [
            (3, "aceptada"),
            (2, "presupuesto_agotado"),
        ]
        assert len(modelo.llamadas) == llamadas_tras_primero + 1
        assert [r["basada_en"] for r in motor.respuestas_recibidas] == [1, 3]
        assert any(
            e["tipo"] == "pulso_omitido_presupuesto" and e["pulso_id"] == 2 and e["motivo_pulso"] == motivo_no_inter
            for e in telemetria.eventos
        )

        registros = [json.loads(l) for l in ruta_costes.read_text(encoding="utf-8").splitlines()]
        assert [r["pulso_id"] for r in registros] == [1, 3]

    def test_reinicio_automatico_al_cambiar_de_dia_utc(self):
        momento = datetime(2026, 11, 19, 23, 50, tzinfo=timezone.utc)
        presupuesto = PresupuestoTokens(
            limite_tokens_dia=100,
            modelo="gpt-4o-mini",
            reloj_utc=lambda: momento,
        )
        presupuesto.registrar_llamada(
            epoca="ep-1",
            pulso_id=1,
            motivo="deriva",
            uso=UsoTokens(tokens_entrada=80, tokens_cache=0, tokens_salida=40),
        )
        assert not presupuesto.puede_gastar("deriva")

        momento = momento + timedelta(minutes=15)
        assert presupuesto.puede_gastar("deriva")
        assert presupuesto.tokens_ordinarios == 0

    def test_cadenas_computan_contra_limite_diario_cuando_no_hay_cupo_separado(self):
        momento = datetime(2026, 11, 21, 10, 0, tzinfo=timezone.utc)
        presupuesto = PresupuestoTokens(
            limite_tokens_dia=100,
            modelo="gpt-4o-mini",
            reloj_utc=lambda: momento,
        )

        # Una llamada de cadena de 100 tokens impide admitir la siguiente cadena u ordinaria ese día.
        presupuesto.registrar_llamada(
            epoca="ep-cad",
            pulso_id=1,
            motivo="hito",
            es_cadena=True,
            uso=UsoTokens(tokens_entrada=80, tokens_cache=0, tokens_salida=20),
        )
        assert presupuesto.tokens_cadenas == 100
        assert not presupuesto.puede_gastar("hito", es_cadena=True)
        assert not presupuesto.puede_gastar("deriva", es_cadena=False)
        assert presupuesto.puede_gastar("interlocucion")

        # Gasto mixto (60 ordinarios + 40 cadena = 100) también agota el tope diario compartido.
        presupuesto_mixto = PresupuestoTokens(
            limite_tokens_dia=100,
            modelo="gpt-4o-mini",
            reloj_utc=lambda: momento,
        )
        presupuesto_mixto.registrar_llamada(
            epoca="ep-mix",
            pulso_id=1,
            motivo="deriva",
            es_cadena=False,
            uso=UsoTokens(tokens_entrada=50, tokens_cache=0, tokens_salida=10),
        )
        assert presupuesto_mixto.puede_gastar("hito", es_cadena=True)
        presupuesto_mixto.registrar_llamada(
            epoca="ep-mix",
            pulso_id=2,
            motivo="hito",
            es_cadena=True,
            uso=UsoTokens(tokens_entrada=30, tokens_cache=0, tokens_salida=10),
        )
        assert not presupuesto_mixto.puede_gastar("hito", es_cadena=True)
        assert not presupuesto_mixto.puede_gastar("deriva", es_cadena=False)
        assert presupuesto_mixto.puede_gastar("interlocucion")

        # Con cupo de cadenas separado, cada contador respeta su propio límite.
        presupuesto_sep = PresupuestoTokens(
            limite_tokens_dia=100,
            limite_tokens_cadenas=200,
            modelo="gpt-4o-mini",
            reloj_utc=lambda: momento,
        )
        presupuesto_sep.registrar_llamada(
            epoca="ep-sep",
            pulso_id=1,
            motivo="hito",
            es_cadena=True,
            uso=UsoTokens(tokens_entrada=120, tokens_cache=0, tokens_salida=30),
        )
        assert presupuesto_sep.puede_gastar("hito", es_cadena=True)
        assert presupuesto_sep.puede_gastar("deriva", es_cadena=False)
        presupuesto_sep.registrar_llamada(
            epoca="ep-sep",
            pulso_id=2,
            motivo="hito",
            es_cadena=True,
            uso=UsoTokens(tokens_entrada=40, tokens_cache=0, tokens_salida=10),
        )
        assert not presupuesto_sep.puede_gastar("hito", es_cadena=True)
        assert presupuesto_sep.puede_gastar("deriva", es_cadena=False)

        # Reinicio al cambiar de día UTC libera ambos cupos.
        momento = momento + timedelta(days=1)
        assert presupuesto.puede_gastar("hito", es_cadena=True)
        assert presupuesto.tokens_cadenas == 0
        assert presupuesto_mixto.puede_gastar("deriva", es_cadena=False)


class TestInformeCosteDiario:
    def test_estimar_coste_diario_vanilla_y_pelican(self):
        informe = generar_informe_coste_diario(modelo="claude-sonnet-4-5")
        vanilla = informe["vanilla"]
        pelican = informe["pelican"]

        assert vanilla.duracion_real_min == 14.0
        assert pelican.duracion_real_min == 1440.0
        assert vanilla.llamadas_cadenas == 8
        assert pelican.llamadas_cadenas == 8
        assert vanilla.coste_usd < vanilla.coste_sin_cache_usd
        assert pelican.coste_usd < pelican.coste_sin_cache_usd
        assert vanilla.ahorro_cache_usd > 0
        # En tiempo real, vanilla consume muchos más días de juego por hora real que Pelican 1:1.
        assert vanilla.coste_por_hora_real_usd > pelican.coste_por_hora_real_usd

        tabla = formatear_informe_coste_diario(informe)
        assert "| vanilla |" in tabla
        assert "| pelican |" in tabla
        assert "`claude-sonnet-4-5`" in tabla

    def test_estimar_coste_diario_tarifa_personalizada_y_validaciones(self):
        tarifa = TarifaModelo("modelo-prueba", entrada_por_1m=2.0, cache_entrada_por_1m=0.2, salida_por_1m=8.0)
        est = estimar_coste_diario("pelican", tarifa=tarifa, tasa_cache=0.5)
        assert est.modelo == "modelo-prueba"
        assert est.coste_usd > 0

        with pytest.raises(ValueError):
            estimar_coste_diario("invalido")
        with pytest.raises(KeyError):
            estimar_coste_diario("vanilla", modelo="modelo-inexistente")

    def test_crear_modelo_propaga_nombre_modelo_al_fakellm(self):
        modelo = crear_modelo(ConfigModelo(proveedor="fake", modelo="gemini-2.5-flash"))
        assert isinstance(modelo, FakeLLM)
        assert modelo.modelo == "gemini-2.5-flash"
