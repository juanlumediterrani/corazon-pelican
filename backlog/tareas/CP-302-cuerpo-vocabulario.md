---
id: CP-302
titulo: CuerpoNpc · vocabulario de acciones v1 sobre Leah 🎮
estado: pendiente
prioridad: P1
hito: F3
depende_de: [CP-011, CP-012, CP-301]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
vivo: si
---

## Objetivo

Leah ejecuta `ir_a`, `sentarse`, `mirar`, `levantar_la_vista`, `dibujar`, `guardar_cuaderno`, `gesto`, `murmurar` y `esperar` con medios nativos.

## Contexto

GSS §5.2. La mente decide qué; el cuerpo decide cómo. Mecanismos de CP-011 (rutas, reclamación) y CP-012 ([`docs/spikes/v07-animaciones.md`](../../docs/spikes/v07-animaciones.md)). Nativos disponibles: `doEmote`, `showTextAboveHead`, `PathFindController`, `StartActivityRouteEndBehavior` / `EndActivityRouteEndBehavior` (esperar `!doingEndOfRouteAnimation.Value` antes de mover para no dejar `freezeMotion = true`; en `sentarse`, sin frame de sentado en Leah, v1 usa `ir_a` + `Halt()` + orientación del lugar).

## Alcance

- Una clase por acción con inicio, avance por tick, fin y causa de bloqueo.
- Prosa de acción hacia el búfer en cada acción.
- `mirar`/`levantar_la_vista` cambian el foco del sensorio (GSS §3.6).
- Evento «plan terminado» al acabar la última acción con duración del plan, que alimenta el disparador de hito (CP-201).

## Criterios de aceptación

- [ ] FakeWorld: cada acción termina en cumplida o bloqueada con motivo; ninguna queda colgada más de su duración máxima.
- [ ] En vivo: `ir_a(orilla_norte)` desde la casa de Leah llega por rutas nativas sin teletransporte visible.
- [ ] En vivo: `dibujar` muestra la animación elegida en CP-012 y `guardar_cuaderno` la detiene.
- [ ] En vivo: «voy a sentarme en la pasarela del lago» lleva a Leah hasta el punto `sentable` de la pasarela, se detiene orientada al lugar (fallback v1 de `sentarse` según CP-012) y emite una pulsión de hito en telemetría.

## Fuera de alcance

- Reflejos automáticos (CP-304).
