# Corazón Pelican

Motor de habitantes para Stardew Valley 1.6: un personaje del valle (primero, **Leah**) con una
mente que habita el juego. Percibe en prosa desde dentro del mundo, piensa cuando su rutina o lo
que pasa a su alrededor lo pide, actúa con su propio cuerpo y deja obra en el valle.

La primera escena que queremos poder observar sucede aunque el granjero pase el día en otra
parte. Cómo le sentaron a Leah varias tardes en casa puede notarse después en algo pequeño.
Un día se demora con el cuaderno. Otro lo guarda antes y sale en busca de compañía. Su oficio,
sus recuerdos y lo que encuentra ese día dan sentido a la elección. El diseño concreta cómo observar esa iniciativa en la
[aceptación de la fase de cuerpo](backlog/tareas/CP-305-aceptacion-cuerpo.md).

La solución compila. El contrato motor ↔ mente ya se valida desde C# y Python. El mod incluye
el cargador de perfiles, el perfil y la voz de Leah (`leah.voz.md` y la rúbrica de carácter).
En FakeWorld se prueban la saliencia, las bandas de detalle, los disparadores de pensamiento,
el presupuesto de pulsos y el léxico único. La observación del juego, las plantillas de prosa,
la API que entregará los pulsos a la mente y la ejecución de intenciones siguen pendientes.

Diez reglas del diseño cuentan con guarda: las plantillas deterministas (R-01, CP-104),
la ausencia de alucinaciones en escena (R-02, CP-104), el nivel de detalle según la banda de
percepción (R-03, CP-103), el presupuesto diario aplicado por el motor (R-04, CP-202), la
no obstrucción del cuerpo (R-05, CP-203), las precondiciones de intención (R-06, CP-301),
la persistencia del diario con la partida (R-10, CP-107), la exclusión de coordenadas y rutas
en los planes (R-13, CP-002), el léxico único compartido (R-14, CP-105) y los cambios de
rutina acotados por el perfil (R-16, CP-212). Las tareas y sus pruebas están en el
[backlog](backlog/README.md).

| Documento | Para qué |
|---|---|
| [`design.md`](design.md) | GSS: qué hace el sistema y por qué (pilares, percepción, pensamiento, rutina, obra, diálogo, memoria, reglas R-xx) |
| [`architecture.md`](architecture.md) | ADS: cómo se construye (solución, contrato motor ↔ mente, bucle, taller, persistencia, verificaciones V-xx) |
| [`docs/decisiones.md`](docs/decisiones.md) | Acuerdos de diseño, propuestas pendientes de respuesta y riesgos técnicos |
| [`docs/adr/`](docs/adr/) | Seis decisiones de arquitectura y las pruebas que les corresponden |
| [`docs/notas-juego.md`](docs/notas-juego.md) | Hechos comprobados del juego, rutas locales y qué reutilizar de Pelican 4Ever y Stardew Voyager |
| [`backlog/README.md`](backlog/README.md) | Índice de tareas (formato Dr4co Core), trazabilidad de reglas |
| [`tools/xnbdump/`](tools/xnbdump/) | Lector de datos `.xnb` del juego (horarios, animaciones) |

Las tareas listas sin juego vivo son la recuperación de memoria y reflexión (CP-206) y el
presupuesto de tokens y costes (CP-207), junto con `HabitanteModule` y telemetría (CP-109,
con `CP-101` pendiente solo de medición DEV en vivo). El índice recoge las
[tareas listas](backlog/README.md#tareas-listas) y [AGENTS.md](AGENTS.md) contiene los comandos
para verificar la solución.

Proyectos hermanos: Pelican 4Ever (`../Stardew Valley MORPG`) y Stardew Voyager
(`../stardew-voyager`).
