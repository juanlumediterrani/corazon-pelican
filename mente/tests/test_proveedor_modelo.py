"""Pruebas sin red de los transportes HTTP de proveedor de modelos conectados a CicloMente (CP-215)."""

from __future__ import annotations

import json
from typing import Any

import httpx

from mente.cliente_motor import CicloMente, ClienteMotor, RegistroTelemetria
from mente.costes import PresupuestoTokens
from mente.modelos import ConfigModelo, crear_modelo
from mente.tests.test_mente import EJEMPLOS_VALIDOS, MotorSimulado, VOZ_LEAH


def _cargar_ejemplo(nombre: str) -> dict[str, Any]:
    return json.loads((EJEMPLOS_VALIDOS / nombre).read_text(encoding="utf-8"))


class TestProveedoresHttpSimulados:
    def test_openai_envia_voz_escena_y_esquema_estrechado_y_ciclo_acepta_respuesta(self, monkeypatch):
        monkeypatch.setenv("OPENAI_API_KEY", "clave-ficticia-openai")
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        motor = MotorSimulado(epoca=pulso["epoca"])
        motor.pulsos = [pulso]

        peticiones: list[dict[str, Any]] = []

        def proveedor_handler(request: httpx.Request) -> httpx.Response:
            cuerpo = json.loads(request.content.decode("utf-8"))
            peticiones.append(
                {
                    "url": str(request.url),
                    "auth": request.headers.get("Authorization"),
                    "cuerpo": cuerpo,
                }
            )
            respuesta_modelo = {
                "pensamiento": "Ana pide un boceto del lago, pero el sol ya cae.",
                "frase": "Ahora la luz se va, Ana. Mañana por la tarde lo intentamos.",
                "actos": [
                    {
                        "acto": "cita",
                        "lugar": "pasarela_lago",
                        "para": "farmer:123",
                        "cuando": {"dia": 69, "hora": 1800},
                    }
                ],
            }
            return httpx.Response(
                200,
                json={
                    "choices": [{"message": {"content": json.dumps(respuesta_modelo, ensure_ascii=False)}}],
                    "usage": {
                        "prompt_tokens": 950,
                        "prompt_tokens_details": {"cached_tokens": 700},
                        "completion_tokens": 45,
                    },
                },
            )

        modelo = crear_modelo(
            ConfigModelo(proveedor="openai", modelo="gpt-4o-mini"),
            transport=httpx.MockTransport(proveedor_handler),
        )
        presupuesto = PresupuestoTokens(limite_tokens_dia=20_000, modelo="gpt-4o-mini")

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH, presupuesto_tokens=presupuesto)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in resultados] == ["aceptada"]
        assert len(peticiones) == 1
        req = peticiones[0]
        assert req["url"].endswith("/chat/completions")
        assert req["auth"] == "Bearer clave-ficticia-openai"
        mensajes = req["cuerpo"]["messages"]
        assert "## Voz del personaje" in mensajes[0]["content"]
        assert "Es última hora de la tarde de otoño" in mensajes[1]["content"]
        esquema_enviado = req["cuerpo"]["response_format"]["json_schema"]["schema"]
        assert esquema_enviado["$defs"]["idLexico"]["enum"] == ["pasarela_lago", "orilla_norte", "tu_casa"]
        assert presupuesto.llamadas_interlocucion == 1
        assert presupuesto.tokens_cache == 700
        assert len(motor.respuestas_recibidas) == 1

    def test_anthropic_y_gemini_conectan_con_ciclo_sin_claves_reales(self, monkeypatch):
        monkeypatch.setenv("ANTHROPIC_API_KEY", "clave-ficticia-anthropic")
        monkeypatch.setenv("GEMINI_API_KEY", "clave-ficticia-gemini")
        pulso = {**_cargar_ejemplo("pulso--hito.json"), "id": 12, "epoca": "ep-multi-prov"}

        def anthropic_handler(request: httpx.Request) -> httpx.Response:
            cuerpo = json.loads(request.content.decode("utf-8"))
            assert request.headers.get("x-api-key") == "clave-ficticia-anthropic"
            assert cuerpo["system"][0]["cache_control"] == {"type": "ephemeral"}
            assert cuerpo["tools"][0]["name"] == "responder_pulso"
            assert "tu_casa" in cuerpo["tools"][0]["input_schema"]["$defs"]["idLexico"]["enum"]
            return httpx.Response(
                200,
                json={
                    "content": [
                        {
                            "type": "tool_use",
                            "name": "responder_pulso",
                            "input": {"pensamiento": "Vuelvo despacio antes de que cierre la noche."},
                        }
                    ],
                    "usage": {"input_tokens": 120, "cache_read_input_tokens": 800, "output_tokens": 25},
                },
            )

        motor_ant = MotorSimulado(epoca="ep-multi-prov")
        motor_ant.pulsos = [pulso]
        modelo_ant = crear_modelo(
            ConfigModelo(proveedor="anthropic", modelo="claude-sonnet-4-5"),
            transport=httpx.MockTransport(anthropic_handler),
        )
        with ClienteMotor(transport=httpx.MockTransport(motor_ant.handler)) as cliente_ant:
            res_ant = CicloMente(cliente_ant, modelo_ant, VOZ_LEAH).ejecutar_paso(espera_ms=0)
        assert [r.estado for r in res_ant] == ["aceptada"]
        assert modelo_ant.ultimo_uso.tokens_cache == 800

        def gemini_handler(request: httpx.Request) -> httpx.Response:
            cuerpo = json.loads(request.content.decode("utf-8"))
            assert request.headers.get("x-goog-api-key") == "clave-ficticia-gemini"
            assert "## Voz del personaje" in cuerpo["systemInstruction"]["parts"][0]["text"]
            assert "responseJsonSchema" in cuerpo["generationConfig"]
            return httpx.Response(
                200,
                json={
                    "candidates": [
                        {
                            "content": {
                                "parts": [
                                    {"text": json.dumps({"pensamiento": "Recogeré el cuaderno en cuanto llegue."})}
                                ]
                            }
                        }
                    ],
                    "usageMetadata": {
                        "promptTokenCount": 900,
                        "cachedContentTokenCount": 500,
                        "candidatesTokenCount": 20,
                    },
                },
            )

        motor_gem = MotorSimulado(epoca="ep-multi-prov")
        motor_gem.pulsos = [pulso]
        modelo_gem = crear_modelo(
            ConfigModelo(proveedor="gemini", modelo="gemini-2.5-flash"),
            transport=httpx.MockTransport(gemini_handler),
        )
        with ClienteMotor(transport=httpx.MockTransport(motor_gem.handler)) as cliente_gem:
            res_gem = CicloMente(cliente_gem, modelo_gem, VOZ_LEAH).ejecutar_paso(espera_ms=0)
        assert [r.estado for r in res_gem] == ["aceptada"]
        assert modelo_gem.ultimo_uso.tokens_cache == 500

    def test_respuesta_mal_formada_y_fallo_de_proveedor_sin_credenciales_reales(self):
        pulso = {**_cargar_ejemplo("pulso--hito.json"), "id": 31, "epoca": "ep-err-prov"}
        motor = MotorSimulado(epoca="ep-err-prov")
        motor.pulsos = [pulso]

        # Proveedor local no exige credenciales en variables de entorno; devuelve JSON roto en ambos intentos.
        modelo_mal = crear_modelo(
            ConfigModelo(proveedor="local", modelo="qwen2.5-7b"),
            transport=httpx.MockTransport(
                lambda _req: httpx.Response(
                    200,
                    json={"choices": [{"message": {"content": "{no es json valido"}}]},
                )
            ),
        )
        telemetria_mal = RegistroTelemetria()
        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            res_mal = CicloMente(cliente, modelo_mal, VOZ_LEAH, telemetria=telemetria_mal).ejecutar_paso(espera_ms=0)

        assert [r.estado for r in res_mal] == ["rechazada_local"]
        assert res_mal[0].intentos_modelo == 2
        assert motor.respuestas_recibidas == []
        assert any(ev["tipo"] == "reintento_local" for ev in telemetria_mal.eventos)
        assert any(ev["tipo"] == "rechazo_local" for ev in telemetria_mal.eventos)

        # Error HTTP 503 del proveedor: captura ErrorModeloError sin tumbar el ciclo y lo registra en telemetría.
        motor_http = MotorSimulado(epoca="ep-err-prov")
        motor_http.pulsos = [pulso]
        modelo_http_err = crear_modelo(
            ConfigModelo(proveedor="local", modelo="qwen2.5-7b"),
            transport=httpx.MockTransport(lambda _req: httpx.Response(503, text="Servicio sobrecargado")),
        )
        telemetria_http = RegistroTelemetria()
        with ClienteMotor(transport=httpx.MockTransport(motor_http.handler)) as cliente_http:
            res_http = CicloMente(cliente_http, modelo_http_err, VOZ_LEAH, telemetria=telemetria_http).ejecutar_paso(espera_ms=0)

        assert [r.estado for r in res_http] == ["error_proveedor"]
        assert "HTTP 503" in res_http[0].errores_locales[0]
        assert any(ev["tipo"] == "error_proveedor" for ev in telemetria_http.eventos)

        # Error de red (transporte caído) también se captura como error_proveedor.
        def red_caida(request: httpx.Request) -> httpx.Response:
            raise httpx.ConnectError("Conexión rechazada", request=request)

        motor_red = MotorSimulado(epoca="ep-err-prov")
        motor_red.pulsos = [pulso]
        modelo_red_err = crear_modelo(
            ConfigModelo(proveedor="local", modelo="qwen2.5-7b"),
            transport=httpx.MockTransport(red_caida),
        )
        with ClienteMotor(transport=httpx.MockTransport(motor_red.handler)) as cliente_red:
            res_red = CicloMente(cliente_red, modelo_red_err, VOZ_LEAH).ejecutar_paso(espera_ms=0)

        assert [r.estado for r in res_red] == ["error_proveedor"]
