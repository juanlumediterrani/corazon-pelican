---
id: CP-011
titulo: Spike V-02 · reclamar a Leah y devolverla a su horario sin romperlo 🎮
estado: hecha
fecha: 2026-11-22
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

- [x] Nota `docs/spikes/v02-horario.md` con el mecanismo y los campos de `NPC` implicados (`docs/spikes/v02-horario.md`).
- [x] En vivo: reclamada 13:00–15:00 cruzando una entrada de horario, a las 16:00 Leah está donde vanilla la tendría (`[V02-Reclamar]` a las `1300`, entrada `1500` omitida mientras reclamada, `[V02-Volver]` a las `1510` con 100 pasos nativos `LeahHouse -> Forest@(33,25)` y a las `1600` en `Forest@{X:33 Y:25} dir=2 anim=leah_draw`).
- [x] En vivo: guardar y cargar con Leah reclamada no deja a Leah congelada ni fuera de horario al día siguiente (`fall 9` con `control=reclamado@1630` -> guardado y cambio de día `p4e_rollover_now` -> `[V02] (DayStarted) Reclamación previa de Leah liberada -> ignoreScheduleToday=False, ScheduleKey=fall, entradas=5`, operativa en `Town@{X:86 Y:66}` en `fall 10`).
- [x] Ninguna reclamación teletransporta a Leah dentro de la vista del jugador (`sinTeletransporte=True` en `cp_v02_reclamar`, `cp_v02_ir_a` y `cp_v02_volver`; desplazamientos únicamente casilla a casilla con `PathFindController` y cruces de puerta `handleWarps`).
- [x] En vivo: el fin de cada entrada del horario de Leah se registra como evento con la clave del día, la hora y el destino (`[V02-HitoFin] npc=Leah clave=... hora=... destino=... posReal=... dir=... anim=...`).
- [x] Con escenarios DEV de dos estaciones y un día de lluvia, la clave del horario y sus entradas cambian como en vanilla y los eventos lo reflejan (`spring 2 sol` -> `ScheduleKey=spring`, `spring 3 lluvia` -> `ScheduleKey=rain`, `fall 9 sol` -> `ScheduleKey=fall`, `fall 12 sol` -> `ScheduleKey=Fri`).
- [x] En vivo: saltarse su entrada del Saloon deja a Leah en otro sitio y la siguiente entrada se cumple como en vanilla (`cp_v02_saltar 1600` en `Fri`: a las `1630` permanece en `LeahHouse@{X:9 Y:5}` y a las `2340` camina 9 pasos hasta `LeahHouse@{X:2 Y:4}` con `leah_sleep`).

## Fuera de alcance

- Vocabulario completo de acciones (CP-302).
