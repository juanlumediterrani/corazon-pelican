# ADR-0003 · El motor valida los actos antes de mostrar la frase

* **Estado:** Aceptado
* **Fecha:** 2026-11-19
* **Ámbito:** `CorazonPelican.Core/Dialogo/**`, `CorazonPelican/Modules/{DialogoModule,CorreoProrrogaModule}.cs`, `schemas/respuesta.schema.json`

## Contexto

Leah puede ofrecer un cuadro que estaba disponible al empezar a pensar y que ya entregó cuando llega la respuesta. Mostrar la oferta en ese instante crearía una promesa imposible.

## Decisión

El motor comprueba el conjunto de actos contra el estado vigente. Si todos son válidos, aplica intención, actos y memoria antes de mostrar la frase. Ante un rechazo devuelve el campo, el código y el motivo en prosa. La mente dispone de una reformulación. Un segundo fallo da paso al diálogo vanilla.

## Consecuencias

Un regalo inválido en una respuesta con dos actos impide también aplicar el acto válido. CP-501 probará ese caso y comprobará la disponibilidad de la obra. CP-503 llevará el resultado a la interfaz de diálogo.

## Alternativas descartadas

Mostrar la frase en cuanto llega o aplicar cada acto según se valida dejaría al jugador con una oferta que el mundo solo cumplió en parte.

## Verificación

[R-07 y R-08](../../design.md#13-reglas-verificables). [CP-501](../../backlog/tareas/CP-501-actos-validador.md), [CP-503](../../backlog/tareas/CP-503-dialogo-intercepcion.md).
