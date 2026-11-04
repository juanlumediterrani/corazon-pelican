---
id: CP-001
titulo: Repositorio y solución multiproyecto que compila sin juego
estado: hecha
prioridad: P1
hito: F0
depende_de: []
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
fecha: 2026-11-04
---

## Objetivo

Dejar un repositorio git con la estructura de la ADS §2 que compila y pasa una suite vacía, sin tocar la instalación del juego.

## Contexto

La carpeta solo contiene `design.md` y `architecture.md`; no es un repositorio. La convención de la casa (Pelican 4Ever, Voyager) es `TreatWarningsAsErrors`, `Pathoschild.Stardew.ModBuildConfig`, `local.props` no versionado y compilaciones de comprobación con `-p:EnableModDeploy=false`.

## Alcance

- `git init`, `.gitignore` (bin/obj, `local.props`, `telemetria/`, `mente/.venv`, cachés de Python).
- `CorazonPelican.sln` con `CorazonPelican.Core` (net6.0, sin referencias a Stardew/SMAPI/Harmony), `CorazonPelican` (mod SMAPI, net6.0) y `CorazonPelican.Tests` (xUnit, net8.0).
- Esqueletos `mente/` y `taller/` con `pyproject.toml` y pytest.
- `AGENTS.md` (y `CLAUDE.md` que lo enlace) con comandos de verificación, idioma (docs en español, identificadores según ADS) y la regla «sin juego vivo salvo tareas 🎮 autorizadas».
- `manifest.json` con UniqueID `Dr4co.CorazonPelican` (ajustable) y dependencia mínima SMAPI 4.1.

## Criterios de aceptación

- [x] `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` termina con 0 advertencias. Evidencia (2026-11-04): «0 Advertencia(s), 0 Errores».
- [x] `dotnet test` y `python3 -m pytest -q` terminan en verde. Evidencia: xUnit «Superado: 6, Total: 6»; pytest «2 passed».
- [x] Un test de arquitectura falla si `CorazonPelican.Core` referencia el juego, SMAPI o Harmony: `ArquitecturaTests.Core_no_referencia_juego_smapi_ni_harmony` (5 casos) y `Core_no_contiene_nada_especifico_de_leah`.
- [x] Primer commit `CP-001: …` sin ficheros de telemetría ni `local.props` (rama `main`).

## Fuera de alcance

- Lógica de dominio. Inscripción en Dr4co Core (decisión del usuario, ver `docs/decisiones.md`).
