"""Pruebas de la mente sin cuerpo: esquema estrechado, ensamblador, FakeLLM y cliente HTTP (CP-204)."""

from __future__ import annotations

import json
import threading
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from typing import Any
from urllib.parse import parse_qs, urlparse

import httpx
import pytest
from jsonschema import Draft202012Validator

from mente import contrato
from mente.cliente_motor import (
    AlmacenCursor,
    CicloMente,
    ClienteMotor,
    ConflictoEpocaError,
    CursorMente,
    RegistroTelemetria,
)
from mente.ensamblador import cargar_voz, ensamblar_prompt
from mente.modelos import (
    ConfigModelo,
    ErrorModeloError,
    FakeLLM,
    PresupuestoTokensAgotadoError,
    crear_modelo,
    estrechar_esquema,
    validar_respuesta_estrechada,
)

RAIZ = Path(__file__).resolve().parent.parent.parent
EJEMPLOS_VALIDOS = contrato.DIR_ESQUEMAS / "ejemplos" / "validos"
VOZ_LEAH = cargar_voz(RAIZ / "CorazonPelican" / "assets" / "personajes" / "leah.voz.md")


def _cargar_ejemplo(nombre: str) -> dict[str, Any]:
    return json.loads((EJEMPLOS_VALIDOS / nombre).read_text(encoding="utf-8"))


class MotorSimulado:
    """Servidor lógico en memoria para probar el cliente HTTP mediante `httpx.MockTransport`."""

    def __init__(self, epoca: str = "ep-001", token: str | None = None) -> None:
        self.epoca = epoca
        self.mundo_cargado = True
        self.token = token
        self.pulsos: list[dict[str, Any]] = []
        self.respuestas_recibidas: list[dict[str, Any]] = []
        self.peticiones_pulsos: list[dict[str, Any]] = []
        self.memoria_por_personaje: dict[str, dict[str, Any]] = {}
        self.al_recibir_respuesta: Any = None

    def nueva_epoca(self, epoca: str, pulsos: list[dict[str, Any]] | None = None) -> None:
        self.epoca = epoca
        self.pulsos = list(pulsos or [])

    def handler(self, request: httpx.Request) -> httpx.Response:
        if self.token and request.headers.get("Authorization") != f"Bearer {self.token}":
            return httpx.Response(401, json={"error": "No autorizado"})

        ruta = request.url.path
        if ruta == "/api/v1/salud" and request.method == "GET":
            cuerpo: dict[str, Any] = {
                "version": "0.1.0",
                "contrato": "v1",
                "mundo_cargado": self.mundo_cargado,
                "personajes": ["leah"],
            }
            if self.mundo_cargado:
                cuerpo["epoca"] = self.epoca
            return httpx.Response(200, json=cuerpo)

        if ruta == "/api/v1/pulsos" and request.method == "GET":
            epoca_q = request.url.params.get("epoca")
            since_q = int(request.url.params.get("since", "0"))
            espera_q = int(request.url.params.get("espera_ms", "0"))
            self.peticiones_pulsos.append({"epoca": epoca_q, "since": since_q, "espera_ms": espera_q})
            if not self.mundo_cargado or epoca_q != self.epoca:
                return httpx.Response(
                    409,
                    json={
                        "error": "epoca_obsoleta" if self.mundo_cargado else "mundo_no_cargado",
                        "epoca": self.epoca if self.mundo_cargado else None,
                        "mundo_cargado": self.mundo_cargado,
                    },
                )
            pendientes = [p for p in self.pulsos if p["id"] > since_q]
            return httpx.Response(200, json={"epoca": self.epoca, "pulsos": pendientes})

        if ruta == "/api/v1/respuestas" and request.method == "POST":
            cuerpo = json.loads(request.content.decode("utf-8"))
            self.respuestas_recibidas.append(cuerpo)
            if self.al_recibir_respuesta is not None:
                return httpx.Response(200, json=self.al_recibir_respuesta(cuerpo))
            return httpx.Response(200, json={"estado": "aceptada"})

        if ruta.startswith("/api/v1/memoria/") and request.method == "GET":
            personaje = ruta.rsplit("/", 1)[-1]
            mem = self.memoria_por_personaje.get(
                personaje,
                {
                    "epoca": self.epoca,
                    "personaje": personaje,
                    "diario": [],
                    "recuerdos": [],
                    "relaciones": {},
                    "compromisos": [],
                    "portafolio": [],
                },
            )
            return httpx.Response(200, json=mem)

        return httpx.Response(404, json={"error": "No encontrado"})


