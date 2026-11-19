---
id: CP-012
titulo: Spike V-07 · mapear las animaciones de Leah a las acciones del cuerpo
estado: hecha
prioridad: P2
hito: S
depende_de: [CP-001]
tipo: analysis
complejidad: baja
duracion_min: 45
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

Decidir qué animación nativa usa cada acción del cuerpo de Leah (`dibujar`, `guardar_cuaderno`, `sentarse`, gestos) y si sirven fuera de los sitios de su horario.

## Contexto

El inventario ya está hecho ([notas](../../docs/notas-juego.md) §4, leído con `tools/xnbdump`): `leah_draw` (bucle sobre los frames 32–35, reposo en 35), `leah_sculpt` (bucle en 19), `leah_sleep` (51) y `leah_beach_drink`. Su horario usa `leah_sculpt` cada mañana en casa y `leah_draw` en el bosque y en la playa. Leah es zurda. Queda el mapeo y comprobar orientaciones.

## Alcance

- Mapeo acción ↔ animación: `dibujar` → `leah_draw`; fin de dibujo y `guardar_cuaderno` → frame de reposo o salida de la animación; `sentarse` → lo que ofrezca su sprite (o declararlo ausente).
- Comprobar en qué orientaciones existe `leah_draw` (el horario la usa mirando a la izquierda) y qué pasa en otras.
- Cómo lanzar y detener una animación de `animationDescriptions` desde código sin pasar por el horario.

## Criterios de aceptación

- [x] `docs/spikes/v07-animaciones.md` con el mapeo y las orientaciones válidas. Evidencia: [`docs/spikes/v07-animaciones.md`](../../docs/spikes/v07-animaciones.md) §2 (orientaciones en `Characters/schedules/Leah` y frames de `Characters/Leah` / `Leah_Winter` / `Leah_Beach`) y §4 (mapeo completo del vocabulario v1, declarando `sentarse` ausente en sprite nativo).
- [x] Para `dibujar` se usa `leah_draw` o se declara por qué no y qué alternativa se usa. Evidencia: [`docs/spikes/v07-animaciones.md`](../../docs/spikes/v07-animaciones.md) §2.3 y §4 — se usa `leah_draw` (frames `32–35`, mirando abajo `facingDirection = 2`) fuera de su horario en todo el valle (`Characters/Leah` y `Characters/Leah_Winter`), excluyendo `Characters/Leah_Beach`.
- [x] Se documenta la llamada que inicia y detiene la animación (decompilado citado). Evidencia: [`docs/spikes/v07-animaciones.md`](../../docs/spikes/v07-animaciones.md) §3 — `NPC.StartActivityRouteEndBehavior("leah_draw", null)` (`NPC.cs:4617-4620`) y `NPC.EndActivityRouteEndBehavior()` (`NPC.cs:4612-4615`, `finishEndOfRouteAnimation` `L4161-4219`, `routeEndAnimationFinished` `L4276-4305`), más alternativa local `AnimatedSprite.setCurrentAnimation` / `StopAnimation` y advertencia sobre `freezeMotion` con `Halt()`.
- [x] El contenido del juego no se versiona en el repositorio. Evidencia: `git status` muestra únicamente `docs/spikes/v07-animaciones.md` y `backlog/tareas/CP-012-spike-animaciones-leah.md`.

## Fuera de alcance

- Sprites nuevos de Leah. Escultura como obra (fuera de v1).
