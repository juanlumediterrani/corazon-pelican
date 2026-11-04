---
id: CP-011
titulo: Spike V-02 · reclamar a Leah y devolverla a su horario sin romperlo 🎮
estado: pendiente
prioridad: P1
hito: S
depende_de: [CP-006]
tipo: analysis
complejidad: alta
duracion_min: 180
sensibilidad: public
vivo: si
---

## Objetivo

Un mecanismo probado para suspender el horario de Leah, moverla por rutas nativas y reincorporarla al tramo horario que le corresponde.

## Contexto

Según el decompilado ([notas](../../docs/notas-juego.md) §3, con la clave de horario por prioridad y el horario real de Leah): el horario es `Dictionary<int, SchedulePathDescription> Schedule`; `checkSchedule(timeOfDay)` encola la ruta solo en la hora exacta de cada entrada y avanza `lastAttemptedSchedule`. Si Leah está reclamada cuando llega una entrada, esa ruta se pierde: `volver_a_rutina` tiene que calcular por sí mismo la ruta desde donde esté hasta el destino del tramo vigente. También existen `followSchedule`, `ignoreScheduleToday`, `controller`/`temporaryController` y `ClearSchedule()`.

## Alcance

- Reclamación: detener el controlador vigente sin perder el `Schedule` del día y marcar `modData["CorazonPelican.Control"]`.
- `ir_a` con `PathFindController` nativo entre casillas de una ubicación y entre ubicaciones.
- Devolución: localizar la entrada vigente del horario y pedir la ruta a su destino (o esperar a la siguiente entrada si está cerca).
- Leer el horario que el juego eligió hoy (`Schedule`, `dayScheduleName`) y detectar el final de cada entrada (llegada al destino y comportamiento final) como evento para los hitos de rutina (CP-201).
- Documentar qué campos de `SchedulePathDescription` sirven para las anotaciones por patrón del perfil (destino, animación, hora) y cómo se elige la clave del día (estación, día, clima, festival, corazones, matrimonio, cartas).
- Saltar o sustituir una entrada concreta del día sin perder las siguientes (base de CP-306).
- Casos: reclamar justo antes de una entrada de horario, reclamar fuera de su ubicación, guardar con Leah reclamada, fin de día con Leah reclamada.

## Criterios de aceptación

- [ ] Nota `docs/spikes/v02-horario.md` con el mecanismo y los campos de `NPC` implicados.
- [ ] En vivo: reclamada 13:00–15:00 cruzando una entrada de horario, a las 16:00 Leah está donde vanilla la tendría.
- [ ] En vivo: guardar y cargar con Leah reclamada no deja a Leah congelada ni fuera de horario al día siguiente.
- [ ] Ninguna reclamación teletransporta a Leah dentro de la vista del jugador.
- [ ] En vivo: el fin de cada entrada del horario de Leah se registra como evento con la clave del día, la hora y el destino.
- [ ] Con escenarios DEV de dos estaciones y un día de lluvia, la clave del horario y sus entradas cambian como en vanilla y los eventos lo reflejan.
- [ ] En vivo: saltarse su entrada del Saloon deja a Leah en otro sitio y la siguiente entrada se cumple como en vanilla.

## Fuera de alcance

- Vocabulario completo de acciones (CP-302).
