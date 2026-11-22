---
id: CP-608
titulo: Madrugada NightHold (2550) y arranque con desfase horario en HabitanteModule
estado: pendiente
prioridad: P2
hito: F6
depende_de: [CP-201, CP-601]
tipo: code
complejidad: media
duracion_min: 120
sensibilidad: public
proyecto_destino: ambos (Corazón Pelican + Stardew Valley MORPG)
---

## Objetivo

Ajustar el reloj/integrador de Corazón Pelican durante la ventana `NightHold` (`01:50`–`06:00` reales en Pelican 4Ever) y estabilizar la entrada en escena cuando Pelican arranca recuperando rollovers pendientes en mitad de la noche.

## Contexto

Observado en código y en prueba en vivo (`05:13`, partida `CorazonDev_454975123`):
1. En `RealTimeClockModule`, durante `NightHold` (`01:50` a `06:00` reales, 4 horas), `Game1.timeOfDay` permanece congelado en `2550` mientras `Postfix_shouldTimePass` hace que `Game1.shouldTimePass()` devuelva `true`. `HabitanteModule.OnUpdateTicked` calcula `minutosJuego` a partir del tiempo real cada vez que `shouldTimePass()` es `true`, acumulando 240+ minutos virtuales de madrugada con `hora = 2550`.
2. Al arrancar una partida desfasada dentro de `NightHold`, el salto inicial de reloj dispara en el mismo instante hitos atrasados y `acostarse`, reclamando al NPC mientras `CatchUpVillagerSchedules` aún intenta recolocar el pueblo.

## Alcance

- **Lado Pelican 4Ever (`Stardew Valley MORPG` · `TASK-2603`):** opcionalmente exponer `IsNightHold()` en `IPelicanApi` (`PelicanApi.cs`) junto con la hora efectiva de display o si hay rollover pendiente en curso (`DailyRolloverModule.IsBusy`).
- **Lado Corazón Pelican:**
  - Política explícita en `HabitanteModule` / `Core` cuando el reloj está en `NightHold` (`hora == 2550` sostenida o `IPelicanApi.IsNightHold()`): acotar/pausar acumulación de deriva y no re-disparar hitos diurnos atrasados por un salto inicial de arranque.
  - En `ObservadorJuego` / `HabitanteModule`, distinguir entre transición horaria continua y salto inicial tras `SaveLoaded`.

## Criterios de aceptación

- [ ] Tests en `CorazonPelican.Tests`: mantener la observación estable en `hora = 2550` (`NightHold`) no desborda el integrador ni repite hitos diurnos pendientes tras el primer pulso de cierre del día.
- [ ] Tests en `CorazonPelican.Tests`: un salto inicial de `600 -> 2550` justo tras `SaveLoaded` (caso de arranque con recuperación Pelican) no emite ráfagas duplicadas de pulsos en el mismo tick.

## Fuera de alcance

- Cambiar las horas reales de `NightHoldStartTime` o `CalendarDayChangeTime` de Pelican 4Ever.
