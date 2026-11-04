---
id: CP-402
titulo: Decisión de obra y autocrítica de la mente
estado: pendiente
prioridad: P2
hito: F4
depende_de: [CP-401, CP-204]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
---

## Objetivo

La mente decide tema, composición, título, paleta y nota desde la escena y la memoria, mira el resultado y lo acepta o lo repite (máx. 2).

## Contexto

GSS §6.1. Descartar bocetos es canónico; la nota de cómo se siente con el resultado es parte de la obra.

## Alcance

- Esquema `obra` con decisión y nota.
- Llamada con imagen para la autocrítica; registro de intentos.
- Si el sprite no llega antes de caducar la intención, la obra queda como boceto (ADS §7).

## Criterios de aceptación

- [ ] pytest con FakeLLM: tras 2 repeticiones rechazadas la obra queda `descartada` o `boceto`, nunca en bucle.
- [ ] Toda decisión cita al menos un elemento de la escena de origen.
- [ ] La autocrítica queda en el portafolio como nota de la obra.

## Fuera de alcance

- Venta y precio (pregunta abierta, CP-902).