class TestEsquemaEstrechado:
    @pytest.mark.parametrize(
        "archivo_pulso",
        [
            "pulso--interlocucion.json",
            "pulso--hito.json",
            "pulso--diario-con-origen.json",
        ],
    )
    def test_esquema_estrechado_es_draft_2020_12_valido_y_pensamiento_es_primero(self, archivo_pulso):
        pulso = _cargar_ejemplo(archivo_pulso)
        esquema = estrechar_esquema(pulso)
        Draft202012Validator.check_schema(esquema)
        assert list(esquema["properties"])[0] == "pensamiento"

    def test_estrechar_esquema_valida_como_subconjunto_del_general(self):
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        esquema = estrechar_esquema(pulso)

        respuesta_valida = {
            "pensamiento": "Ana pide el lago, aunque ya cae la tarde.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "intencion": {
                "objetivo": "levantar la vista y saludar a Ana",
                "porque": "acaba de acercarse a la pasarela",
                "plan": [
                    {"accion": "levantar_la_vista"},
                    {"accion": "gesto", "tipo": "saludo"},
                    {"accion": "dibujar", "obra": "leah-0030"},
                ],
            },
            "frase": "La luz ya se va, Ana. Mañana con calma.",
            "actos": [
                {
                    "acto": "promete",
                    "obra": "nueva",
                    "tema": "lago en otoño",
                    "para": "farmer:123",
                    "cuando": {"dia": 69, "hora": 1900},
                },
                {
                    "acto": "cita",
                    "lugar": "pasarela_lago",
                    "para": "farmer:123",
                    "cuando": {"dia": 69, "hora": 1900},
                },
            ],
        }
        assert validar_respuesta_estrechada(pulso, respuesta_valida, esquema) == []
        assert contrato.errores("respuesta", respuesta_valida) == []

    def test_rechaza_lugar_y_ref_que_no_estan_en_el_pulso(self):
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        pulso_con_ir_a = {
            **pulso,
            "acciones_disponibles": [*pulso["acciones_disponibles"], "ir_a", "mirar"],
        }
        esquema = estrechar_esquema(pulso_con_ir_a)

        con_lugar_inventado = {
            "pensamiento": "Quiero ir a un muelle inventado.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "intencion": {
                "objetivo": "ir a un lugar que no existe",
                "porque": "prueba de guarda",
                "plan": [{"accion": "ir_a", "lugar": "muelle_inventado"}],
            },
        }
        assert validar_respuesta_estrechada(pulso_con_ir_a, con_lugar_inventado, esquema) != []

        con_cita_lugar_inventado = {
            "pensamiento": "Cito en un lugar ajeno.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "actos": [
                {
                    "acto": "cita",
                    "lugar": "cueva_secreta",
                    "para": "farmer:123",
                    "cuando": {"dia": 69, "hora": 1900},
                }
            ],
        }
        assert validar_respuesta_estrechada(pulso_con_ir_a, con_cita_lugar_inventado, esquema) != []

        con_ref_inventada = {
            "pensamiento": "Miro a alguien que no está.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "intencion": {
                "objetivo": "mirar a un desconocido",
                "porque": "prueba de guarda",
                "plan": [{"accion": "mirar", "objetivo": "npc:Wizard"}],
            },
        }
        assert validar_respuesta_estrechada(pulso_con_ir_a, con_ref_inventada, esquema) != []

        con_acto_para_inventado = {
            "pensamiento": "Prometo a quien no está en el pulso.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "actos": [
                {
                    "acto": "promete",
                    "obra": "nueva",
                    "tema": "el lago",
                    "para": "farmer:999",
                    "cuando": {"dia": 69, "hora": 1900},
                }
            ],
        }
        assert validar_respuesta_estrechada(pulso_con_ir_a, con_acto_para_inventado, esquema) != []

    def test_cierra_acciones_hitos_obras_y_episodios_diario(self):
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        esquema = estrechar_esquema(pulso)

        accion_no_ofrecida = {
            "pensamiento": "Me voy a esperar aunque no estaba en la lista.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "intencion": {
                "objetivo": "esperar",
                "porque": "prueba",
                "plan": [{"accion": "esperar", "duracion_min": 20}],
            },
        }
        assert validar_respuesta_estrechada(pulso, accion_no_ofrecida, esquema) != []

        hito_inexistente = {
            "pensamiento": "Cambio un hito que no existe.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "intencion": {
                "objetivo": "saltar algo inexistente",
                "porque": "prueba",
                "plan": [{"accion": "guardar_cuaderno"}],
                "cambios_rutina": [{"hito": "fall:2350", "cambio": "saltar"}],
            },
        }
        assert validar_respuesta_estrechada(pulso, hito_inexistente, esquema) != []

        obra_inexistente = {
            "pensamiento": "Enseño una obra ajena.",
            "epoca": pulso["epoca"],
            "basada_en": pulso["id"],
            "actos": [{"acto": "ensena", "obra_id": "leah-9999"}],
        }
        assert validar_respuesta_estrechada(pulso, obra_inexistente, esquema) != []

        pulso_diario = _cargar_ejemplo("pulso--diario-con-origen.json")
        resp_diario_valida = _cargar_ejemplo("respuesta--reflexion-con-origen.json")
        assert validar_respuesta_estrechada(pulso_diario, resp_diario_valida) == []

        resp_diario_ajena = {
            "pensamiento": "Invento un episodio.",
            "epoca": pulso_diario["epoca"],
            "basada_en": pulso_diario["id"],
            "memoria": {"reflexiones": [{"texto": "Algo inventado.", "origen_diario": ["ep-999"]}]},
        }
        assert validar_respuesta_estrechada(pulso_diario, resp_diario_ajena) != []


