"""Pruebas de continuidad de humor entre días y prueba de dos días CP-905 en Python (CP-210)."""

from __future__ import annotations

import json
from typing import Any

import httpx

from mente import contrato
from mente.cliente_motor import CicloMente, ClienteMotor
from mente.ensamblador import ensamblar_prompt
from mente.modelos import FakeLLM, estrechar_esquema, validar_respuesta_estrechada
from mente.tests.test_mente import EJEMPLOS_VALIDOS, MotorSimulado, VOZ_LEAH


def _cargar_ejemplo(nombre: str) -> dict[str, Any]:
    return json.loads((EJEMPLOS_VALIDOS / nombre).read_text(encoding="utf-8"))


class TestContratoYPromptHumor:
    def test_contrato_pulso_y_respuesta_con_humor_y_estrechado(self):
        pulso_humor = _cargar_ejemplo("pulso--humor.json")
        resp_humor = _cargar_ejemplo("respuesta--humor.json")
        assert contrato.errores("pulso", pulso_humor) == []
        assert contrato.errores("respuesta", resp_humor) == []

        esquema = estrechar_esquema(pulso_humor)
        assert validar_respuesta_estrechada(pulso_humor, resp_humor, esquema) == []

        prompt = ensamblar_prompt(VOZ_LEAH, pulso_humor)
        assert "## Humor vigente" in prompt.usuario
        assert "serena tras la tarde tranquila junto al agua" in prompt.usuario
        assert "0.65" in prompt.usuario
        assert "`ep-12`" in prompt.usuario

        sin_origen = {
            **resp_humor,
            "humor": {"resumen": "algo sin base", "intensidad": 0.5, "origen_diario": ["ep-999"]},
        }
        assert validar_respuesta_estrechada(pulso_humor, sin_origen, esquema) != []


