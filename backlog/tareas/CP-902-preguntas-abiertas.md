---
id: CP-902
titulo: Decidir los pendientes de docs/decisiones.md (P-5…P-7, GSS §14 y otros)
estado: en-curso
prioridad: P2
hito: D
depende_de: []
tipo: planning
complejidad: media
duracion_min: 60
sensibilidad: public
---

## Objetivo

Que cada punto pendiente de [`docs/decisiones.md`](../../docs/decisiones.md) §2 tenga decisión, o una fecha y una tarea que la responda.

## Contexto

Son decisiones del autor del diseño. El registro de decisiones y pendientes vive en [`docs/decisiones.md`](../../docs/decisiones.md).

## Alcance

- Repasar la tabla de pendientes con el autor y decidir cada fila.
- Llevar cada decisión a la GSS, la ADS o una ADR, y ajustar las tareas afectadas.

## Acuerdos del 2026-11-19

- P-6. El diario se persiste desde F1. CP-107 lo verifica con FakeWorld, CP-108 en la partida DEV y CP-403 extiende la guarda a las obras.
- P-12. El motor dispone de una bolsa extra diaria para compromisos. Si se agota, Leah pide una prórroga y solo el jugador destinatario puede aceptarla. Queda repartido entre CP-202 (presupuesto), CP-502 (compromiso) y CP-509 (carta al destinatario ausente, aceptación o rechazo antes del vencimiento y lectura posterior).
- P-9. La mente interpreta lo vivido según el carácter y los recuerdos. El motor conserva y limita el humor que puede influir durante varios días en la conducta. CP-210 prepara su verificación.

## Acuerdos del 2026-11-21

- **P-5.** Espera en el diálogo medida en paciencia real (la `DialogueBox` del reflejo para el reloj en un jugador y no en multijugador; `CP-503`).
- **P-7.** Interceptar conversación (`NPC.checkAction`) sin sustituir amistad diaria, misiones, regalos, cónyuge ni eventos (`CP-010`, `CP-503`).
- **P-10.** Velocidad de consolidación del búfer y fuga de la huella como parámetros opcionales en el perfil de personaje con los valores por defecto del motor (`CP-111`).
- **P-13 (GSS §14.1–§14.3).** La generación de obras se pospone a **Fase 2**, comenzando primero por **Elliott** (obra en texto, `CP-410`) y siguiendo después con las obras visuales de Leah (`CP-013`, `CP-015`, `CP-401`–`CP-406`). Sin efectos mecánicos en v1; venta a precio fijo por formato en Fase 2. `CP-508` deja de depender de `CP-406`.
- **P-14 (GSS §14.4–§14.5).** En v1 el personaje no inicia un diálogo completo solo (sí puede acercarse + `murmurar`/`gesto` según apertura). Cambios de rutina con efecto mecánico quedan para v2.
- **Dr4co Core:** retirada la fila «Inscripción en Dr4co Core» de [`docs/decisiones.md`](../../docs/decisiones.md) §2 por tratarse de un plugin de OpenClaw ajeno a este proyecto.

Único punto pendiente para cerrar `CP-902`: confirmar **`UniqueID`** y **`Author`** del mod en [`CorazonPelican/manifest.json`](../../CorazonPelican/manifest.json) (actualmente `Dr4co.CorazonPelican` / `Dr4co`).

## Criterios de aceptación

- [ ] Cada fila de `docs/decisiones.md` §2 está decidida (y pasa a §1) o tiene tarea y fecha. *(Pendiente solo confirmación de `UniqueID`/`Author` del mod).*
- [x] Las decisiones aceptadas actualizan los documentos y las tareas afectadas en el mismo cambio. (`2026-11-21`: `README.md`, `design.md`, `architecture.md`, `docs/decisiones.md`, `CP-013`, `CP-015`, `CP-401`–`CP-406`, `CP-410`, `CP-508`, `CP-111`, `backlog/README.md`).

## Fuera de alcance

- Implementación.
