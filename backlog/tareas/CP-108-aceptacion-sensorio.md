---
id: CP-108
titulo: Aceptación de fase 1 · un día completo de escenas legibles 🎮
estado: pendiente
prioridad: P2
hito: F1
depende_de: [CP-101, CP-104, CP-107, CP-006]
tipo: review
complejidad: media
duracion_min: 120
sensibilidad: public
vivo: si
---

## Objetivo

Con `MenteHabilitada=false`, Leah vive un día vanilla mientras el motor escribe escenas y diario que una persona lee y juzga.

## Contexto

ADS §14 fase 1. Es la primera prueba de los pilares 1 y 2 sin gastar un token. Aquí se ajustan pesos y plantillas.

## Alcance

- Un día completo en DEV con telemetría `escenas.jsonl`.
- Informe con muestras por tramo horario, costes por paso medidos y problemas de monotonía.
- Ajustes de pesos/plantillas resultantes (en commits de esta tarea).

## Criterios de aceptación

- [ ] Informe `backlog/reportes/CP-108.md` con al menos 10 escenas de distintos tramos y su valoración.
- [ ] Costes medidos por paso frente a los objetivos de ADS §4.
- [ ] R-01, R-02, R-03 y R-14 tienen test verde citado.

## Fuera de alcance

- Cualquier llamada a un LLM.
