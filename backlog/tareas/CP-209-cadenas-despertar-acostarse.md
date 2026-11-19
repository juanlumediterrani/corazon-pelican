---
id: CP-209
titulo: Cadenas de pensamiento en los hitos, empezando por despertar y acostarse
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-201, CP-204, CP-206]
tipo: code-light
complejidad: media
duracion_min: 180
sensibilidad: public
---

## Objetivo

Leah empieza y cierra cada día pensando en varios pasos encadenados: por la mañana, qué trae de ayer y qué quiere hoy; por la noche, qué ha pasado y qué significa.

## Contexto

GSS §4.1 y ADS §6. Cualquier hito con `forma: cadena` en el perfil abre una cadena; `despertar` y `acostarse` siempre. Cada cadena es una sola pulsión (con plaza reservada en el presupuesto, CP-202) que la mente resuelve en varias llamadas, cada una con la salida de la anterior, sin actuar en el mundo entre ellas. La reflexión diario → recuerdos (CP-206) vive en la cadena de acostarse.

- **Despertar:** ayer y lo pendiente (recuerdos, compromisos) → cómo amanece y cómo se siente → qué quiere hoy: **la agenda del día** (qué entradas de su horario mantiene, cambia o sustituye, y dónde encajan sus intenciones). Después, cada hito la reequilibra (pensamiento en cascada, CP-306).
- **Acostarse:** qué pasó hoy (diario) → qué significó (recuerdos) → qué piensa de quienes vio (relaciones) → qué queda para mañana.

En vanilla el guardado suele llegar antes de que termine la cadena de acostarse: su respuesta se aplica al llegar (misma época) y se persiste en el siguiente guardado.

## Alcance

- Pulsos `despertar` y `acostarse` con su contexto (diario del día; recuerdos y compromisos pendientes).
- Pasos de cada cadena definidos como plantillas de la mente, configurables por perfil y por hito.
- Una respuesta final por cadena: agenda del día e intenciones (despertar), o memoria, relaciones y compromisos (acostarse).
- Orden: el despertar espera a que se haya aplicado el acostarse anterior.

## Criterios de aceptación

- [ ] pytest con FakeLLM: cada cadena hace los pasos definidos en orden, pasando la salida de cada uno al siguiente, y envía una sola respuesta.
- [ ] Todo recuerdo de la cadena de acostarse cita al menos una entrada del diario del día.
- [ ] FakeWorld: con el guardado antes de la respuesta de acostarse, los recuerdos se aplican al llegar y aparecen tras el siguiente guardado y recarga.
- [ ] El despertar devuelve una agenda que solo contiene entradas del horario de hoy (o planes de intención) y cuyos cambios pasan R-16.
- [ ] pytest con FakeLLM: partir de un episodio propio sin interlocutor ni compromiso. Seguir su identificación desde la reflexión hasta el contexto que recibe el despertar siguiente y comprobar que la agenda admite continuar o revisar lo iniciado.
- [ ] El despertar de un día con un compromiso pendiente lo coloca en la agenda o lo descarta de forma explícita.
- [ ] Si un paso falla, la cadena termina con lo obtenido hasta entonces y lo registra; Leah sigue su rutina.

## Fuera de alcance

- Hitos con cadena propios de otros personajes (p. ej. el cierre de la tienda de Pierre).
