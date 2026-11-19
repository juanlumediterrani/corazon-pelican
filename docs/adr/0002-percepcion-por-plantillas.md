# ADR-0002 · La escena se escribe con plantillas deterministas

## Contexto

En la pasarela, Leah puede fijarse en la luz sobre el agua y apenas advertir a quien se acerca. Esa diferencia debe poder explicarse a partir de lo observado y de sus rasgos.

## Decisión

El motor calcula la saliencia, asigna una banda y elige las plantillas del perfil. La escena y su apéndice comparten las referencias y los nombres del léxico. La mente interpreta el resultado.

## Consecuencias

Dos ejecuciones con la misma entrada deben producir la misma escena byte a byte. Las escenas doradas de CP-104 permitirán revisar una variante de redacción y detectar si introduce una entidad ausente. CP-103 ya prueba el detalle permitido por banda.

## Alternativas descartadas

Pedir a un modelo que narre la observación en cada muestra añadiría latencia y permitiría que inventase detalles antes de que Leah empezara a pensar.

## Verificación

[R-01, R-02, R-03 y R-14](../../design.md#13-reglas-verificables). [CP-103](../../backlog/tareas/CP-103-bandas-gradiente.md), [CP-104](../../backlog/tareas/CP-104-prosa-determinista.md), [CP-105](../../backlog/tareas/CP-105-lexico-unico.md).
