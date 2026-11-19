---
id: CP-214
titulo: Llevar los recuerdos pertinentes al prompt de un hito ordinario
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-204, CP-206]
tipo: code-light
complejidad: media
duracion_min: 90
sensibilidad: public
---

## Objetivo

Llevar los recuerdos pertinentes al prompt de un hito ordinario.

## Evidencia

El selector de CP-206 puede encontrar el episodio pertinente. En la prueba de un hito ordinario, `CicloMente.ejecutar_paso` hizo cero consultas de memoria y envió al modelo un prompt sin ese recuerdo. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [ ] Ejecutar el ciclo normal con un recuerdo en el motor y un pulso de tarde sin interlocutor. Inspeccionar el prompt que recibe FakeLLM y comprobar que incluye el recuerdo pertinente dentro del cupo de contexto.
- [ ] Repetir con otro personaje o tras un cambio de época. Verificar que se usa su memoria vigente.
