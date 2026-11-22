---
id: CP-208
titulo: Aceptación de fase 2 · medir la cadencia de pensamiento en vanilla y en Pelican 🎮
estado: en-curso
prioridad: P2
hito: F2
depende_de: [CP-202, CP-204, CP-206, CP-207, CP-209, CP-210, CP-108]
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

Hallazgos de CP-201 con el perfil actual de Leah (a resolver aquí):

- En una escena tranquila y ya habituada, las saliencias menores (piso de novedad 0,3) siguen sumando lo bastante para que la **deriva salte en cuanto acaba cada ventana refractaria** (30 s reales ≈ 43 min de juego en vanilla). Consecuencia: casi todos los hitos omitibles se omiten y la cadencia queda marcada por el refractario, no por la rutina.
- El refractario multiplica el umbral de pico por hasta 1,5: durante ~45 s tras cualquier pulsión el pico es imposible (la saliencia es < 1), y con la deriva disparando cada 30 s, **el pico casi nunca llega**. Se suma al hallazgo de CP-102 (con los umbrales actuales el pico ya era difícil).
- Palancas: umbral y semivida de la deriva, piso de novedad, ventana y subida del refractario, y si la deriva debería integrar solo la parte novedosa de la saliencia.

## Alcance

- Un día vanilla completo con mente real; telemetría de pulsos, respuestas y costes.
- Una sesión con Pelican 4Ever de al menos 12 h reales (puede ser con el jugador ausente) para medir deriva e hitos a reloj 1:1.
- Ajuste de umbrales, fuga y presupuesto por modo a partir de las medidas.
- Puntuación con la rúbrica de una muestra de 15 pensamientos.

## Criterios de aceptación

- [x] `backlog/reportes/CP-208.md` con, por modo: pulsos por motivo, intervalo entre pensamientos (mediana y p90) frente a la expectativa, coste total y puntuaciones. (`CadenciaAceptacionTests` tras ajustar `deriva=4.5, deriva_semivida_min=4`: Vanilla `2.08 min`, Pelican simulado 12h `179.83 min`; falta únicamente corrida continua de 12 h reales de reloj en Pelican 4Ever)
- [x] Gasto ordinario y extra de compromisos por día dentro de sus respectivos cupos (R-04), con interlocuciones contabilizadas aparte y tick sin bloqueos con mente lenta (R-05), con tests citados. (`PresupuestoPulsosTests`, `CadenciaAceptacionTests`, `CuerpoNoBloqueaTests`)
- [x] Dentro de las 15 muestras, seguir un tramo sin interlocución ni encargos. Conservar el pulso de origen, el recuerdo o detalle atendido y la intención que propone la mente real. Comparar con un tramo tranquilo y comprobar si la deriva repetida al salir del refractario ahoga las decisiones de rutina. La ejecución corporal se revisa en CP-305. (`backlog/reportes/CP-208.md` §1–§3: ajuste de `deriva=4.5` y `deriva_semivida_min=4` evita que la deriva ahogue los hitos)
- [x] Cada día medido tiene una cadena de despertar y una de acostarse completas. (`pulsos.jsonl` en vivo y `CadenasHitosTests`)
- [x] Revisar dos muestras comparables con la mente real y experiencias previas distintas (P-9). Identificar si el humor conservado ayuda a explicar una preferencia pequeña. Anotar también una muestra plana o incoherente si la hay. (`backlog/reportes/CP-208.md` §3 muestras 8 y 9 con `eu.amazon.nova-2-lite-v1:0`)
- [x] Evaluar el ciclo en vivo con un modelo real y credenciales sobre el transporte HTTP conectado en [CP-215](CP-215-conectar-proveedor-de-la-mente.md). (`CicloMente` con AWS Bedrock `eu.amazon.nova-2-lite-v1:0` sobre `127.0.0.1:25560`)
- [x] Ningún pensamiento menciona entidades fuera de escena o memoria en la muestra revisada. (`rechazos.jsonl`: `tasa_no_ancladas = 0`)

## Fuera de alcance

- Que Leah actúe (F3).
