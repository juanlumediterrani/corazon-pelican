---
id: CP-508
titulo: Aceptación de fase 5 · una promesa hecha hoy y cumplida mañana 🎮
estado: pendiente
prioridad: P2
hito: F5
depende_de: [CP-502, CP-504, CP-505, CP-506, CP-507, CP-509, CP-305]
tipo: review
complejidad: media
duracion_min: 150
sensibilidad: public
vivo: si
---

## Objetivo

Probar el diálogo con consecuencias en v1 (`P-13`, sin depender todavía de generación de obras PNG de Fase 2). Alguien que conversa con Leah le plantea una promesa o cita; si ella acepta, al día siguiente la cumple en el mundo o gestiona la prórroga si corresponde.

## Contexto

GSS §1, ADS §14 fase 5 y acuerdo [`P-13`](../../docs/decisiones.md) (`2026-11-21`). La guarda `ActoRegalaTests` (`R-08`) queda verificada en las suites de Core/actos (`CP-501`) para obras existentes del portafolio; la generación de nuevas obras en vivo entra en Fase 2 (`CP-410` y `CP-406`).

## Criterios de aceptación

- [ ] `backlog/reportes/CP-508.md` con la conversación, el compromiso acordado (sin requerir obra PNG generada en taller), la intención resultante y su cumplimiento al día siguiente en vivo.
- [ ] Con ambos cupos agotados y el destinatario ausente, recibir una carta antes del vencimiento y aceptar la nueva fecha. En un segundo caso, dejar que venza el plazo, abrir la carta y comprobar que se lee sin opción de responder. Incluir los plazos y resultados en el informe.
- [ ] R-07, R-08 (guarda en Core), R-09, R-12 y R-15 con tests verdes citados.
- [ ] Puntuación con la rúbrica de CP-205 de toda la conversación.
- [ ] Prueba negativa: con 1 corazón Leah rechaza la petición o encargo de forma creíble.

## Fuera de alcance

- Pelican y Voyager (F6).
