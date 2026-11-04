---
id: CP-002
titulo: Contrato v1 en JSON Schema, con época de mundo, validado desde C# y Python
estado: pendiente
prioridad: P1
hito: F0
depende_de: [CP-001]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-13]
---

## Objetivo

`schemas/` es la fuente única del contrato motor ↔ mente (ADR-0005) y los dos lados lo validan en sus tests.

## Contexto

ADS §3.3–3.5 y §5. Decisión P-1 (ADS §5): los `id` de pulso y de obra pueden repetirse tras recargar una partida, porque la memoria retrocede con el guardado pero la mente conserva su cursor. Cada carga de mundo genera una `epoca`: identificador opaco y único (p. ej. ULID), no persistido en la partida y que no se repite entre procesos. Va en `salud`, en cada pulso y en cada respuesta.

## Alcance

- `pulso`, `respuesta`, `obra` y `perfil` en JSON Schema (draft 2020-12).
- `epoca` en pulso, respuesta y `GET salud`; una respuesta de otra época se rechaza con motivo `epoca_obsoleta`.
- `motivo` del pulso como enumerado: `pico`, `interlocucion`, `deriva`, `hito`, `fin_intencion`, `compromiso`, `transicion` (GSS §4.1); los pulsos de hito llevan `hito: { id, origen, forma }`.
- `cambios_rutina` en la intención (`saltar`, `mover`, `sustituir`), sin coordenadas (ADS §3.4).
- `agenda` del día en el pulso (hitos de hoy con su estado) y en la respuesta a `despertar`.
- `pensamiento` como **primer** campo de la respuesta (prosa, nunca ejecutado) y separación explícita entre campos de prosa y campos efectivos (ADS §3.4, P-11).
- Apéndice con pares id — prosa: `lugares`, entidades, hitos de la agenda y obras.
- Motivos de rechazo con `codigo`, `campo` y `prosa` (ADS §4.1).
- `huella` de atención en el pulso (tiempo desde el último pensamiento, prosa y entidades con su atención; ADS §3.3).
- `plan` de la intención sin coordenadas: solo acciones del vocabulario con argumentos del léxico (R-13).
- DTOs en `Core/Contrato` y modelos Python (`pydantic` o `jsonschema`) contra los mismos ficheros.
- Ejemplos válidos e inválidos en `schemas/ejemplos/`.

## Criterios de aceptación

- [ ] Los ejemplos de la ADS §3.3–3.5 validan contra los esquemas (C# y Python).
- [ ] `EsquemaIntencionTests`: un `plan` con `x`, `y`, `tile` o `casilla` es inválido en ambos lados.
- [ ] Un ejemplo de respuesta con `epoca` distinta a la del pulso se rechaza en el validador de Core.
- [ ] Cambiar un campo obligatorio en un esquema rompe al menos un test en cada lenguaje.
- [ ] Test de orden: `pensamiento` es la primera propiedad de la respuesta en el esquema.

## Fuera de alcance

- Servidor HTTP (CP-203). Campos de la API v2.
