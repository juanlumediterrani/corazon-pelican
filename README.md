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

Cuatro reglas del diseño cuentan con guarda: el nivel de detalle según la banda de percepción
(R-03, CP-103), el presupuesto diario aplicado por el motor (R-04, CP-202), la exclusión de
coordenadas y rutas en los planes de la mente (R-13, CP-002) y el léxico único compartido
(R-14, CP-105). Las tareas y sus pruebas están en el [backlog](backlog/README.md).

| Documento | Para qué |
|---|---|
| [`design.md`](design.md) | GSS: qué hace el sistema y por qué (pilares, percepción, pensamiento, rutina, obra, diálogo, memoria, reglas R-xx) |
| [`architecture.md`](architecture.md) | ADS: cómo se construye (solución, contrato motor ↔ mente, bucle, taller, persistencia, verificaciones V-xx) |
| [`docs/decisiones.md`](docs/decisiones.md) | Acuerdos de diseño, propuestas pendientes de respuesta y riesgos técnicos |
| [`docs/adr/`](docs/adr/) | Seis decisiones de arquitectura y las pruebas que les corresponden |
| [`docs/notas-juego.md`](docs/notas-juego.md) | Hechos comprobados del juego, rutas locales y qué reutilizar de Pelican 4Ever y Stardew Voyager |
| [`backlog/README.md`](backlog/README.md) | Índice de tareas (formato Dr4co Core), trazabilidad de reglas |
| [`tools/xnbdump/`](tools/xnbdump/) | Lector de datos `.xnb` del juego (horarios, animaciones) |

Las dos tareas P1 listas son la prueba de legibilidad de los cuadros (CP-013) y las plantillas
de prosa deterministas (CP-104); en P2, CP-005 desbloquea la observación y la API del motor.
El índice recoge las [tareas listas](backlog/README.md#tareas-listas) y [AGENTS.md](AGENTS.md)
contiene los comandos para verificar la solución.

Proyectos hermanos: Pelican 4Ever (`../Stardew Valley MORPG`) y Stardew Voyager
(`../stardew-voyager`).
