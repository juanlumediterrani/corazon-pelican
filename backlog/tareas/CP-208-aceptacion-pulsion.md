---
id: CP-208
titulo: Aceptación de fase 2 · medir la cadencia de pensamiento en vanilla y en Pelican 🎮
estado: pendiente
prioridad: P2
hito: F2
depende_de: [CP-202, CP-204, CP-206, CP-207, CP-209, CP-108]
tipo: review
complejidad: media
duracion_min: 120
sensibilidad: public
vivo: si
---

## Objetivo

Medir qué cadencia de pensamiento dan deriva, hitos y el resto de disparadores, y comprobar que los pensamientos se leen como Leah.

## Contexto

ADS §14 fase 2 y GSS §7 (decisión P-2): la cadencia se mide, no se programa. Expectativa de partida: del orden de un pensamiento cada 5–15 min reales en vanilla y cada 4–6 h en Pelican, más interrupciones puntuales y las dos cadenas del día. Si la medida se aleja, se ajustan umbrales, fuga y presupuesto; no se añade un temporizador. Primera aplicación de la rúbrica de CP-205.

## Alcance

- Un día vanilla completo con mente real; telemetría de pulsos, respuestas y costes.
- Una sesión con Pelican 4Ever de al menos 12 h reales (puede ser con el jugador ausente) para medir deriva e hitos a reloj 1:1.
- Ajuste de umbrales, fuga y presupuesto por modo a partir de las medidas.
- Puntuación con la rúbrica de una muestra de 15 pensamientos.

## Criterios de aceptación

- [ ] `backlog/reportes/CP-208.md` con, por modo: pulsos por motivo, intervalo entre pensamientos (mediana y p90) frente a la expectativa, coste total y puntuaciones.
- [ ] Pulsos/día ≤ presupuesto (R-04) y tick sin bloqueos con mente lenta (R-05), con tests citados.
- [ ] Cada día medido tiene una cadena de despertar y una de acostarse completas.
- [ ] Ningún pensamiento menciona entidades fuera de escena o memoria en la muestra revisada.

## Fuera de alcance

- Que Leah actúe (F3).