class TestEnsambladorYModelos:
    def test_prefijo_sistema_identico_byte_a_byte_entre_pulsos(self):
        pulso_a = _cargar_ejemplo("pulso--interlocucion.json")
        pulso_b = _cargar_ejemplo("pulso--hito.json")

        prompt_a = ensamblar_prompt(VOZ_LEAH, pulso_a, recuerdos=["Ayer llovió en el lago."])
        prompt_b = ensamblar_prompt(
            VOZ_LEAH,
            pulso_b,
            motivos_rechazo=[{"codigo": "lugar_ocupado", "campo": "plan[0]", "prosa": "Hay alguien en la pasarela"}],
        )

        assert prompt_a.sistema.encode("utf-8") == prompt_b.sistema.encode("utf-8")
        assert prompt_a.usuario != prompt_b.usuario
        assert "Es última hora de la tarde de otoño" in prompt_a.usuario
        assert "Ayer llovió en el lago." in prompt_a.usuario
        assert "Hay alguien en la pasarela" in prompt_b.usuario
        assert "comp-12" in prompt_b.usuario

    def test_enrutado_configuracion_sin_claves_reales(self, monkeypatch):
        monkeypatch.delenv("OPENAI_API_KEY", raising=False)
        modelo_fake = crear_modelo(ConfigModelo(proveedor="fake", modelo="fake-mente"))
        assert isinstance(modelo_fake, FakeLLM)

        modelo_remoto = crear_modelo(ConfigModelo(proveedor="openai", modelo="gpt-4o"))
        prompt = ensamblar_prompt(VOZ_LEAH, _cargar_ejemplo("pulso--hito.json"))
        with pytest.raises(ErrorModeloError, match="OPENAI_API_KEY"):
            modelo_remoto.generar(prompt, estrechar_esquema(_cargar_ejemplo("pulso--hito.json")))

    def test_presupuesto_tokens_limita_llamadas(self):
        pulso = _cargar_ejemplo("pulso--hito.json")
        prompt = ensamblar_prompt(VOZ_LEAH, pulso)
        esquema = estrechar_esquema(pulso)
        modelo = FakeLLM(presupuesto_tokens_dia=10)
        with pytest.raises(PresupuestoTokensAgotadoError):
            modelo.generar(prompt, esquema)


