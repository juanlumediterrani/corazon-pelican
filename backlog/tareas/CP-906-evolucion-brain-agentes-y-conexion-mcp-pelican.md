---
id: CP-906
titulo: Evolucionar documentación a Project Brain (estándar Niu D'Ocells) y conectar contratos con MCP Pelican
estado: pendiente
prioridad: P2
hito: D
depende_de: [CP-901]
tipo: planning
complejidad: media
duracion_min: 90
sensibilidad: public
---

## Objetivo

Evolucionar la documentación de *Corazón Pelican* hacia un **Project Brain operativo** conforme al estándar corporativo (`1- brain&mcp.md` v1.1) e indexar sus ADRs, esquemas y contrato de puente con *Pelican 4Ever* para consumo limpio desde **MCP Pelican** (`/home/mtx/Documentos/Projects/MCP Pelican`, `repo_id="corazon-pelican"`).

## Contexto

*Corazón Pelican* ya tiene `AGENTS.md` compacto (~4 KB), `schemas/*.schema.json` y `docs/adr/0001..0006`, pero requiere cuatro ajustes para operar como Brain Nivel 1 acoplado en Nivel 2:
1. Faltan `docs/README.md` y `docs/adr/README.md` (y aún permanece `docs/adr/.gitkeep`), y los ADRs `0001..0006` deben normalizar `Estado`, `Fecha` y `Ámbito` en cabecera para que `list_active_adrs` y `route_context` del MCP operen sin ambigüedad.
2. `AGENTS.md` carece de reglas de exclusión explícitas (`docs/spikes/**`, `backlog/reportes/**`, `docs/archive/**`, `docs/verifications/**`), tabla de enrutamiento condicional Código → ADR/Contrato/Schema y protocolo formal de *write-back* pre-PR.
3. La frontera de interoperabilidad con *Pelican 4Ever* (`PelicanApi`, `CorazonPelican.Control`, `CorazonPelican.Prorroga.*`, `RegisterTimeJumpHook`) conviene concentrarla en un contrato Markdown indexable por `read_contract` en `MCP Pelican`.

## Alcance

- Crear `docs/README.md` y `docs/adr/README.md` (retirando `docs/adr/.gitkeep`) y añadir cabeceras `Estado`, `Fecha` y `Ámbito` en `docs/adr/0001..0006`.
- Aislar/excluir `docs/spikes/**` y `backlog/reportes/**` como evidencia puntual (`docs/verifications/` y `docs/archive/` según corresponda) fuera de las búsquedas por defecto.
- Actualizar `AGENTS.md` (`< 150` líneas / `< 20 KB`) con exclusiones, jerarquía de precedencia (`schemas/*.schema.json` + tests → ADRs y contratos → `architecture.md`/`design.md` → `backlog/`), tabla de enrutamiento condicional (`CorazonPelican.Core/`, `CorazonPelican/Adaptador/`, `CorazonPelican/Modules/IntegracionPelican*`, `schemas/`, `mente/`, `taller/`) y protocolo de *write-back*.
- Documentar/indexar el contrato de integración `Corazón Pelican ↔ Pelican 4Ever` en `docs/contrato-pelican4ever.md` (registrándolo en `MCP Pelican` `configs/pelican-ecosystem.mcp.json`) y añadir test de contrato del Brain en `CorazonPelican.Tests` / `pytest`.

## Criterios de aceptación

- [ ] `docs/adr/README.md` indexa `0001..0006` sin colisiones y los 6 ADRs declaran `Estado`, `Fecha` y `Ámbito` en sus primeras 20 líneas.
- [ ] `AGENTS.md` (`< 150` líneas, `< 20 KB`, sin frontmatter) declara exclusiones, precedencia, enrutamiento Código → ADR/Schema y regla de *write-back*.
- [ ] Existe contrato explícito de puente con *Pelican 4Ever* legible por sección vía `read_contract("corazon-pelican", ...)` en `MCP Pelican`.
- [ ] Suite completa (`dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` y `python3 -m pytest -q`) en verde incluyendo verificador de Brain, y `get_ecosystem_map("corazon-pelican")` en `MCP Pelican` reporta cumplimiento al 100 %.

## Fuera de alcance

- Cambiar los esquemas JSON v1 (`schemas/*.schema.json`) ni el comportamiento runtime del mod o la mente.
