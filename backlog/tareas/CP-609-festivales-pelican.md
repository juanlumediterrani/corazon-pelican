---
id: CP-609
titulo: Festivales reprogramados y ventana prime-time (FestivalAccessModule / WorldLifeModule)
estado: hecha
fecha: 2026-11-22
prioridad: P2
hito: F6
depende_de: [CP-306, CP-601]
tipo: code
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

Alinear la detección de festivales, los límites de agenda (`nunca: ["festival"]`) y las guardas de `DialogoModule` con los festivales reprogramados y en ventana prime-time (`18:00–23:50`) de Pelican 4Ever.

## Contexto

En Pelican 4Ever:
- `WorldLifeModule.OnAssetRequested` modifica `Data/Festivals/FestivalDates` según `RescheduledFestivals` y reescribe las condiciones de `Data/Festivals/*` para mover la ventana a `FestivalWindowStart..FestivalWindowEnd` (`1800–2350`).
- `FestivalAccessModule` permite entrada/salida individual al mapa temporal del festival sin congelar al anfitrión en `Game1.isFestival()` durante todo el día.
Sin esta adaptación, `DialogoModule` y `AgendaDia` en Corazón Pelican solo miran `Game1.CurrentEvent != null || Game1.isFestival()`, desincronizando qué días/horas se consideran festival frente a la agenda de Leah y lo que ven los jugadores.

## Alcance

- Leer en el adaptador de horario/calendario si la fecha actual figura en `Data/Festivals/FestivalDates` (que ya incluye las reprogramaciones de Pelican) y su ventana `conditions`, exponiendo contexto de festival del día al motor.
- Definir el criterio para `DialogoModule` y `Disparadores` cuando haya un evento `festival.*` activo o cuando el jugador interactúe dentro vs fuera del mapa de festival en Pelican.

## Criterios de aceptación

- [x] Tests en `CorazonPelican.Tests`: en una fecha movida vía `FestivalDates`, el motor detecta que es día de festival (y aplica `nunca: ["festival"]` en `R-16`), mientras en la fecha original ya vaciada la trata como día normal. (`FestivalesPelicanTests.Detecta_festival_reprogramado_en_FestivalDates_y_aplica_nunca_festival_R16_CP609`, `418/418` superados; `CalendarioFestivales` + `HabitanteModule.EsDiaFestivalEnCalendario`)
- [x] En ventana no festival antes de `FestivalWindowStart` (p. ej. `12:00` en día de festival prime-time de Pelican), Leah conserva comportamiento coherente con su mapa real hasta la ventana del festival. (`CalendarioFestivales.ParsearVentanaFestival` + `EstaEnVentanaFestival` cubiertos en `FestivalesPelicanTests`, y `DialogoModule` mantiene conversación ordinaria fuera de evento activo)

## Fuera de alcance

- Reescribir los guiones de `FestivalAccessModule` o `FestivalParticipationModule` en Pelican 4Ever.
