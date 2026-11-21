"""Pruebas de cadenas de pensamiento en hitos `despertar` y `acostarse` (CP-209)."""

from __future__ import annotations

import json
from typing import Any

import httpx

from mente.cadenas import PasoCadena, PlantillaCadena, ejecutar_cadena, resolver_plantilla_cadena
from mente.cliente_motor import CicloMente, ClienteMotor, RegistroTelemetria
from mente.modelos import FakeLLM
from mente.tests.test_mente import EJEMPLOS_VALIDOS, MotorSimulado, VOZ_LEAH


def _cargar_ejemplo(nombre: str) -> dict[str, Any]:
    return json.loads((EJEMPLOS_VALIDOS / nombre).read_text(encoding="utf-8"))


class TestCadenasHitos:
    def test_cadena_despertar_ejecuta_tres_pasos_en_orden_y_envia_una_respuesta(self):
        pulso_base = _cargar_ejemplo("pulso--hito.json")
        pulso_despertar = {
            **pulso_base,
            "id": 100,
            "epoca": "ep-desp",
            "hito": {"id": "despertar", "origen": "rutina", "forma": "cadena"},
            "agenda": [
                {"hito": "Fri:900", "prosa": "pasear junto al río", "estado": "pendiente"},
                {"hito": "Fri:1600", "prosa": "ir al Saloon", "estado": "pendiente"},
            ],
            "compromisos_proximos": [],
            "recuerdos": [
                {"id": "rec-1", "texto": "Ayer la luz del lago quedó a medias.", "origen_diario": ["ep-7"]}
            ],
        }
        motor = MotorSimulado(epoca="ep-desp")
        motor.pulsos = [pulso_despertar]

        modelo = FakeLLM(
            respuestas=[
                {"pensamiento": "Paso 1: recuerdo que ayer quedó pendiente la luz del lago."},
                {"pensamiento": "Paso 2: amanece despejado y me siento con buen pulso para dibujar."},
                {
                    "pensamiento": "Paso 3: mantengo la mañana junto al río y dejo el Saloon para luego.",
                    "agenda": [
                        {"hito": "Fri:900", "estado": "pendiente"},
                        {"hito": "Fri:1600", "estado": "pendiente"},
                    ],
                },
            ]
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in resultados] == ["aceptada"]
        assert len(modelo.llamadas) == 3
        assert len(motor.respuestas_recibidas) == 1

        # Prefijo estable idéntico en toda la cadena; cada paso recibe el pensamiento del anterior.
        sistemas = {c.prompt.sistema for c in modelo.llamadas}
        assert len(sistemas) == 1
        assert "ayer_y_pendiente" not in modelo.llamadas[0].prompt.usuario  # instrucción va en prosa
        assert "Paso 1/3" in modelo.llamadas[0].prompt.usuario
        assert "Paso 2/3" in modelo.llamadas[1].prompt.usuario
        assert "Paso 1: recuerdo que ayer quedó pendiente la luz del lago." in modelo.llamadas[1].prompt.usuario
        assert "Paso 3/3" in modelo.llamadas[2].prompt.usuario
        assert "Paso 2: amanece despejado y me siento con buen pulso para dibujar." in modelo.llamadas[2].prompt.usuario

        resp_enviada = motor.respuestas_recibidas[0]
        assert [a["hito"] for a in resp_enviada["agenda"]] == ["Fri:900", "Fri:1600"]

    def test_cadena_acostarse_cuatro_pasos_y_todo_recuerdo_cita_episodios_diario(self):
        pulso_acostarse = {**_cargar_ejemplo("pulso--diario-con-origen.json"), "id": 200, "epoca": "ep-noche"}
        motor = MotorSimulado(epoca="ep-noche")
        motor.pulsos = [pulso_acostarse]

        modelo = FakeLLM(
            respuestas=[
                {"pensamiento": "Paso 1: hoy golpeaba la lluvia mientras trazaba líneas."},
                {"pensamiento": "Paso 2: ep-8 y ep-9 explican por qué me serenó recoger a tiempo."},
                {"pensamiento": "Paso 3: hoy no coincidí con nadie cerca."},
                # Primer intento final mezcla ep-999 ajeno junto a ep-8; luego reintenta válido solo con ep-8.
                {
                    "pensamiento": "Paso 4: recojo el cuaderno antes de dormir.",
                    "memoria": {
                        "reflexiones": [
                            {"texto": "La lluvia sobre el tejado me ayudó a concentrarme.", "origen_diario": ["ep-999"]}
                        ]
                    },
                },
                {
                    "pensamiento": "Paso 4 corregido: cito únicamente el episodio real del día.",
                    "memoria": {
                        "reflexiones": [
                            {"texto": "La lluvia sobre el tejado me ayudó a concentrarme.", "origen_diario": ["ep-8"]}
                        ]
                    },
                },
            ]
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in resultados] == ["aceptada"]
        # 3 intermedios + 2 intentos del paso final = 5 llamadas, pero 1 sola respuesta al motor.
        assert len(modelo.llamadas) == 5
        assert len(motor.respuestas_recibidas) == 1
        reflexiones = motor.respuestas_recibidas[0]["memoria"]["reflexiones"]
        assert len(reflexiones) == 1
        assert all(
            len(ref["origen_diario"]) >= 1 and all(ep in pulso_acostarse["episodios_diario"] for ep in ref["origen_diario"])
            for ref in reflexiones
        )

    def test_plantillas_configurables_por_perfil_y_por_hito(self):
        plantillas = {
            ("pierre", "cierre_tienda"): PlantillaCadena(
                hito_id="cierre_tienda",
                perfil="pierre",
                pasos=(
                    PasoCadena("caja", "Paso 1: haz balance de las ventas del día."),
                    PasoCadena("reposicion", "Paso 2: anota qué semillas faltan y emite el JSON final."),
                ),
            ),
            (None, "hito_comun"): [
                ("unico_paso", "Paso único: resume el momento y decide."),
            ],
        }

        pulso_pierre = {
            "personaje": "pierre",
            "hito": {"id": "cierre_tienda", "origen": "rutina", "forma": "cadena"},
        }
        pulso_otro = {
            "personaje": "leah",
            "hito": {"id": "cierre_tienda", "origen": "rutina", "forma": "cadena"},
        }
        pulso_comun = {
            "personaje": "leah",
            "hito": {"id": "hito_comun", "origen": "rutina", "forma": "cadena"},
        }

        tpl_pierre = resolver_plantilla_cadena(pulso_pierre, plantillas)
        assert tpl_pierre.perfil == "pierre"
        assert [p.nombre for p in tpl_pierre.pasos] == ["caja", "reposicion"]

        tpl_generica = resolver_plantilla_cadena(pulso_otro, plantillas)
        assert [p.nombre for p in tpl_generica.pasos] == ["situacion", "decision"]

        tpl_comun = resolver_plantilla_cadena(pulso_comun, plantillas)
        assert [p.nombre for p in tpl_comun.pasos] == ["unico_paso"]

    def test_episodio_propio_trazable_hasta_despertar_siguiente_con_continuidad_en_agenda(self):
        # Episodio propio sin interlocutor ni compromiso en día 10 -> acostarse -> despertar día 11.
        episodio_id = "ep-42"
        pulso_acostarse = {
            **_cargar_ejemplo("pulso--diario-con-origen.json"),
            "id": 10,
            "epoca": "ep-solo",
            "tiempo_juego": {"dia": 10, "hora": 2300},
            "episodios_diario": {
                episodio_id: "Empezaste a tallar a solas una figura de madera en la cabaña, sin terminar la base.",
            },
        }
        pulso_despertar = {
            **_cargar_ejemplo("pulso--hito.json"),
            "id": 11,
            "epoca": "ep-solo",
            "tiempo_juego": {"dia": 11, "hora": 600},
            "hito": {"id": "despertar", "origen": "rutina", "forma": "cadena"},
            "agenda": [
                {"hito": "Sat:900", "prosa": "trabajar en la escultura", "estado": "pendiente"},
                {"hito": "Sat:1700", "prosa": "dar un paseo por el bosque", "estado": "pendiente"},
            ],
            "compromisos_proximos": [],
        }
        pulso_despertar.pop("recuerdos", None)

        motor = MotorSimulado(epoca="ep-solo")
        # Llega despertar antes en el array para verificar también orden acostarse -> despertar.
        motor.pulsos = [pulso_despertar, pulso_acostarse]

        def responder_al_recibir(cuerpo: dict[str, Any]) -> dict[str, Any]:
            if cuerpo.get("basada_en") == 10:
                reflexiones = (cuerpo.get("memoria") or {}).get("reflexiones") or []
                motor.memoria_por_personaje["leah"] = {
                    "epoca": "ep-solo",
                    "personaje": "leah",
                    "diario": [
                        {
                            "id": episodio_id,
                            "dia_juego": 10,
                            "hora": 1800,
                            "resumen_prosa": pulso_acostarse["episodios_diario"][episodio_id],
                            "saliencia": 0.88,
                            "entidades": [],
                        }
                    ],
                    "recuerdos": [
                        {
                            "id": "rec-42",
                            "texto": reflexiones[0]["texto"],
                            "importancia": 0.85,
                            "origen_diario": reflexiones[0]["origen_diario"],
                        }
                    ],
                    "relaciones": {},
                    "compromisos": [],
                    "portafolio": [],
                }
            return {"estado": "aceptada"}

        motor.al_recibir_respuesta = responder_al_recibir

        modelo = FakeLLM(
            respuestas=[
                # Cadena acostarse (4 pasos)
                {"pensamiento": "Hoy me quedé tallando la base de madera hasta tarde."},
                {"pensamiento": f"Ese episodio ({episodio_id}) me dejó con ganas de terminarlo."},
                {"pensamiento": "Estuve completamente a solas."},
                {
                    "pensamiento": "Dejo anotado lo que empecé hoy con la madera.",
                    "memoria": {
                        "reflexiones": [
                            {
                                "texto": "Dejé a medias la talla de madera en la cabaña.",
                                "origen_diario": [episodio_id],
                            }
                        ]
                    },
                },
                # Cadena despertar (3 pasos)
                {"pensamiento": f"Traigo de ayer {episodio_id}: quiero continuar la talla."},
                {"pensamiento": "Amanece tranquilo en la cabaña."},
                {
                    "pensamiento": "Retomo la figura hoy y mantengo el paseo.",
                    "intencion": {
                        "objetivo": "continuar la talla iniciada ayer",
                        "porque": "quedó a medias",
                        "plan": [{"accion": "esperar", "duracion_min": 20}],
                    },
                    "agenda": [
                        {"hito": "int-11", "estado": "pendiente"},
                        {"hito": "Sat:900", "estado": "pendiente"},
                        {"hito": "Sat:1700", "estado": "pendiente"},
                    ],
                },
            ]
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.pulso_id for r in resultados] == [10, 11]
        assert [r.estado for r in resultados] == ["aceptada", "aceptada"]
        # En el despertar, el prompt incluye el recuerdo nacido de la reflexión y su id de origen.
        prompt_despertar = modelo.llamadas[4].prompt.usuario
        assert "## Recuerdos" in prompt_despertar
        assert "Dejé a medias la talla de madera en la cabaña." in prompt_despertar
        assert episodio_id in prompt_despertar
        agenda_despertar = resultados[1].respuesta["agenda"]
        assert any(a["hito"] == "int-11" and a["estado"] == "pendiente" for a in agenda_despertar)

    def test_despertar_con_compromiso_pendiente_lo_coloca_o_descarta_en_la_agenda(self):
        pulso_despertar = {
            **_cargar_ejemplo("pulso--hito.json"),
            "id": 30,
            "epoca": "ep-comp",
            "hito": {"id": "despertar", "origen": "rutina", "forma": "cadena"},
            "agenda": [
                {"hito": "fall:1900", "prosa": "volver a casa", "estado": "pendiente"},
            ],
            "compromisos_proximos": [
                {
                    "id": "comp-12",
                    "con": "farmer:123",
                    "prosa": "dibujar el lago para Ana",
                    "plazo_juego": {"dia": 69, "hora": 1900},
                },
                {
                    "id": "comp-13",
                    "con": "farmer:123",
                    "prosa": "mostrar boceto antiguo",
                    "plazo_juego": {"dia": 72, "hora": 1200},
                },
            ],
        }
        motor = MotorSimulado(epoca="ep-comp")
        motor.pulsos = [pulso_despertar]
        modelo = FakeLLM(
            respuestas=[
                {"pensamiento": "Tengo dos compromisos pendientes con Ana."},
                {"pensamiento": "Hoy solo puedo encajar el del lago y descarto el otro hasta mañana."},
                {
                    "pensamiento": "Coloco comp-12 en la agenda y descarto explícitamente comp-13 hoy.",
                    "agenda": [
                        {"hito": "comp-12", "estado": "pendiente"},
                        {"hito": "comp-13", "estado": "saltado"},
                        {"hito": "fall:1900", "estado": "pendiente"},
                    ],
                },
            ]
        )

        with ClienteMotor(transport=httpx.MockTransport(motor.handler)) as cliente:
            ciclo = CicloMente(cliente, modelo, VOZ_LEAH)
            resultados = ciclo.ejecutar_paso(espera_ms=0)

        assert [r.estado for r in resultados] == ["aceptada"]
        agenda = resultados[0].respuesta["agenda"]
        assert {"hito": "comp-12", "estado": "pendiente"} in agenda
        assert {"hito": "comp-13", "estado": "saltado"} in agenda

    def test_fallo_en_un_paso_termina_cadena_con_lo_obtenido_y_no_altera_rutina(self):
        pulso_despertar = {
            **_cargar_ejemplo("pulso--hito.json"),
            "id": 50,
            "epoca": "ep-parcial",
            "hito": {"id": "despertar", "origen": "rutina", "forma": "cadena"},
        }
        telemetria = RegistroTelemetria()
        modelo = FakeLLM(
            respuestas=[
                {"pensamiento": "Paso 1: recuerdo la luz fría de ayer junto al lago."},
                RuntimeError("tiempo de espera agotado en el paso 2"),
            ]
        )

        resultado = ejecutar_cadena(
            pulso_despertar,
            modelo,
            VOZ_LEAH,
            al_registrar_evento=telemetria.registrar,
        )

        assert resultado.estado == "parcial"
        assert resultado.parcial is True
        assert resultado.respuesta is not None
        assert "luz fría de ayer junto al lago" in resultado.respuesta["pensamiento"]
        # Sin intención, agenda ni cambios de rutina: sigue la rutina del juego sin sobresaltos.
        assert "intencion" not in resultado.respuesta
        assert "agenda" not in resultado.respuesta
        assert any(
            ev["tipo"] == "cadena_parcial"
            and ev["paso_fallido"] == "amanecer_y_animo"
            and ev["pasos_completados"] == ["ayer_y_pendiente"]
            for ev in telemetria.eventos
        )
