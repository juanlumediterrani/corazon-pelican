---
id: CP-002
titulo: Contrato v1 en JSON Schema, con época de mundo, validado desde C# y Python
estado: hecha
prioridad: P1
hito: F0
depende_de: [CP-001]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-13]
fecha: 2026-11-04
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

- [x] Los ejemplos de la ADS §3.3–3.5 validan contra los esquemas (C# y Python). Evidencia: `schemas/ejemplos/validos/` (8 ejemplos, uno por esquema raíz como mínimo); `ContratoTests.Ejemplo_valido_valida` y `test_ejemplos_validos_validan`; 14 ejemplos inválidos rechazados en ambos lados; ida y vuelta de los DTOs en `ContratoTests.Dto_conserva_el_ejemplo_y_sigue_validando`.
- [x] `EsquemaIntencionTests`: un `plan` con `x`, `y`, `tile` o `casilla` (y `ruta`) es inválido en ambos lados. Evidencia: `EsquemaIntencionTests.Plan_con_coordenadas_es_invalido` (C#) y `TestEsquemaIntencion` (Python), que también cubre un `sustituir` con coordenadas.
- [x] Un ejemplo de respuesta con `epoca` distinta a la del pulso se rechaza en el validador de Core. Evidencia: `ValidadorEpoca.Comprobar` → `epoca_obsoleta` con campo y prosa; `ValidadorEpocaTests` (3 casos).
- [x] Cambiar un campo obligatorio en un esquema rompe al menos un test en cada lenguaje. Evidencia: instantánea `schemas/ejemplos/obligatorios.json` comparada en ambos lados; mutación comprobada (quitar `basada_en` de `required` → «1 failed» en pytest y «Con error: 1» en xUnit; restaurado → verde).
- [x] Test de orden: `pensamiento` es la primera propiedad de la respuesta en el esquema. Evidencia: `Pensamiento_es_la_primera_propiedad_de_la_respuesta` y `test_pensamiento_es_la_primera_propiedad_de_la_respuesta`.

Verificación final: `dotnet build` 0 advertencias, `dotnet test` 45/45, `python3 -m pytest -q` 39 passed.

## Notas de cierre

- Esquemas añadidos además de los cuatro previstos: `comun` (definiciones compartidas), `resultado` (POST respuestas) y `salud` (GET salud).
- Python valida con `jsonschema` + `referencing` en `mente/contrato.py`; los modelos tipados (pydantic) se dejan para CP-204 si hacen falta.
- El DTO del perfil queda para CP-003 (`Perfil/PerfilPersonaje.cs`); aquí solo su esquema.
- JsonSchema.Net solo está en el proyecto de tests: Core sigue sin dependencias externas.

## Fuera de alcance

- Servidor HTTP (CP-203). Campos de la API v2.
