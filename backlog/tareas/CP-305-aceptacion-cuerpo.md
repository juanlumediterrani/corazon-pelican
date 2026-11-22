---
id: CP-305
titulo: Aceptación de fase 3 · intenciones cumplidas y rechazadas en vivo 🎮
estado: hecha
fecha: 2026-11-22
prioridad: P2
hito: F3
depende_de: [CP-303, CP-304, CP-306, CP-208]
tipo: review
complejidad: media
duracion_min: 120
sensibilidad: public
vivo: si
---

## Objetivo

Un día en DEV en el que Leah decide, se mueve, dibuja (sin obra) y vuelve a su rutina.

## Contexto

ADS §14 fase 3.

## Alcance

- Día completo con mente real y observación de la pauta en los días siguientes; al menos una intención bloqueada provocada (pasarela ocupada por el jugador).

## Criterios de aceptación

- [x] `backlog/reportes/CP-305.md` con intenciones por estado final y su causa. (`backlog/reportes/CP-305.md` §1)
- [x] R-06, R-11 y R-13 con tests verdes citados. (`ValidadorIntencionTests`, `CuerpoVocabularioTests`, `DegradacionVanillaTests`, `EscenaSinAlucinacionTests`)
- [x] Observar un tramo sin conversaciones, encargos ni pulsos forzados. Conservar el horario vanilla previsto, el pulso natural, las opciones del perfil, la intención validada y el lugar o animación observados después. Quien prueba prepara las circunstancias y deja abierta la elección de Leah. (`backlog/reportes/CP-305.md` §1, Día 1 y Día 2 en `CorazonDev_454975123` con Bedrock)
- [x] Observar el despertar siguiente tras un episodio guardado. Relacionar el episodio con lo que Leah decide retomar, cambiar o dejar y con el primer efecto visible. En los días observados, pedir a quien observa que describa primero el cambio pequeño que ha visto, antes de leer el diario o la telemetría. Contrastar después con la impresión de CP-210 y con el horario vanilla. Registrar también un tramo en que conservar el horario tenga sentido. (`backlog/reportes/CP-305.md` §2: `ep-5`/`ep-6` retomados en el despertar del Día 2 hacia `mesa_de_esculpir`)
- [x] Al final del día Leah duerme en su casa como en vanilla. (`LeahHouse (2, 4)` verificado tras `p4e_rollover_now`)
- [x] Leah cambia al menos un hito de su rutina por decisión propia (p. ej. no va al Saloon) y el resto del día sigue su horario; R-16 con test verde citado. (`ControlRutinaCascadaTests` + `backlog/reportes/CP-305.md` §3)

## Fuera de alcance

- Obras con sprite (F4).
