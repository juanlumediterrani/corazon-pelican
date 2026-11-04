---
id: CP-301
titulo: Intención con ciclo de vida, pesos de atención y validador de precondiciones
estado: pendiente
prioridad: P1
hito: F3
depende_de: [CP-105, CP-203]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-06, R-13]
---

## Objetivo

Las intenciones de la mente se validan contra la observación vigente, modulan la saliencia y terminan siempre en un estado final.

## Contexto

GSS §5.1, ADS §4.1. La capa de intención de la saliencia (CP-102) deja de valer 1.

## Alcance

- `Intencion.cs`: propuesta → validada → activa → cumplida | bloqueada | caducada | sustituida; rechazada.
- `ValidadorIntencion.cs`: lugares del léxico alcanzables, acciones del vocabulario, plazos futuros.
- Fin de intención dispara pulsión (CP-201).

## Criterios de aceptación

- [ ] `ValidadorIntencionTests`: lugar inexistente, plazo pasado y acción fuera del perfil se rechazan con motivo (R-06), cada uno con `codigo`, `campo` y `prosa` legible por el personaje.
- [ ] `EsquemaIntencionTests` en verde también contra el validador de Core (R-13).
- [ ] Una intención activa con plazo vencido pasa a caducada y emite pulsión de fin.

## Fuera de alcance

- Ejecución física (CP-302).
