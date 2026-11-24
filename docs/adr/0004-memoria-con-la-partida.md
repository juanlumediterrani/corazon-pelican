# ADR-0004 · La memoria del personaje viaja con la partida

* **Estado:** Aceptado
* **Fecha:** 2026-11-19
* **Ámbito:** `CorazonPelican.Core/Memoria/**`, `CorazonPelican/Modules/PersistenciaModule.cs`

## Contexto

Si una sesión se cierra antes de guardar, el valle retrocede. Recordar en la siguiente sesión una promesa o un regalo de ese tramo rompería la continuidad.

## Decisión

El host custodia el diario, los recuerdos, las relaciones, los compromisos y el portafolio. Escribe el estado con la partida mediante WriteSaveData y lo restaura al cargar. La mente reconstruye su caché desde la memoria del motor.

## Consecuencias

Un episodio y una obra posteriores al último guardado desaparecen al recargar. Las obras guardadas recuperan su textura. Una reflexión que llegue después de guardar quedará expuesta a la misma pérdida hasta el siguiente guardado.

## Alternativas descartadas

Guardar la biografía en un fichero de la mente o reconstruirla desde telemetría conservaría sucesos de una sesión que la partida ya descartó.

## Verificación

[R-10](../../design.md#13-reglas-verificables). [CP-107](../../backlog/tareas/CP-107-persistencia-estado.md), [CP-209](../../backlog/tareas/CP-209-cadenas-despertar-acostarse.md), [CP-403](../../backlog/tareas/CP-403-obras-y-portafolio.md).
