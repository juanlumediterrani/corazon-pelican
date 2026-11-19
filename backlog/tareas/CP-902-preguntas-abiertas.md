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

Son decisiones del autor del diseño. El registro, con una recomendación para cada punto, vive en `docs/decisiones.md` §2: propuestas P-5, P-7 y P-10; preguntas abiertas de la GSS §14 (obras con efecto, venta, segundo personaje, iniciativa de Leah, cambios de rutina con efecto mecánico); inscripción en Dr4co Core y confirmación del UniqueID que CP-001 ya usa en el manifiesto.

## Alcance

- Repasar la tabla de pendientes con el autor y decidir cada fila.
- Llevar cada decisión a la GSS, la ADS o una ADR, y ajustar las tareas afectadas.

## Acuerdos del 2026-11-19

- P-6. El diario se persiste desde F1. CP-107 lo verifica con FakeWorld, CP-108 en la partida DEV y CP-403 extiende la guarda a las obras.
- P-12. El motor dispone de una bolsa extra diaria para compromisos. Si se agota, Leah pide una prórroga y solo el jugador destinatario puede aceptarla. Queda repartido entre CP-202 (presupuesto), CP-502 (compromiso) y CP-509 (carta al destinatario ausente, aceptación o rechazo antes del vencimiento y lectura posterior).
- P-9. La mente interpreta lo vivido según el carácter y los recuerdos. El motor conserva y limita el humor que puede influir durante varios días en la conducta. CP-210 prepara su verificación.

Los demás pendientes siguen en el registro de decisiones. Las guardas de P-6 y P-12 corresponden a las tareas de implementación.

## Criterios de aceptación

- [ ] Cada fila de `docs/decisiones.md` §2 está decidida (y pasa a §1) o tiene tarea y fecha.
- [ ] Las decisiones aceptadas actualizan los documentos y las tareas afectadas en el mismo cambio.

## Fuera de alcance

- Implementación.
