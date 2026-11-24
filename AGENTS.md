# `AGENTS.md` — Índice Operativo del Brain (*Corazón Pelican*)

> **Punto de entrada obligatorio para cualquier agente de IA (`EST-001` v1.2 de Niu D'Ocells).**
> Lee primero estas reglas de contexto, localiza los ficheros obligatorios en la tabla de enrutamiento según el área que vayas a tocar y ejecuta el protocolo de *write-back* al cerrar la tarea.

---

## 1. Reglas de Contexto y Fuente Única de Verdad

1. **Idioma único normativo:** el **español** (ficheros `.md` sin sufijo de idioma) es la única fuente de verdad del Project Brain.
2. **Rutas prohibidas en búsquedas y lectura de contexto** *(salvo petición literal del usuario)*:
   - **`docs/archive/**`** — diseño retirado o decisiones históricas superadas.
   - **`docs/verifications/**`** — informes puntuales o evaluaciones fechadas.
   - **`docs/spikes/**`** y **`backlog/reportes/**`** — investigación exploratoria (`v01..v07`, `muestras-taller/`) e informes de aceptación cerrados.
   - **`**/*.en.md` y `**/*.zh-CN.md`** — traducciones derivadas (si existen); nunca son fuente normativa.
   - **`telemetria/**`, `telemetry/**`, `*.xnb`** — artefactos locales o contenido propietario del juego.
3. **Orden de precedencia ante conflicto:**
   1. **Código, esquemas JSON y tests:** `schemas/*.schema.json`, `CorazonPelican.Core/**`, `CorazonPelican/**`, `CorazonPelican.Tests/**`, `mente/**`, `taller/**`.
   2. **ADRs `Aceptados` y contratos vigentes:** [`docs/adr/README.md`](docs/adr/README.md), [`docs/contrato-pelican4ever.md`](docs/contrato-pelican4ever.md), [`docs/decisiones.md`](docs/decisiones.md).
   3. **Diseño e información del juego:** [`design.md`](design.md) (GSS), [`architecture.md`](architecture.md) (ADS), [`docs/notas-juego.md`](docs/notas-juego.md), [`docs/rubrica-caracter.md`](docs/rubrica-caracter.md).
   4. **Backlog local** ([`backlog/README.md`](backlog/README.md)).
4. **ADRs `Propuestos` o reservados:** actualmente están `Aceptados` `0001..0006`; cualquier decisión nueva toma **`0007+`** verificando reservas en [`docs/adr/README.md`](docs/adr/README.md).
5. **Ecosistema Nivel 2 (`MCP Pelican`):** para consultar contratos compartidos de `Pelican4Ever` (por ejemplo `host-api.md`) o `Stardew Voyager`, usa **`MCP Pelican`** (`../MCP Pelican`, `repo_id="corazon-pelican"`) sobre [`docs/contrato-pelican4ever.md`](docs/contrato-pelican4ever.md).

---

## 2. Invariantes Críticos del Sistema y Empresa

- **Empresa (`Niu D'Ocells`):**
  - El sistema corporativo único de tareas de producto es **ERPNext** (`DEC-0001`; `PROJ-0005` en Pelican). La carpeta `backlog/` conserva el desglose técnico de desarrollo de este repositorio (sin dependencia de arneses externos antiguos).
  - Todo artefacto desplegable limpio generado en release se deposita exclusivamente en `~/Documentos/Niu D'Ocells/produccio/` (`DEC-0002`).
- **Pureza de dominio:** `CorazonPelican.Core` (`net6.0`) **no referencia** Stardew Valley, SMAPI ni Harmony (`ArquitecturaTests`). Todo perfil de personaje va en `CorazonPelican/assets/personajes/`.
- **Contrato único motor ↔ mente:** `schemas/*.schema.json` manda sobre `CorazonPelican.Core/Contrato/**` y `mente/contrato.py` (`ADR-0005`).
- **Compilación segura:** usa siempre `-p:EnableModDeploy=false -p:EnableModZip=false` en `dotnet build` / `dotnet test` para no tocar `Mods/` ni generar zips locales.
- **Sin juego vivo salvo autorización:** toda verificación ordinaria corre con `FakeWorld`, `CorazonPelican.Tests` y `pytest`. Las pruebas 🎮 autorizadas usan siempre el slot DEV `CorazonDev_*` (`python3 tools/escenarios.py`).
- **Verificación estándar:**
  ```bash
  dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false
  dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false
  python3 -m pytest -q
  ```

---

## 3. Enrutamiento Condicional (Código → Memoria)

| Si vas a tocar... | Lectura obligatoria | Qué regula |
| --- | --- | --- |
| `CorazonPelican.Core/Api/*`, `CorazonPelican/Modules/ApiModule.cs`, `mente/**` | [`ADR-0001`](docs/adr/0001-motor-en-el-host.md), [`architecture.md`](architecture.md), [`corazon-pelican-dev`](.agents/skills/corazon-pelican-dev/SKILL.md) | Host dueño del cuerpo, mente Python fuera del juego y bucle HTTP |
| `CorazonPelican/Adaptador/ObservadorJuego.cs`, `CorazonPelican.Core/Sensorio/**`, `CorazonPelican/assets/personajes/*.plantillas.json` | [`ADR-0002`](docs/adr/0002-percepcion-por-plantillas.md), [`docs/notas-juego.md`](docs/notas-juego.md) | Percepción sin alucinaciones y plantillas deterministas |
| `CorazonPelican.Core/Dialogo/**`, `CorazonPelican/Modules/{DialogoModule,CorreoProrrogaModule}.cs`, `schemas/respuesta.schema.json` | [`ADR-0003`](docs/adr/0003-actos-antes-de-la-frase.md), [`docs/rubrica-caracter.md`](docs/rubrica-caracter.md) | Validación de actos previa, reflejos < 300 ms y prórroga por carta |
| `CorazonPelican.Core/Memoria/**`, `CorazonPelican/Modules/PersistenciaModule.cs` | [`ADR-0004`](docs/adr/0004-memoria-con-la-partida.md) | Persistencia del diario/compromisos ligada a la partida y protección ante fallos |
| `schemas/*.schema.json`, `CorazonPelican.Core/Contrato/**`, `mente/contrato.py` | [`ADR-0005`](docs/adr/0005-contrato-y-epoca.md) | Fuente única JSON Schema, épocas por carga y coherencia motor ↔ mente |
| `CorazonPelican.Core/Atencion/**`, `CorazonPelican/Modules/HabitanteModule.cs` | [`ADR-0006`](docs/adr/0006-cadencia-por-disparadores.md), [`docs/decisiones.md`](docs/decisiones.md) | Disparadores, saliencia, presupuesto de pulsos y cadena rutina |
| `CorazonPelican/Framework/IPelicanApi.cs`, `CorazonPelican/Modules/IntegracionPelicanModule.cs`, `CorazonPelican/Adaptador/ReclamacionNpc.cs` | [`docs/contrato-pelican4ever.md`](docs/contrato-pelican4ever.md), [`smapi-harmony-pelican-bridge`](../MCP%20Pelican/skills/smapi-harmony-pelican-bridge/SKILL.md) | Puente opcional `Pelican4Ever.SVRTMP`, `modData["CorazonPelican.Control"]` y checkpoints |
| `taller/**`, `CorazonPelican/Adaptador/CargadorPerfiles.cs`, `CorazonPelican/assets/personajes/*.json` | [`design.md`](design.md), [`corazon-pelican-dev`](.agents/skills/corazon-pelican-dev/SKILL.md) | Paletas, taller fuera del juego y reglas de perfil/léxico |

---

## 4. Skills y Protocolo de Cierre (*Write-Back*)

1. **Skills locales del proyecto (`.agents/skills/` y `.agents/skills.json`):**
   - `corazon-pelican-dev`: flujo de desarrollo C# Core + SMAPI + mente Python + escenarios `CorazonDev_*`.
   - `brain-cierre-tarea`: checklist obligatorio de verificación y sincronización documental antes de cerrar/PR.
   - `smapi-harmony-pelican-bridge` (enlazada desde `MCP Pelican` vía `.agents/skills.json`): parches Harmony seguros, ciclo de vida SMAPI y cruce por `ModRegistry.GetApi`.
2. **Regla obligatoria de *write-back*:** si modificas `schemas/*.schema.json`, invariantes de `CorazonPelican.Core`, el puente `IPelicanApi` / `IntegracionPelicanModule`, o `ProtocoloMultijugador`, actualiza [`docs/contrato-pelican4ever.md`](docs/contrato-pelican4ever.md) o añade/actualiza el ADR correspondiente en `docs/adr/` **en el mismo commit**.
3. **Evidencia:** toda verificación puntual o informe fechado va en `docs/verifications/`, nunca en la raíz de `docs/`.
