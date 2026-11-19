---
id: CP-905
titulo: Registrar la revisión de Argón y la prueba de continuidad entre días
estado: hecha
prioridad: P1
hito: D
depende_de: []
tipo: planning
complejidad: media
duracion_min: 60
sensibilidad: public
fecha: 2026-11-19
---

## Alcance de la revisión

Se revisaron los 19 commits de `5c5a818..710de95`. La escena determinista, los episodios con identidad, la memoria con procedencia y FakeWorld permiten seguir una experiencia concreta. El punto que falta demostrar es si una tarde de Leah llega a influir en una elección posterior a través de los componentes que registra el mod.

## Hallazgos en 710de95

| Prioridad | Evidencia de la revisión | Seguimiento |
|---|---|---|
| P1 | Ante un error de lectura o migración, el host crea un estado vacío. El siguiente guardado escribe en la misma clave. La posible pérdida se identificó por inspección del [módulo de persistencia](../../CorazonPelican/Modules/PersistenciaModule.cs). | [CP-110](CP-110-proteger-memoria-tras-fallo-de-carga.md) |
| P1 | Con la configuración del host, el envío de una reflexión válida devolvió `aceptada` y quedaron **0 recuerdos guardados**. La [API](../../CorazonPelican.Core/Api/ApiMotor.cs) confirma el envío cuando falta el aplicador. | [CP-213](CP-213-aplicar-respuestas-en-el-host.md) |
| P1 | Al emitir `acostarse`, emitir `despertar` y después entregar la reflexión nocturna en la misma época, la API devolvió `pulso_obsoleto`. Los dos hitos tienen la misma prioridad. | [CP-209](CP-209-cadenas-despertar-acostarse.md) |
| P1 | Con un recuerdo disponible en el motor, el [ciclo de la mente](../../mente/cliente_motor.py) procesó un hito ordinario con **0 consultas de memoria**. El recuerdo quedó fuera del prompt. | [CP-214](CP-214-recuperar-recuerdos-en-hitos.md) |
| P1 para evaluar con mente real | Con una clave presente, el [adaptador de modelos](../../mente/modelos.py) lanzó `requiere un adaptador activo` antes de enviar la petición. FakeLLM sí permitió probar el ciclo simulado. | [CP-215](CP-215-conectar-proveedor-de-la-mente.md) |
| P2 | Con un tope de 100 tokens y cuatro llamadas de cadena de 100, el gasto llegó a 400 y se admitía otra llamada. El [control de costes](../../mente/costes.py) consulta el contador ordinario en ese caso. | [CP-216](CP-216-computar-tokens-de-cadenas.md) |

## Prueba de dos días

1. En FakeWorld, Leah pasa una tarde a solas. Tomar el id de un episodio que el motor incorpore a su diario.
2. Retrasar la respuesta de acostarse hasta después del guardado y del cambio de día. Entregarla en la misma época, por la API y con los componentes que registra el host. Comprobar que se acepta y queda un único recuerdo con ese origen.
3. Efectuar el siguiente guardado y recargarlo. Enviar un hito ordinario con el mismo lugar y las mismas opciones. Inspeccionar el prompt del ciclo habitual de la mente y verificar que el recuerdo recuperado explica una opción que Leah podría elegir.
4. En la aceptación corporal posterior, observar primero cuándo guarda el cuaderno o sale de casa y cotejar la conducta con su horario y su experiencia. El resultado de FakeWorld verifica el recorrido de los datos. La observación del personaje queda en [CP-305](CP-305-aceptacion-cuerpo.md).

La recarga se hace **después de guardar la reflexión ya aplicada**. La ADS §6 contempla que se pierda si el juego se cierra entre su llegada y ese guardado. [Arquitectura](../../architecture.md#6-la-mente-mente).

## Cierre de la inscripción

- [x] Los seis hallazgos tienen evidencia y una ficha de seguimiento en el [índice](../README.md).
- [x] CP-209 recoge el caso de reflexión tardía y [CP-210](CP-210-humor-y-conducta.md) recoge la prueba de continuidad con la mente y el host conectados.

Verificación sobre `710de95` con los comandos de AGENTS.md y el despliegue desactivado. Compilación con 0 advertencias y 0 errores, xUnit con 338 pruebas superadas y 0 omitidas, pytest con 94 pruebas superadas. La revisión se realizó fuera del juego.
