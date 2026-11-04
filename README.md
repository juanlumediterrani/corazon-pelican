# Corazón Pelican

Motor de habitantes para Stardew Valley 1.6: un personaje del valle (primero, **Leah**) con una
mente que habita el juego. Percibe en prosa desde dentro del mundo, piensa cuando su rutina o lo
que pasa a su alrededor lo pide, actúa con su propio cuerpo y deja obra en el valle.

**Estado:** diseño, backlog y esqueleto de la solución (CP-001). Comandos y reglas de trabajo en
[`AGENTS.md`](AGENTS.md).

| Documento | Para qué |
|---|---|
| [`design.md`](design.md) | GSS: qué hace el sistema y por qué (pilares, percepción, pensamiento, rutina, obra, diálogo, memoria, reglas R-xx) |
| [`architecture.md`](architecture.md) | ADS: cómo se construye (solución, contrato motor ↔ mente, bucle, taller, persistencia, verificaciones V-xx) |
| [`docs/decisiones.md`](docs/decisiones.md) | Decisiones tomadas (P-1…P-4, P-8, P-11), pendientes con recomendación y riesgos |
| [`docs/notas-juego.md`](docs/notas-juego.md) | Hechos comprobados del juego, rutas locales y qué reutilizar de Pelican 4Ever y Stardew Voyager |
| [`backlog/README.md`](backlog/README.md) | Índice de tareas (formato Dr4co Core), trazabilidad de reglas |
| [`tools/xnbdump/`](tools/xnbdump/) | Lector de datos `.xnb` del juego (horarios, animaciones) |

**Por dónde empezar:** CP-001 (repositorio y solución), luego el spike del taller (CP-013), la voz
de Leah y su rúbrica (CP-205) y los pendientes de `docs/decisiones.md` (CP-902).

Proyectos hermanos: Pelican 4Ever (`../Stardew Valley MORPG`) y Stardew Voyager
(`../stardew-voyager`).
