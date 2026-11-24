# ADR-0006 · Los sucesos y la rutina determinan cuándo pensar

* **Estado:** Aceptado
* **Fecha:** 2026-11-19
* **Ámbito:** `CorazonPelican.Core/Atencion/**`, `CorazonPelican/Modules/{HabitanteModule,IntegracionPelicanModule}.cs`

## Contexto

El mismo tramo del horario ocupa segundos en vanilla y horas en Pelican. Una consulta periódica al modelo interrumpiría incluso una tarde tranquila sin nada nuevo que atender.

## Decisión

Un pico, la deriva de estímulos menores, una transición, un interlocutor, un compromiso o el fin de un plan pueden disparar una pulsión. El horario elegido por el juego aporta los hitos del día. Despertar y acostarse abren sendas cadenas, cada una con una plaza reservada. El motor aplica el presupuesto por personaje, día y modo de reloj.

## Consecuencias

CP-201 prueba los disparadores con reloj virtual. CP-202 probará el presupuesto. CP-208 medirá intervalos y motivos por modo para ajustar umbrales y fuga. Contar llamadas al modelo como pensamientos distorsionaría la medida porque una cadena puede usar varias llamadas.

## Alternativas descartadas

El latido periódico ensayado durante el diseño sustituía a la deriva. Un calendario fijo en el perfil también duplicaría el horario que el juego ya eligió para ese día.

## Verificación

[R-04](../../design.md#13-reglas-verificables). [CP-201](../../backlog/tareas/CP-201-integrador-disparadores.md), [CP-202](../../backlog/tareas/CP-202-presupuesto-pulsos.md), [CP-208](../../backlog/tareas/CP-208-aceptacion-pulsion.md), [CP-209](../../backlog/tareas/CP-209-cadenas-despertar-acostarse.md).
