---
id: CP-207
titulo: Presupuesto de tokens, caché de prompt y telemetría de costes
estado: pendiente
prioridad: P2
hito: F2
depende_de: [CP-204]
tipo: code-light
complejidad: baja
duracion_min: 90
sensibilidad: public
---

## Objetivo

La segunda barrera de coste: tokens por día real, con métricas de caché y coste en `costes.jsonl`.

## Contexto

ADS §6 y §12.2. Subordinado al presupuesto de pulsos del motor (CP-202). Las cadenas hacen varias llamadas por pulso: su coste se registra aparte.

## Alcance

- Contabilidad por llamada: tokens de entrada, caché, salida y coste estimado.
- Al agotarse, la mente responde solo a interlocución o deja que el motor degrade.

## Criterios de aceptación

- [ ] `costes.jsonl` registra cada llamada con época, pulso y modelo.
- [ ] Con presupuesto agotado (FakeLLM), los pulsos no interlocutorios no llegan al modelo.
- [ ] Informe de coste estimado por día de juego vanilla y por día Pelican con el modelo elegido.

## Fuera de alcance

- Modelo local para reflejos.
