# ADR-0001 · El host gobierna el cuerpo y la mente trabaja fuera del juego

## Contexto

Una respuesta puede tardar varios segundos. Durante esa espera el horario del NPC, los jugadores y el resto del valle siguen cambiando.

## Decisión

El mod del anfitrión publica los pulsos por HTTP en loopback. La mente los recoge y devuelve propuestas. En cada tick el motor drena la cola en el hilo principal y valida la propuesta contra el mundo vigente antes de aplicarla.

## Consecuencias

Una mente lenta deja avanzar el cuerpo. Si cae, Leah puede volver al horario vanilla. CP-203 medirá el tiempo del tick con una mente falsa que tarda 30 segundos. CP-303 comprobará la devolución del NPC.

## Alternativas descartadas

Esperar al proveedor desde el tick trasladaría su latencia al juego. Dejar que cada cliente gobierne al NPC daría a varios jugadores autoridad sobre el mismo cuerpo.

## Verificación

[R-05 y R-11](../../design.md#13-reglas-verificables). [CP-203](../../backlog/tareas/CP-203-api-motor.md), [CP-303](../../backlog/tareas/CP-303-reclamacion-y-degradacion.md).
