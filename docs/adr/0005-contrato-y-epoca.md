# ADR-0005 · Un solo contrato y una época por carga de mundo

* **Estado:** Aceptado
* **Fecha:** 2026-11-19
* **Ámbito:** `schemas/*.schema.json`, `CorazonPelican.Core/Contrato/**`, `mente/contrato.py`

## Contexto

Tras una recarga se puede volver a emitir el pulso 17 mientras la mente todavía prepara una respuesta al pulso 17 anterior.

## Decisión

Los esquemas de schemas/ definen los mensajes que comparten C# y Python. Cada carga genera una época opaca y única. El motor comprueba la época antes de aplicar una respuesta. La pareja de época e id de pulso identifica un reintento.

## Consecuencias

CP-002 ya contrasta ejemplos en ambos lenguajes, rechaza coordenadas en los planes y prueba el rechazo epoca_obsoleta en Core. CP-203 llevará la época al transporte. CP-403 la comprobará también al recibir la textura de una obra.

## Alternativas descartadas

Mantener contratos independientes permitiría que una respuesta válida para Python fuese ilegible para C#. Identificar la petición solo por número de pulso confundiría sesiones después de recargar.

## Verificación

[R-13](../../design.md#13-reglas-verificables). [CP-002](../../backlog/tareas/CP-002-contrato-json-schema.md), [CP-203](../../backlog/tareas/CP-203-api-motor.md), [CP-403](../../backlog/tareas/CP-403-obras-y-portafolio.md).
