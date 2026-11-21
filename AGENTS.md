# AGENTS.md · Corazón Pelican

Instrucciones para cualquier agente que trabaje en este repositorio.

## Qué leer antes de tocar nada

1. [`README.md`](README.md): mapa de documentos.
2. [`design.md`](design.md) (GSS, qué y por qué) y [`architecture.md`](architecture.md) (ADS, cómo).
3. [`docs/decisiones.md`](docs/decisiones.md): decisiones P-x y pendientes. No reabras una
   decisión tomada sin que el usuario lo pida.
4. [`docs/notas-juego.md`](docs/notas-juego.md): hechos del juego ya comprobados y rutas locales.
   Consúltalo antes de volver a buscar en el decompilado.

## Comandos de verificación

```bash
dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false
dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false
python3 -m pytest -q
```

Sin `-p:EnableModDeploy=false`, ModBuildConfig copia el mod a `Mods/` del juego: hazlo solo
cuando se quiera probar en vivo.

## Partida de desarrollo y escenarios (`CP-006`)

Las sesiones 🎮 autorizadas usan siempre el slot DEV `CorazonDev_*` (nunca una campaña real).
Los respaldos y escenarios con nombre viven fuera del repo en `~/.local/share/corazon-pelican/escenarios/`
y solo pueden restaurarse en carpetas cuyo nombre empiece por `CorazonDev`:

```bash
python3 tools/escenarios.py guardar base_primavera --partida CorazonDev_454975123 --descripcion "..."
python3 tools/escenarios.py listar
python3 tools/escenarios.py verificar base_primavera
python3 tools/escenarios.py restaurar base_primavera --destino CorazonDev_454975123 --confirmar
```

En la consola de SMAPI están disponibles `cp_posicion [npc]` (imprime ubicación, casilla,
orientación, transitabilidad y fragmento JSON) y `cp_lugares [personaje]` (verifica en vivo que
todas las áreas del perfil son transitables).

## Estructura

| Carpeta | Contenido |
|---|---|
| `CorazonPelican.Core/` | Dominio puro (net6.0), sin Stardew, SMAPI ni Harmony; subcarpetas según ADS §2 |
| `CorazonPelican/` | Mod SMAPI (net6.0): `Adaptador/`, `Modules/`, `assets/personajes/` |
| `CorazonPelican.Tests/` | xUnit (net8.0) sobre Core y FakeWorld |
| `schemas/` | JSON Schema: única fuente del contrato motor ↔ mente (CP-002) |
| `mente/`, `taller/` | Procesos Python (≥ 3.10) fuera del juego, con sus `tests/` |
| `docs/` | Decisiones, notas del juego, `adr/` y `spikes/` |
| `backlog/` | Tareas y `reportes/` de aceptación |
| `tools/` | Utilidades de desarrollo (p. ej. `xnbdump`) |

## Reglas

1. **Idioma:** documentación, mensajes y nombres de dominio en español, como en la ADS.
2. **Moratoria de prosa:** una regla R-xx de la GSS no está vigente hasta que exista su guarda
   (test). Cada tarea que la implementa cita el test.
3. **Core es puro:** `ArquitecturaTests` lo vigila. El motor no contiene nada específico de un
   personaje: lo propio de Leah va en su perfil (`assets/personajes/`).
4. **Sin juego vivo** salvo en tareas marcadas 🎮 y con autorización explícita del usuario en
   esa sesión, siempre en la partida de desarrollo. Todo lo demás se prueba con FakeWorld.
5. **No se versiona** contenido del juego (`.xnb`, volcados de `tools/xnbdump`), telemetría ni
   `local.props`.
6. **Evidencia sobre afirmaciones:** al cerrar una tarea, cita la salida de los comandos de
   verificación; un test omitido o fallido se declara.

## Backlog y ciclo de tareas

Formato de tarea v1 de Dr4co Core (el mismo que Stardew Voyager): una tarea por fichero en
`backlog/tareas/<ID>-<slug>.md`, índice en `backlog/README.md` sincronizado en el mismo commit.

- Estados: `pendiente` → `en-curso` → `hecha`; o `bloqueada` (con `## Bloqueo`) o `dormida`.
- Una tarea está **lista** cuando es `pendiente` y todas sus `depende_de` están `hecha`. Orden:
  `prioridad` (P1, P2, P3, profunda), hito e id.
- Una tarea, un commit: `<ID>: <titulo>`.
- No amplíes el alcance: lo que descubras fuera de la tarea va a una tarea nueva `pendiente`.
- Al cerrar: `estado: hecha`, `fecha`, criterios marcados con su evidencia y, en las de
  aceptación, informe en `backlog/reportes/<ID>.md`.
