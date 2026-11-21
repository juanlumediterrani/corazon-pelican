---
id: CP-214
titulo: Llevar los recuerdos pertinentes al prompt de un hito ordinario
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-204, CP-206]
tipo: code-light
complejidad: media
duracion_min: 90
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Llevar los recuerdos pertinentes al prompt de un hito ordinario.

## Evidencia

El selector de CP-206 puede encontrar el episodio pertinente. En la prueba de un hito ordinario, `CicloMente.ejecutar_paso` hizo cero consultas de memoria y envió al modelo un prompt sin ese recuerdo. [Revisión de Argón](CP-905-revision-argon.md).

## Alcance

- `mente/cliente_motor.py`: consulta automática de `GET /api/v1/memoria/{personaje}` en `CicloMente.procesar_pulso` con caché descartable por personaje/época, selección mediante `recuperar_recuerdos(...)` respetando `presupuesto_tokens_recuerdos` e invalidación de caché cuando se acepta una respuesta con cambios de memoria.
- `mente/tests/test_memoria.py` (`TestRecuperacionEnCicloMente`): pruebas con `MotorSimulado` y `FakeLLM` sobre hitos ordinarios sin interlocutor, cambio de personaje y cambio de época.

## Criterios de aceptación

- [x] Ejecutar el ciclo normal con un recuerdo en el motor y un pulso de tarde sin interlocutor. Inspeccionar el prompt que recibe FakeLLM y comprobar que incluye el recuerdo pertinente dentro del cupo de contexto. — Evidencia: `TestRecuperacionEnCicloMente.test_hito_ordinario_recupera_recuerdo_pertinente_en_prompt_dentro_del_cupo` (`pytest`: 96/96 superados).
- [x] Repetir con otro personaje o tras un cambio de época. Verificar que se usa su memoria vigente. — Evidencia: `TestRecuperacionEnCicloMente.test_cambio_de_personaje_y_cambio_de_epoca_usan_memoria_vigente` (`pytest`: 96/96 superados).