class TestContinuidadDosDiasYConducta:
    def test_prueba_dos_dias_cp905_sigue_episodio_hasta_prompt_de_hito_ordinario(self):
        episodio_id = "ep-tarde-1"
        pulso_acostarse = {
            **_cargar_ejemplo("pulso--diario-con-origen.json"),
            "id": 10,
            "epoca": "ep-cp905-a",
            "tiempo_juego": {"dia": 68, "hora": 2300},
            "episodios_diario": {
                episodio_id: "Pasaste toda la tarde a solas mirando la luz sobre el lago desde la pasarela.",
            },
        }

        motor = MotorSimulado(epoca="ep-cp905-a")
        motor.pulsos = [pulso_acostarse]

        estado_guardado: dict[str, Any] = {}

        def al_recibir_en_host(cuerpo: dict[str, Any]) -> dict[str, Any]:
            if cuerpo.get("basada_en") == 10:
                reflexiones = (cuerpo.get("memoria") or {}).get("reflexiones") or []
                humor = cuerpo.get("humor")
                estado_guardado["leah"] = {
                    "diario": [
                        {
                            "id": episodio_id,
                            "dia_juego": 68,
                            "hora": 1740,
                            "resumen_prosa": pulso_acostarse["episodios_diario"][episodio_id],
                            "saliencia": 0.9,
                            "entidades": ["agua:Mountain:lago"],
                        }
                    ],
                    "recuerdos": [
                        {
                            "id": "rec-1",
                            "texto": reflexiones[0]["texto"],
                            "importancia": 0.85,
                            "origen_diario": reflexiones[0]["origen_diario"],
                        }
                    ],
                    "relaciones": {},
                    "compromisos": [],
                    "portafolio": [],
                    "humor": {
                        "resumen": humor["resumen"],
                        "intensidad": min(0.85, humor["intensidad"]),
                        "intensidad_inicial": min(0.85, humor["intensidad"]),
                        "origen_diario": humor["origen_diario"],
                        "creado_en": {"dia": 68, "hora": 2300},
                    },
                }
            return {"estado": "aceptada"}

        motor.al_recibir_respuesta = al_recibir_en_host

        modelo = FakeLLM(
            respuestas=[
                # 4 pasos cadena acostarse
                {"pensamiento": "Hoy dibujé sin que nadie interrumpiera."},
                {"pensamiento": f"Ese momento ({episodio_id}) me dejó calma y claridad."},
                {"pensamiento": "Estuve a solas toda la tarde."},
                {
                    "pensamiento": "Guardo este recuerdo y esta serenidad para mañana.",
                    "memoria": {
                        "reflexiones": [
                            {
                                "texto": "La tarde a solas junto al lago me dio calma para el nuevo cuadro.",
                                "origen_diario": [episodio_id],
                            }
                        ]
                    },
                    "humor": {
                        "resumen": "serena tras mirar el agua en calma",
                        "intensidad": 0.78,
                        "origen_diario": [episodio_id],
                    },
                },
                # Día siguiente tras recarga: hito ordinario
                {
                    "pensamiento": "Todavía traigo la calma de ayer; me siento en la pasarela a empezar.",
                    "intencion": {
                        "objetivo": "retomar el boceto en calma",
                        "porque": "la luz y el humor de ayer acompañan",
                        "plan": [
                            {"accion": "ir_a", "lugar": "pasarela_lago"},
                            {"accion": "dibujar", "obra": "nueva"},
                        ],
                    },
                },
            ]
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            res_noche = ciclo.ejecutar_paso(espera_ms=0)
            assert [r.estado for r in res_noche] == ["aceptada"]

            # Simulamos guardar tras acostarse y recargar en otra época de sesión.
            pulso_hito_ordinario = {
                **_cargar_ejemplo("pulso--hito.json"),
                "id": 1,
                "epoca": "ep-cp905-recarga",
                "tiempo_juego": {"dia": 69, "hora": 1700},
                "escena": "Vuelves a pasar cerca del lago al caer la tarde.",
                "acciones_disponibles": ["ir_a(pasarela_lago)", "dibujar", "esperar"],
                "lugares": [
                    {"id": "pasarela_lago", "prosa": "la pasarela de madera del lago"},
                    {"id": "tu_casa", "prosa": "tu casa"},
                ],
                "humor": {
                    **estado_guardado["leah"]["humor"],
                    "intensidad": 0.62,
                },
            }
            pulso_hito_ordinario.pop("recuerdos", None)
            motor.nueva_epoca("ep-cp905-recarga", [pulso_hito_ordinario])
            motor.memoria_por_personaje["leah"] = {
                "epoca": "ep-cp905-recarga",
                "personaje": "leah",
                **estado_guardado["leah"],
            }

            res_dia_siguiente = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in res_dia_siguiente] == ["aceptada"]
        prompt_ordinario = modelo.llamadas[-1].prompt.usuario
        assert "## Recuerdos" in prompt_ordinario
        assert episodio_id in prompt_ordinario
        assert "## Humor vigente" in prompt_ordinario
        assert "serena tras mirar el agua en calma" in prompt_ordinario

    def test_misma_tarde_variando_solo_experiencia_previa_usa_solo_opciones_del_pulso(self):
        pulso_base = {
            **_cargar_ejemplo("pulso--hito.json"),
            "id": 40,
            "personaje": "leah",
            "tiempo_juego": {"dia": 20, "hora": 1700},
            "escena": "El lago brilla suavemente frente a la pasarela al final de la tarde.",
            "acciones_disponibles": ["ir_a(pasarela_lago)", "ir_a(tu_casa)", "dibujar", "guardar_cuaderno"],
            "lugares": [
                {"id": "pasarela_lago", "prosa": "la pasarela de madera del lago"},
                {"id": "tu_casa", "prosa": "tu casa"},
            ],
            "compromisos_proximos": [],
        }

        def correr_tarde(epoca: str, humor: dict[str, Any], recuerdos_mem: list[dict[str, Any]], resp_guion: dict[str, Any]):
            pulso = {**pulso_base, "epoca": epoca, "humor": humor}
            pulso.pop("recuerdos", None)
            motor = MotorSimulado(epoca=epoca)
            motor.pulsos = [pulso]
            motor.memoria_por_personaje["leah"] = {
                "epoca": epoca,
                "personaje": "leah",
                "diario": [{"id": humor["origen_diario"][0], "dia_juego": 19, "hora": 1700, "resumen_prosa": recuerdos_mem[0]["texto"], "saliencia": 0.8, "entidades": []}],
                "recuerdos": recuerdos_mem,
                "relaciones": {},
                "compromisos": [],
                "portafolio": [],
                "humor": humor,
            }
            modelo = FakeLLM(respuestas=[resp_guion])
            with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
                ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
                res = ciclo.ejecutar_paso(espera_ms=0)
            return res[0], modelo.llamadas[0].prompt.usuario

        res_a, prompt_a = correr_tarde(
            "ep-tarde-a",
            {"resumen": "serena e inspirada en el lago", "intensidad": 0.7, "origen_diario": ["ep-a"]},
            [{"id": "rec-a", "texto": "Ayer la luz en la pasarela fue limpia.", "importancia": 0.85, "origen_diario": ["ep-a"]}],
            {
                "pensamiento": "Aprovecho esta calma para dibujar en la pasarela.",
                "intencion": {
                    "objetivo": "bocetar junto al lago",
                    "porque": "la calma de ayer pide seguir",
                    "plan": [{"accion": "ir_a", "lugar": "pasarela_lago"}, {"accion": "dibujar", "obra": "nueva"}],
                },
            },
        )
        res_b, prompt_b = correr_tarde(
            "ep-tarde-b",
            {"resumen": "fatigada y con ganas de recogerse", "intensidad": 0.7, "origen_diario": ["ep-b"]},
            [{"id": "rec-b", "texto": "Ayer acabé agotada y con frío en el sendero.", "importancia": 0.85, "origen_diario": ["ep-b"]}],
            {
                "pensamiento": "Hoy prefiero guardar el cuaderno y volver a casa.",
                "intencion": {
                    "objetivo": "recogerme en casa",
                    "porque": "arrastro cansancio desde ayer",
                    "plan": [{"accion": "guardar_cuaderno"}, {"accion": "ir_a", "lugar": "tu_casa"}],
                },
            },
        )

        assert res_a.estado == "aceptada"
        assert res_b.estado == "aceptada"
        assert "serena e inspirada en el lago" in prompt_a
        assert "fatigada y con ganas de recogerse" in prompt_b
        assert res_a.respuesta["intencion"]["plan"] != res_b.respuesta["intencion"]["plan"]
