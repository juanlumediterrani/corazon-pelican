---
id: CP-105
titulo: Léxico único compartido por percepción, acción y diálogo
estado: pendiente
prioridad: P2
hito: F1
depende_de: [CP-003]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
reglas: [R-14]
---

## Objetivo

Un único registro de nombres (lugares, objetos, acciones) que usan la escena, el validador de intenciones y el de actos.

## Contexto

GSS §3.5. Si la escena dice `pasarela_lago`, `ir_a(pasarela_lago)` es válido y Leah puede citar allí.

## Alcance

- `Lexico` en Core construido desde el perfil; resolución id → área/prosa.
- Los generadores de escena y los validadores reciben el mismo objeto, no copias.

## Criterios de aceptación

- [ ] `LexicoUnicoTests`: todo lugar del apéndice de escena es aceptado por `ir_a` y por `cita`, y viceversa.
- [ ] Un id duplicado o con mayúsculas/espacios lo rechaza el validador del perfil.

## Fuera de alcance

- Léxico de objetos dinámicos del inventario (CP-504).
