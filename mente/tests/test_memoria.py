"""Pruebas de recuperación de memoria y reflexión del día (CP-206, CP-211)."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pytest
from jsonschema import Draft202012Validator

from mente import contrato
from mente.ensamblador import cargar_voz, ensamblar_prompt
from mente.modelos import FakeLLM, validar_respuesta_estrechada
from mente.recuperacion import estimar_tokens, recuperar_recuerdos
from mente.reflexion import (
    construir_esquema_reflexion,
    filtrar_reflexiones,
    generar_reflexion,
    validar_reflexiones,
)

RAIZ = Path(__file__).resolve().parent.parent.parent
EJEMPLOS_VALIDOS = contrato.DIR_ESQUEMAS / "ejemplos" / "validos"
VOZ_LEAH = cargar_voz(RAIZ / "CorazonPelican" / "assets" / "personajes" / "leah.voz.md")


def _cargar_ejemplo(nombre: str) -> dict[str, Any]:
    return json.loads((EJEMPLOS_VALIDOS / nombre).read_text(encoding="utf-8"))


class TestRecuperacionMemoria:
    def test_con_200_entradas_diario_respeta_presupuesto_tokens_y_prioriza_por_formula(self):
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        # El ejemplo tiene día 68, hora 1740, cerca farmer:123 (Ana), lugar pasarela_lago, huella agua:Mountain:lago.
        diario: list[dict[str, Any]] = []
        for i in range(1, 201):
            if i == 199:
                # Muy reciente, muy importante y muy relevante (menciona entidad y tema del pulso).
                diario.append(
                    {
                        "id": f"ep-{i}",
                        "dia_juego": 68,
                        "hora": 1500,
                        "resumen_prosa": "Ana se acercó a la pasarela_lago mirando el agua gris del lago.",
                        "saliencia": 0.95,
                        "entidades": ["farmer:123", "agua:Mountain:lago"],
                    }
                )
            elif i == 200:
                # Reciente, importante, relevante a intención/lugar.
                diario.append(
                    {
                        "id": f"ep-{i}",
                        "dia_juego": 67,
                        "hora": 1800,
                        "resumen_prosa": "Boceto del sauce en la pasarela de madera junto al lago.",
                        "saliencia": 0.88,
                        "entidades": ["agua:Mountain:lago", "luz"],
                    }
                )
            elif i <= 100:
                # Entradas muy antiguas, baja importancia y sin relación con el pulso.
                diario.append(
                    {
                        "id": f"ep-{i}",
                        "dia_juego": 5,
                        "hora": 900,
                        "resumen_prosa": f"Entrada antigua #{i}: recogiste setas secas en un sendero remoto del bosque.",
                        "saliencia": 0.18,
                        "entidades": ["objeto:seta"],
                    }
                )
            else:
                # Entradas intermedias sin relación directa.
                diario.append(
                    {
                        "id": f"ep-{i}",
                        "dia_juego": 50,
                        "hora": 1200,
                        "resumen_prosa": f"Entrada intermedia #{i}: ordenaste herramientas de carpintería en la cabaña.",
                        "saliencia": 0.40,
                        "entidades": ["objeto:martillo"],
                    }
                )

        memoria = {
            "epoca": pulso["epoca"],
            "personaje": "leah",
            "diario": diario,
            "recuerdos": [],
            "relaciones": {},
            "compromisos": [],
            "portafolio": [],
        }

        presupuesto = 90
        seleccion = recuperar_recuerdos(memoria, pulso, presupuesto_tokens=presupuesto)

        assert len(seleccion) >= 2
        tokens_totales = sum(estimar_tokens(f"- {r['texto']}") for r in seleccion)
        assert tokens_totales <= presupuesto
        assert sum(r["tokens"] for r in seleccion) <= presupuesto

        # Los recuerdos más recientes, importantes y relevantes deben encabezar la selección.
        ids_top = [r.get("id") for r in seleccion[:2]]
        assert ids_top[0] == "ep-199"
        assert "ep-200" in ids_top

        # Debe integrarse limpiamente con ensamblar_prompt.
        prompt = ensamblar_prompt(VOZ_LEAH, pulso, recuerdos=seleccion)
        assert "Ana se acercó a la pasarela_lago" in prompt.usuario

    def test_con_200_recuerdos_con_origen_diario_respeta_presupuesto_estricto(self):
        pulso = _cargar_ejemplo("pulso--hito.json")
        recuerdos = [
            {
                "id": f"rec-{i}",
                "texto": (
                    f"Recuerdo #{i}: la luz de otoño sobre la pasarela_lago me pidió otro boceto."
                    if i == 180
                    else f"Recuerdo #{i}: ordené frascos de pintura seca y pinceles gastados en la estantería."
                ),
                "importancia": 0.92 if i == 180 else 0.35,
                "origen_diario": [f"ep-{i}"],
                "dia_juego": 68 if i == 180 else 20 + (i % 30),
                "hora": 1600,
                "entidades": ["agua:Mountain:lago", "luz"] if i == 180 else [],
            }
            for i in range(1, 201)
        ]
        memoria = {
            "epoca": pulso["epoca"],
            "personaje": "leah",
            "diario": [],
            "recuerdos": recuerdos,
        }

        presupuesto = 65
        seleccion = recuperar_recuerdos(memoria, pulso, presupuesto_tokens=presupuesto)
        assert len(seleccion) >= 1
        assert sum(r["tokens"] for r in seleccion) <= presupuesto
        assert seleccion[0]["id"] == "rec-180"
        assert seleccion[0]["origen_diario"] == ["ep-180"]

    def test_compatibilidad_con_recuerdos_heredados_en_prosa_sin_origen_diario(self):
        pulso = _cargar_ejemplo("pulso--interlocucion.json")
        memoria = {
            "epoca": pulso["epoca"],
            "personaje": "leah",
            "diario": [
                {
                    "id": "ep-8",
                    "dia_juego": 68,
                    "hora": 1400,
                    "resumen_prosa": "Ana estuvo mirando el muelle del lago.",
                    "saliencia": 0.85,
                    "entidades": ["farmer:123", "agua:Mountain:lago"],
                }
            ],
            "recuerdos": [
                # Recuerdo con origen_diario (modelo nuevo CP-211)
                {
                    "id": "rec-1",
                    "texto": "Ana disfruta viendo la niebla sobre el lago tanto como yo.",
                    "importancia": 0.85,
                    "origen_diario": ["ep-8"],
                },
                # Recuerdo heredado en prosa pura sin origen_diario ni dict
                "Una vez prometí pintar la pasarela del lago al atardecer con Ana.",
                # Recuerdo heredado como dict sin origen_diario
                {"texto": "En invierno la cabaña huele a leña y trementina.", "importancia": 0.4},
            ],
        }

        seleccion = recuperar_recuerdos(memoria, pulso, presupuesto_tokens=200)
        textos = [r["texto"] for r in seleccion]
        assert "Ana disfruta viendo la niebla sobre el lago tanto como yo." in textos
        assert "Una vez prometí pintar la pasarela del lago al atardecer con Ana." in textos
        assert "En invierno la cabaña huele a leña y trementina." in textos

        # También acepta una lista plana heredada de cadenas en prosa.
        seleccion_lista = recuperar_recuerdos(
            [
                "Ayer llovió en el lago y Ana trajo café.",
                "Compré harina en el pueblo.",
            ],
            pulso,
            presupuesto_tokens=150,
        )
        assert seleccion_lista[0]["texto"] == "Ayer llovió en el lago y Ana trajo café."


class TestReflexionDia:
    def test_genera_reflexiones_citando_ids_reales_de_episodios_diario(self):
        pulso = _cargar_ejemplo("pulso--diario-con-origen.json")
        esquema = construir_esquema_reflexion(pulso)
        Draft202012Validator.check_schema(esquema)

        modelo = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "La lluvia sobre el tejado me ayudó a concentrarme hasta guardar el cuaderno.",
                    "memoria": {
                        "reflexiones": [
                            {
                                "texto": "Me vino bien aquella tarde tranquila bajo la lluvia.",
                                "origen_diario": ["ep-8", "ep-9"],
                            }
                        ]
                    },
                }
            ]
        )

        resultado = generar_reflexion(pulso, modelo, VOZ_LEAH)

        assert resultado.valida is True
        assert resultado.intentos == 1
        assert resultado.errores == []
        assert resultado.descartadas == []
        assert resultado.respuesta is not None
        assert len(resultado.reflexiones) == 1
        assert resultado.reflexiones[0]["origen_diario"] == ["ep-8", "ep-9"]
        assert contrato.errores("respuesta", resultado.respuesta) == []
        assert validar_respuesta_estrechada(pulso, resultado.respuesta, esquema) == []
        assert validar_reflexiones(pulso, resultado.respuesta) == []
        assert "ep-8" in modelo.llamadas[0].prompt.usuario
        assert "ep-9" in modelo.llamadas[0].prompt.usuario

    def test_rechaza_cita_ajena_y_reintenta_hasta_obtener_origen_valido(self):
        pulso = _cargar_ejemplo("pulso--diario-con-origen.json")
        modelo = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Cito un episodio que no existe en este pulso.",
                    "memoria": {
                        "reflexiones": [
                            {
                                "texto": "Recuerdo inventado de otro día.",
                                "origen_diario": ["ep-999"],
                            }
                        ]
                    },
                },
                {
                    "pensamiento": "Corrijo la referencia al episodio real de la lluvia.",
                    "memoria": {
                        "reflexiones": [
                            {
                                "texto": "La lluvia en el tejado me dejó serenidad.",
                                "origen_diario": ["ep-8"],
                            }
                        ]
                    },
                },
            ]
        )

        resultado = generar_reflexion(pulso, modelo, VOZ_LEAH, max_reintentos=1)

        assert resultado.valida is True
        assert resultado.intentos == 2
        assert len(modelo.llamadas) == 2
        assert "ep-8" in modelo.llamadas[1].prompt.usuario
        assert "ese episodio no figura" in modelo.llamadas[1].prompt.usuario
        assert resultado.reflexiones == [
            {"texto": "La lluvia en el tejado me dejó serenidad.", "origen_diario": ["ep-8"]}
        ]

    def test_filtra_citas_ajenas_y_nunca_emite_reflexion_sin_origen_valido(self):
        pulso = _cargar_ejemplo("pulso--diario-con-origen.json")

        # Caso 1: mezcla una reflexión completamente inventada y otra con un id real + uno ajeno.
        modelo_mixto = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Repaso lo vivido antes de dormir.",
                    "memoria": {
                        "reflexiones": [
                            {"texto": "Reflexión sin base real.", "origen_diario": ["ep-999"]},
                            {"texto": "La tarde de lluvia valió la pena.", "origen_diario": ["ep-8", "ep-777"]},
                        ]
                    },
                }
            ]
        )
        res_mixto = generar_reflexion(pulso, modelo_mixto, VOZ_LEAH, max_reintentos=0, filtrar_citas_ajenas=True)
        assert res_mixto.valida is True
        assert res_mixto.reflexiones == [
            {"texto": "La tarde de lluvia valió la pena.", "origen_diario": ["ep-8"]}
        ]
        assert len(res_mixto.descartadas) == 2
        assert contrato.errores("respuesta", res_mixto.respuesta) == []

        # Caso 2: si TODAS las citas son ajenas o usa memoria.recordar, nunca emite una reflexión inválida.
        modelo_invalido = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Intento usar recordar en prosa sin origen_diario.",
                    "memoria": {"recordar": ["Recuerdo suelto sin cita."]},
                }
            ]
        )
        res_invalido = generar_reflexion(pulso, modelo_invalido, VOZ_LEAH, max_reintentos=0, filtrar_citas_ajenas=True)
        assert res_invalido.valida is True
        assert res_invalido.reflexiones == []
        assert res_invalido.respuesta is not None
        assert "memoria" not in res_invalido.respuesta
        assert len(res_invalido.descartadas) == 1

        # Caso 3: con filtrar_citas_ajenas=False rechaza completamente.
        modelo_estricto = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Cito un id ajeno.",
                    "memoria": {"reflexiones": [{"texto": "Ajena.", "origen_diario": ["ep-999"]}]},
                }
            ]
        )
        res_estricto = generar_reflexion(pulso, modelo_estricto, VOZ_LEAH, max_reintentos=0, filtrar_citas_ajenas=False)
        assert res_estricto.valida is False
        assert res_estricto.respuesta is None
        assert res_estricto.reflexiones == []

        # filtrar_reflexiones directo tampoco deja pasar citas ajenas.
        limpia, desc = filtrar_reflexiones(
            pulso,
            {
                "pensamiento": "Prueba directa.",
                "epoca": pulso["epoca"],
                "basada_en": pulso["id"],
                "memoria": {"reflexiones": [{"texto": "Sin origen real.", "origen_diario": ["ep-ajeno"]}]},
            },
        )
        assert "memoria" not in limpia
        assert len(desc) == 1

    def test_compatibilidad_con_pulso_anterior_con_diario_dia_en_prosa(self):
        pulso_antiguo = _cargar_ejemplo("pulso--diario-con-origen.json")
        pulso_antiguo.pop("episodios_diario", None)
        pulso_antiguo["diario_dia"] = ["La lluvia golpeaba el tejado.", "Guardaste el cuaderno."]

        modelo = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Fue un día sereno.",
                    "memoria": {"recordar": ["Trabajar con lluvia da calma."]},
                }
            ]
        )
        resultado = generar_reflexion(pulso_antiguo, modelo, VOZ_LEAH)
        assert resultado.valida is True
        assert resultado.respuesta is not None
        assert resultado.respuesta["memoria"] == {"recordar": ["Trabajar con lluvia da calma."]}
        assert contrato.errores("respuesta", resultado.respuesta) == []


class TestSinEscrituraEnDisco:
    def test_recuperacion_y_reflexion_no_escriben_ficheros_de_memoria_en_disco(self, tmp_path, monkeypatch):
        monkeypatch.chdir(tmp_path)

        pulso = _cargar_ejemplo("pulso--diario-con-origen.json")
        memoria = {
            "epoca": pulso["epoca"],
            "personaje": "leah",
            "diario": [
                {
                    "id": "ep-8",
                    "dia_juego": 4,
                    "hora": 1600,
                    "resumen_prosa": "La lluvia golpeaba el tejado mientras trabajabas.",
                    "saliencia": 0.72,
                    "entidades": ["clima:lluvia"],
                },
                {
                    "id": "ep-9",
                    "dia_juego": 4,
                    "hora": 2000,
                    "resumen_prosa": "Al caer la tarde guardaste el cuaderno.",
                    "saliencia": 0.65,
                    "entidades": ["objeto:cuaderno"],
                },
            ],
            "recuerdos": [
                {
                    "id": "rec-1",
                    "texto": "Ayer probé un trazo distinto en la orilla.",
                    "importancia": 0.6,
                    "origen_diario": ["ep-1"],
                },
                "Recuerdo antiguo en prosa.",
            ],
        }

        recuerdos = recuperar_recuerdos(memoria, pulso, presupuesto_tokens=150)
        modelo = FakeLLM(
            respuestas=[
                {
                    "pensamiento": "Cierro el día con calma.",
                    "memoria": {
                        "reflexiones": [
                            {
                                "texto": "La lluvia me ayudó a trabajar sin prisa.",
                                "origen_diario": ["ep-8", "ep-9"],
                            }
                        ]
                    },
                }
            ]
        )
        resultado = generar_reflexion(pulso, modelo, VOZ_LEAH, recuerdos=recuerdos)

        assert resultado.valida is True
        assert list(tmp_path.rglob("*")) == []
