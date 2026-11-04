---
id: CP-204
titulo: Mente en Python que recibe pulsos y escribe pensamientos (sin cuerpo)
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-203, CP-205]
tipo: code-light
complejidad: media
duracion_min: 180
sensibilidad: public
---

## Objetivo

El proceso `mente/` sigue el bucle de ADS §6 y deja en telemetría qué pensaría Leah en cada pulso, sin que el motor aplique nada todavía.

## Contexto

ADS §6. Modelos: un modelo grande para pensamiento y diálogo; el prompt de sistema (voz + reglas) estable para la caché del proveedor.

## Alcance

- `cliente_motor.py` (long-poll, cursor + época, reanudación), `ensamblador.py`, `modelos.py`.
- `estrechar(respuesta.schema.json, pulso)`: esquema del pulso con listas cerradas de acciones, lugares, entidades, hitos y obras sacadas del propio pulso (ADS §6, P-11).
- Salida estructurada contra ese esquema, con `pensamiento` primero; un reintento si no valida.
- Reformulación a partir de la `prosa` de los motivos de rechazo.
- El motor guarda cada `pensamiento` en la telemetría y como entrada del diario.
- `FakeLLM` para tests.

## Criterios de aceptación

- [ ] pytest: con `FakeLLM`, un pulso produce una respuesta válida contra el esquema.
- [ ] pytest: una salida inválida provoca un reintento y luego un rechazo registrado, nunca un envío inválido.
- [ ] pytest: el esquema del pulso valida como subconjunto del general, y rechaza un lugar o una ref que no están en el pulso.
- [ ] pytest con FakeLLM: tras un rechazo, el prompt de reformulación contiene la prosa del motivo.
- [ ] Tras reiniciar la mente, retoma desde el cursor sin reprocesar pulsos; con época nueva, reinicia el cursor.
- [ ] El prefijo del prompt (sistema + voz) es idéntico entre dos pulsos distintos (test de caché).

## Fuera de alcance

- Recuperación de memoria y reflexión (CP-206).
