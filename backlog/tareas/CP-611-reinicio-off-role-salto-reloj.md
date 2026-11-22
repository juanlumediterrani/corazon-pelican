---
id: CP-611
titulo: Tratamiento técnico off-role ante saltos de reloj (/time / p4e_time) en Pelican 4Ever
estado: hecha
fecha: 2026-11-22
prioridad: P1
hito: F6
depende_de: [CP-606, CP-610]
tipo: code
complejidad: media
duracion_min: 90
sensibilidad: public
proyecto_destino: ambos (Corazón Pelican + Stardew Valley MORPG)
---

## Objetivo

Preparar la intersección administrativa «off-role» cuando un operador fuerza el reloj del servidor (`p4e_time` / `/time`) en Pelican 4Ever: emitir advertencia/confirmación previa cuando pueda perder o mezclar progreso (especialmente con otros jugadores conectados o NPCs externos activos) y aplicar un reinicio técnico completo al NPC gobernado para que `CatchUpVillagerSchedules` lo reposicione en su ubicación canónica de la nueva hora y comience una instancia limpia.

## Alcance

- **Stardew Valley MORPG (`TASK-2605`):**
  - `TimeCommandConfirmationGate` y `TimeCommandParser.ParseWithConfirmation`: advertir antes de ejecutar `/time` o `p4e_time` sobre la naturaleza *off-role* de la acción (jugadores conectados, pérdida/mezcla de progreso diario, reinicio de NPCs externos activos), exigiendo repetición del mismo comando en 45 s o sufijo `confirm` / `confirmar` (también aceptado en `RemoteCommandCatalog`).
  - `RealTimeClockModule.TimeJumpHooks` + `PelicanApi.RegisterTimeJumpHook` / `UnregisterTimeJumpHook`: disparar callbacks síncronos `(horaAnterior, horaNueva)` al aplicar `AdjustTime` justo **antes** de `CatchUpVillagerSchedules(Game1.timeOfDay)`.
- **Corazón Pelican (`CP-611`):**
  - `IPelicanApi.RegisterTimeJumpHook` / `UnregisterTimeJumpHook`.
  - `MotorHabitante.RestablecerOffRole` + `Disparadores.ReiniciarOffRole` + `PresupuestoPulsos.ReiniciarDia` + `Refractario.Reiniciar`: cortar intención y plan en curso, cancelar diálogos abiertos (`DialogoModule.CancelarTurnosActivos`), limpiar búfer/disparadores/refractario/presupuesto del día y liberar `CorazonPelican.Control` con `ReclamacionNpc.LiberarParaReubicacion` sin lanzar ruta hacia el tramo anterior.
  - `HabitanteModule.EnSaltoHorarioPelican`: rotar época en `ApiMotor.IniciarEpoca(...)` (rechazando con `409` / `epoca_obsoleta` respuestas en vuelo de la hora anterior y resincronizando `mente/`) y ejecutar `RestablecerOffRole` justo antes de que Pelican recoloque a Leah en su tramo horario vigente.

## Criterios de aceptación

- [x] `Pelican4Ever.Tests`: `TimeCommandConfirmationTests` verifica la advertencia previa, confirmación explícita (`confirmar`) o por repetición en ventana de 45 s, catálogo remoto `p4e_time` y `TimeJumpHookRegistry` (`1840/1840` superados).
- [x] `CorazonPelican.Tests`: `ReinicioOffRoleCP611Tests` verifica el corte completo de plan/intención/diálogo/presupuesto, liberación de control sin ruta previa y descarte `epoca_obsoleta` al rotar época tras un salto horario (`421/421` superados).
- [x] Compilación limpia (`0` advertencias, `0` errores) en ambos repositorios y `pytest` en verde (`117 passed`).
