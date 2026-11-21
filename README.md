# Corazón Pelican

Motor de habitantes para Stardew Valley 1.6: un personaje del valle (primero, **Leah**) con una
mente que habita el juego. Percibe en prosa desde dentro del mundo, piensa cuando su rutina o lo
que pasa a su alrededor lo pide, actúa con su propio cuerpo y deja obra en el valle.

La primera escena que queremos poder observar sucede aunque el granjero pase el día en otra
parte. Cómo le sentaron a Leah varias tardes en casa puede notarse después en algo pequeño.
Un día se demora con el cuaderno. Otro lo guarda antes y sale en busca de compañía. Su oficio,
sus recuerdos y lo que encuentra ese día dan sentido a la elección. El diseño concreta cómo observar esa iniciativa en la
[aceptación de la fase de cuerpo](backlog/tareas/CP-305-aceptacion-cuerpo.md).

La solución compila y pasa las suites sin juego vivo en `FakeWorld`, C# (`CorazonPelican.Tests`)
y Python (`mente/`). Están implementados el contrato JSON Schema motor ↔ mente, los perfiles
y plantillas de prosa (Leah + `leah.voz.md`), el sensorio completo (`ObservadorJuego`, saliencia,
bandas, prosa determinista, búfer, huella y diario), el motor de pulsión, cadenas, humor, agenda,
compromisos con prórroga por carta, validadores de intención/actos/menciones, la API loopback del
anfitrión, los módulos SMAPI (`PersistenciaModule`, `ApiModule`, `HabitanteModule`,
`CorreoProrrogaModule`, `ConsolaModule`) y el proceso `mente/` en Python (recuperación/reflexión
de memoria, cadenas de despertar y acostarse, continuidad de humor, presupuestos de tokens y
clientes HTTP reales).

Catorce reglas del diseño cuentan con guarda vigente: las plantillas deterministas (`R-01`, `CP-104`),
la ausencia de alucinaciones en escena (`R-02`, `CP-104`), el gradiente de detalle (`R-03`, `CP-103`),
el presupuesto diario aplicado por el motor (`R-04`, `CP-202`), la no obstrucción del cuerpo
(`R-05`, `CP-203`), las precondiciones de intención (`R-06`, `CP-301`), la validación de actos
antes de mostrar frase (`R-07`, `CP-501`), el acto `regala` (`R-08`, `CP-501`), los compromisos
con plazo y prórroga por carta (`R-09`, `CP-502` y `CP-509`), la persistencia del diario con la
partida (`R-10 (diario)`, `CP-107`), la exclusión de coordenadas en los planes (`R-13`, `CP-002`),
el léxico único compartido (`R-14`, `CP-105`), el validador de menciones ancladas a escena y memoria
(`R-15`, `CP-506`) y los cambios de rutina acotados por el perfil (`R-16`, `CP-212`). Las tareas y
sus pruebas están en el [backlog](backlog/README.md).

| Documento | Para qué |
|---|---|
| [`design.md`](design.md) | GSS: qué hace el sistema y por qué (pilares, percepción, pensamiento, rutina, obra, diálogo, memoria, reglas R-xx) |
| [`architecture.md`](architecture.md) | ADS: cómo se construye (solución, contrato motor ↔ mente, bucle, taller, persistencia, verificaciones V-xx) |
| [`docs/decisiones.md`](docs/decisiones.md) | Acuerdos de diseño, propuestas pendientes de respuesta y riesgos técnicos |
| [`docs/adr/`](docs/adr/) | Seis decisiones de arquitectura y las pruebas que les corresponden |
| [`docs/notas-juego.md`](docs/notas-juego.md) | Hechos comprobados del juego, rutas locales y qué reutilizar de Pelican 4Ever y Stardew Voyager |
| [`backlog/README.md`](backlog/README.md) | Índice de tareas (formato Dr4co Core), trazabilidad de reglas |
| [`tools/xnbdump/`](tools/xnbdump/) | Lector de datos `.xnb` del juego (horarios, animaciones) |

Las tareas sin juego vivo listas de las fases F0–F2 y F5 han quedado completadas, y la
generación de obras se ha pospuesto a Fase 2 empezando primero por Elliott en texto (`P-13`).
Las únicas fichas listas en el índice son **CP-006** 🎮 (partida de desarrollo y medición de
lugares de Leah en juego vivo) y **CP-111** (parámetros de búfer y huella en el perfil, `P-10`),
mientras que `CP-014`, `CP-101` y `CP-902` permanecen en curso. El índice recoge las
[tareas listas](backlog/README.md#tareas-listas) y [AGENTS.md](AGENTS.md) contiene los comandos
para verificar la solución.

Proyectos hermanos: Pelican 4Ever (`../Stardew Valley MORPG`) y Stardew Voyager
(`../stardew-voyager`).