class TestCicloMente:
    def test_fakellm_produce_respuesta_valida_y_registra_pensamiento(self):
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        motor = MotorSimulado(epoca=pulso["epoca"])
        motor.pulsos = [pulso]

        modelo = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Ana pregunta por el lago, pero ya casi no queda luz.",
                    "frase": "Ahora mismo la luz se ha ido. Mañana al caer la tarde.",
                    "actos": [
                        {
                            "acto": "promete",
                            "obra": "nueva",
                            "tema": "el lago al atardecer",
                            "para": "farmer:123",
                            "cuando": {"dia": 69, "hora": 1900},
                        }
                    ],
                }
            ]
        )
        telemetria = RegistroTelemetria()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH, telemetria=telemetria)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert len(resultados) == 1
        res = resultados[0]
        assert res.estado == "aceptada"
        assert res.respuesta is not None
        assert res.respuesta["epoca"] == pulso["epoca"]
        assert res.respuesta["basada_en"] == pulso["id"]
        assert list(res.respuesta)[0] == "pensamiento"
        assert contrato.errores("respuesta", res.respuesta) == []
        assert validar_respuesta_estrechada(pulso, res.respuesta) == []
        assert len(motor.respuestas_recibidas) == 1
        assert telemetria.eventos[-1]["tipo"] == "pensamiento_aceptado"
        assert telemetria.eventos[-1]["pensamiento"] == res.respuesta["pensamiento"]

    def test_salida_invalida_provoca_un_reintento_y_luego_rechazo_sin_enviar_al_motor(self):
        pulso = _cargar_ejemplo("pulso--hito.json")
        motor = MotorSimulado(epoca=pulso["epoca"])
        motor.pulsos = [pulso]

        # Dos salidas inválidas seguidas: primero JSON roto, luego lugar inventado.
        modelo = FakeLLM(
            respuestas=[
                "{no es json",
                {
                    "pensamiento": "Intento ir a un sitio que no existe.",
                    "intencion": {
                        "objetivo": "ir a un lugar ajeno",
                        "porque": "prueba",
                        "plan": [{"accion": "ir_a", "lugar": "taberna_inventada"}],
                    },
                },
            ]
        )
        telemetria = RegistroTelemetria()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH, telemetria=telemetria)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert len(resultados) == 1
        res = resultados[0]
        assert res.estado == "rechazada_local"
        assert res.intentos_modelo == 2
        assert len(modelo.llamadas) == 2
        assert motor.respuestas_recibidas == []
        assert [e["tipo"] for e in telemetria.eventos] == ["reintento_local", "rechazo_local"]
        assert telemetria.eventos[-1]["pensamiento"] == "Intento ir a un sitio que no existe."

    def test_salida_invalida_se_recupera_en_el_reintento(self):
        pulso = _cargar_ejemplo("pulso--hito.json")
        motor = MotorSimulado(epoca=pulso["epoca"])
        motor.pulsos = [pulso]

        modelo = FakeLLM(
            respuestas=[
                {"pensamiento": "Falta lugar en ir_a.", "intencion": {"objetivo": "ir", "porque": "p", "plan": [{"accion": "ir_a"}]}},
                {
                    "pensamiento": "Ya es hora de volver a casa.",
                    "intencion": {
                        "objetivo": "volver a casa",
                        "porque": "cae la noche",
                        "plan": [{"accion": "ir_a", "lugar": "tu_casa"}],
                    },
                },
            ]
        )
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert resultados[0].estado == "aceptada"
        assert resultados[0].intentos_modelo == 2
        assert len(motor.respuestas_recibidas) == 1

    def test_tras_rechazo_del_motor_el_prompt_de_reformulacion_contiene_la_prosa_del_motivo(self):
        pulso = _cargar_ejemplo("pulso--hito.json")
        motor = MotorSimulado(epoca=pulso["epoca"])
        motor.pulsos = [pulso]
        prosa_motivo = "no puedes sentarte en la pasarela: hay alguien allí ahora mismo"

        intentos_envio = 0

        def responder_motor(_cuerpo: dict[str, Any]) -> dict[str, Any]:
            nonlocal intentos_envio
            intentos_envio += 1
            if intentos_envio == 1:
                return {
                    "estado": "rechazada",
                    "motivos": [
                        {
                            "codigo": "lugar_ocupado",
                            "campo": "intencion.plan[0]",
                            "prosa": prosa_motivo,
                        }
                    ],
                }
            return {"estado": "aceptada"}

        motor.al_recibir_respuesta = responder_motor
        modelo = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Me paso un momento por la pasarela.",
                    "intencion": {
                        "objetivo": "ir a la pasarela",
                        "porque": "ver el agua",
                        "plan": [{"accion": "ir_a", "lugar": "pasarela_lago"}],
                    },
                },
                {
                    "pensamiento": "Si hay alguien en la pasarela, mejor vuelvo a casa.",
                    "intencion": {
                        "objetivo": "volver a casa",
                        "porque": "la pasarela está ocupada",
                        "plan": [{"accion": "ir_a", "lugar": "tu_casa"}],
                    },
                },
            ]
        )
        telemetria = RegistroTelemetria()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH, telemetria=telemetria)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert len(resultados) == 1
        assert resultados[0].estado == "aceptada"
        assert len(modelo.llamadas) == 2
        assert prosa_motivo in modelo.llamadas[1].prompt.usuario
        assert [e["tipo"] for e in telemetria.eventos] == ["rechazo_motor", "pensamiento_aceptado"]

    def test_reinicio_retoma_cursor_sin_reprocesar_y_epoca_nueva_reinicia_a_cero(self, tmp_path):
        ruta_cursor = tmp_path / "cursor.json"
        pulso_1 = {**_cargar_ejemplo("pulso--hito.json"), "id": 1, "epoca": "ep-1"}
        pulso_2 = {**_cargar_ejemplo("pulso--hito.json"), "id": 2, "epoca": "ep-1"}
        pulso_3 = {**_cargar_ejemplo("pulso--hito.json"), "id": 3, "epoca": "ep-1"}

        motor = MotorSimulado(epoca="ep-1")
        motor.pulsos = [pulso_1, pulso_2]

        modelo_1 = FakeLLM()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo_1 = CicloMente(cliente, modelo_1, VOZ_LEAH, almacen_cursor=AlmacenCursor(ruta_cursor))
            ciclo_1.cache_memoria["leah"] = {"recuerdos": ["caché vieja"]}
            res_1 = ciclo_1.ejecutar_paso(espera_ms=0)
            assert [r.pulso_id for r in res_1] == [1, 2]
            assert ciclo_1.cursor == CursorMente(epoca="ep-1", since=2)

        # Simula reinicio de proceso: nuevo CicloMente con el mismo fichero de cursor.
        motor.pulsos.append(pulso_3)
        modelo_2 = FakeLLM()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo_2 = CicloMente(cliente, modelo_2, VOZ_LEAH, almacen_cursor=AlmacenCursor(ruta_cursor))
            assert ciclo_2.cursor == CursorMente(epoca="ep-1", since=2)
            res_2 = ciclo_2.ejecutar_paso(espera_ms=0)
            assert [r.pulso_id for r in res_2] == [3]
            assert len(modelo_2.llamadas) == 1
            assert motor.peticiones_pulsos[-1]["since"] == 2

            # Cambia la época del motor (recarga de partida): debe reiniciar el cursor a 0 y descartar caché.
            pulso_ep2 = {**_cargar_ejemplo("pulso--hito.json"), "id": 1, "epoca": "ep-2"}
            motor.nueva_epoca("ep-2", [pulso_ep2])
            ciclo_2.cache_memoria["leah"] = {"recuerdos": ["debe borrarse"]}

            # Probamos además la vía 409 directa saltando el chequeo de salud previo.
            res_3 = ciclo_2.ejecutar_paso(espera_ms=0, verificar_salud=False)
            assert [r.pulso_id for r in res_3] == [1]
            assert res_3[0].epoca == "ep-2"
            assert ciclo_2.cursor == CursorMente(epoca="ep-2", since=1)
            assert ciclo_2.cache_memoria == {}
            assert motor.peticiones_pulsos[-1] == {"epoca": "ep-2", "since": 0, "espera_ms": 0}

        # Nueva época detectada por GET /salud != cursor.epoca.
        motor.nueva_epoca("ep-3", [{**_cargar_ejemplo("pulso--hito.json"), "id": 1, "epoca": "ep-3"}])
        modelo_3 = FakeLLM()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo_3 = CicloMente(cliente, modelo_3, VOZ_LEAH, almacen_cursor=AlmacenCursor(ruta_cursor))
            ciclo_3.cache_memoria["leah"] = {"recuerdos": ["debe borrarse por salud"]}
            res_4 = ciclo_3.ejecutar_paso(espera_ms=0, verificar_salud=True)
            assert ciclo_3.cache_memoria == {}
            assert ciclo_3.cursor == CursorMente(epoca="ep-3", since=1)
            assert [r.epoca for r in res_4] == ["ep-3"]

    def test_interlocucion_se_procesa_antes_que_otros_pulsos(self):
        pulso_hito = {**_cargar_ejemplo("pulso--hito.json"), "id": 10, "epoca": "ep-1"}
        pulso_inter = {**_cargar_ejemplo("pulso--interlocucion.json"), "id": 11, "epoca": "ep-1"}

        motor = MotorSimulado(epoca="ep-1")
        motor.pulsos = [pulso_hito, pulso_inter]

        modelo = FakeLLM()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.pulso_id for r in resultados] == [11, 10]
        assert [r["basada_en"] for r in motor.respuestas_recibidas] == [11, 10]

    def test_cliente_envia_token_y_consulta_memoria_y_409(self):
        motor = MotorSimulado(epoca="ep-actual", token="secreto-123")
        motor.memoria_por_personaje["leah"] = {
            "epoca": "ep-actual",
            "personaje": "leah",
            "diario": [],
            "recuerdos": [{"id": "rec-1", "texto": "Ayer probé un corte nuevo.", "importancia": 0.7, "origen_diario": ["ep-1"]}],
            "relaciones": {},
            "compromisos": [],
            "portafolio": [],
        }

        with ClienteMotor(token="secreto-123", transport=httpx.MockTransport(motor.handler)) as cliente:
            mem = cliente.obtener_memoria("leah")
            assert mem["personaje"] == "leah"
            assert len(mem["recuerdos"]) == 1
            with pytest.raises(ConflictoEpocaError) as exc:
                cliente.obtener_pulsos(epoca="ep-vieja", since=4, espera_ms=0)
            assert exc.value.epoca_actual == "ep-actual"
            assert exc.value.mundo_cargado is True


