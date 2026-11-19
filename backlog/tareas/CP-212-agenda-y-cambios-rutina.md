---
id: CP-212
titulo: Agenda del día y validador de cambios de rutina en Core (R-16)
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-003, CP-201]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-16]
---

## Objetivo

Modelar la agenda diaria en Core (`AgendaDia`) y validar `cambios_rutina` contra el horario vigente y las anotaciones/límites del perfil (GSS §5.3, R-16), desbloqueando el pensamiento en cascada (`CP-209`) sin depender del spike en vivo `CP-011`.

## Contexto

GSS §4.1 y §5.3; ADS §2 (`HitosRutina.cs`) y §3.4. Hasta ahora `Rutina.cs` y la guarda `CambioRutinaTests` (R-16) vivían dentro de `CP-306` (F3), bloqueados por el spike en vivo `CP-011` 🎮. Sin embargo, `CP-209` (F2) ya exige que la cadena de despertar construya una agenda válida y pase R-16. Extraer el modelo y validador puros a Core permite probar R-16 y las cadenas en F2 con `FakeWorld`.

## Alcance

- `CorazonPelican.Core/Intencion/Rutina.cs` (`AgendaDia` y `ValidadorCambiosRutina`):
  - Construye la agenda inicial del día a partir de `HorarioDia` y `Rutina` del perfil (`HitosRutina`).
  - Valida `CambioRutina` (`saltar`, `mover`, `sustituir`) y la lista `agenda` propuesta al despertar o al reequilibrar un hito: el hito debe existir en el horario del día, la anotación del perfil debe permitir ese tipo de cambio, respetando `limites.max_por_semana` (con historial de la semana) y `limites.nunca` (`festival`, `lluvia`, `boda`), y sin coordenadas ni cambios sobre `despertar`/`acostarse`.
  - Devuelve `MotivoRechazo` (`codigo`, `campo`, `prosa`) si un cambio no es válido.
  - Aplica cambios válidos sobre la agenda vigente del día sin afectar a los hitos posteriores ni al día siguiente (al nuevo día vuelve la rutina canónica).

## Criterios de aceptación

- [ ] `CambioRutinaTests` (guarda de **R-16**): un cambio sobre un hito sin `cambios`, de un tipo no permitido, en una condición prohibida (`festival`, `lluvia`, `boda`) o fuera de `max_por_semana` se rechaza con `MotivoRechazo` en dos formas (`codigo`, `campo`, `prosa`).
- [ ] El validador rechaza cambios sobre `despertar`/`acostarse` o sobre hitos inexistentes en el horario de hoy.
- [ ] FakeWorld: saltar o sustituir un hito no altera los posteriores de la agenda, y al cambiar al día siguiente la agenda parte de la rutina canónica.

## Fuera de alcance

- Suspender/reincorporar `NPC.Schedule` dentro de Stardew Valley y degradación física del cuerpo (`CP-011`, `CP-303`, `CP-306`).