class TestHumoLoopbackHttp:
    def test_humo_httpx_contra_servidor_loopback_real(self, tmp_path):
        pulso = {**_cargar_ejemplo("pulso--interlocucion.json"), "id": 1, "epoca": "ep-loopback"}
        recibidas: list[dict[str, Any]] = []

        class Manejador(BaseHTTPRequestHandler):
            def log_message(self, format: str, *args: Any) -> None:  # noqa: A002
                return

            def _enviar_json(self, codigo: int, carga: dict[str, Any]) -> None:
                cuerpo = json.dumps(carga).encode("utf-8")
                self.send_response(codigo)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(cuerpo)))
                self.end_headers()
                self.wfile.write(cuerpo)

            def do_GET(self) -> None:  # noqa: N802
                parsed = urlparse(self.path)
                if parsed.path == "/api/v1/salud":
                    self._enviar_json(
                        200,
                        {"version": "0.1.0", "contrato": "v1", "mundo_cargado": True, "epoca": "ep-loopback", "personajes": ["leah"]},
                    )
                    return
                if parsed.path == "/api/v1/pulsos":
                    qs = parse_qs(parsed.query)
                    since = int(qs.get("since", ["0"])[0])
                    pulsos = [pulso] if pulso["id"] > since else []
                    self._enviar_json(200, {"epoca": "ep-loopback", "pulsos": pulsos})
                    return
                self._enviar_json(404, {"error": "no_encontrado"})

            def do_POST(self) -> None:  # noqa: N802
                if self.path == "/api/v1/respuestas":
                    largo = int(self.headers.get("Content-Length", "0"))
                    datos = json.loads(self.rfile.read(largo).decode("utf-8"))
                    recibidas.append(datos)
                    self._enviar_json(200, {"estado": "aceptada"})
                    return
                self._enviar_json(404, {"error": "no_encontrado"})

        servidor = ThreadingHTTPServer(("127.0.0.1", 0), Manejador)
        puerto = servidor.server_address[1]
        hilo = threading.Thread(target=servidor.serve_forever, daemon=True)
        hilo.start()
        try:
            ruta_cursor = tmp_path / "cursor.json"
            ruta_telemetria = tmp_path / "pensamientos.jsonl"
            modelo = FakeLLM(
                respuestas=[
                    {
                        "pensamiento": "Ana espera una respuesta sobre el lago.",
                        "frase": "Mañana al atardecer lo intento, cuando baje la luz.",
                    }
                ]
            )
            with ClienteMotor(puerto=puerto) as cliente:
                ciclo = CicloMente(
                    cliente,
                    modelo,
                    VOZ_LEAH,
                    almacen_cursor=AlmacenCursor(ruta_cursor),
                    telemetria=RegistroTelemetria(ruta_telemetria),
                )
                resultados = ciclo.ejecutar_paso(espera_ms=0)
        finally:
            servidor.shutdown()
            servidor.server_close()
            hilo.join(timeout=2)

        assert len(resultados) == 1
        assert resultados[0].estado == "aceptada"
        assert len(recibidas) == 1
        assert contrato.errores("respuesta", recibidas[0]) == []
        assert json.loads(ruta_cursor.read_text(encoding="utf-8")) == {"epoca": "ep-loopback", "since": 1}
        lineas_log = [json.loads(l) for l in ruta_telemetria.read_text(encoding="utf-8").splitlines()]
        assert len(lineas_log) == 1
        assert lineas_log[0]["pensamiento"] == "Ana espera una respuesta sobre el lago."
