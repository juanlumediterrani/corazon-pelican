---
id: CP-601
titulo: Pelican 4Ever · WorldLifeModule ignora NPCs reclamados por Corazón Pelican
estado: hecha
fecha: 2026-11-22
prioridad: P3
hito: F6
depende_de: [CP-303]
tipo: code-light
complejidad: baja
duracion_min: 45
sensibilidad: public
proyecto_destino: Stardew Valley MORPG
---

## Objetivo

Abrir y hacer en Pelican el cambio pequeño que exige la ADS §11.1: el *micro-wandering* no mueve a un NPC con `modData["CorazonPelican.Control"]`.

## Contexto

`Pelican4Ever/Modules/WorldLifeModule.cs` existe. Es una tarea de otro repositorio: aquí solo se sigue; el trabajo se inscribe en el backlog de Pelican.

## Alcance

- Tarea espejo en `Stardew Valley MORPG/backlog.md` y cambio con test en `Pelican4Ever.Tests`.
- Auditoría completa de puntos de intersección en `Pelican4Ever/Modules/` sobre NPCs reclamados con `modData["CorazonPelican.Control"]` (`WorldLifeModule` micro-wandering, `Prefix_CheckSchedule` y `RealTimeClockModule.CatchUpSchedule`).

## Criterios de aceptación

- [x] Test en Pelican: un NPC con la clave no recibe destinos de *micro-wandering*. (`NpcShopkeeperLogicTests.CanMicroWander_BlocksExternallyControlledNpc_CorazonPelican` parametrizado en `"Forest"`, `"Town"`, `"Saloon"`, `"Club"`, integrado en `main` de `Stardew Valley MORPG` —merge `be875dc` sobre `d5a2aa3` de `dev` + commits `fe76df7` y `4766210`—, ejecutado sobre `main` en `7fb03e9`: `dotnet test --no-build` → `1809/1809` superados, `0` errores y `0` advertencias de compilación)
- [x] La clave se documenta como contrato entre ambos mods (nombre exacto y semántica). (`NpcShopkeeperLogic.CorazonPelicanControlKey = "CorazonPelican.Control"` en Pelican 4Ever y `ReclamacionNpc.ClaveControlModData = "CorazonPelican.Control"` en Corazón Pelican; documentado en `docs/notas-juego.md` §10 y en la tarea espejo de `Stardew Valley MORPG/backlog.md`, commit `7fb03e9`)

## Contrato e integración en `Stardew Valley MORPG` (`main`, `be875dc` / `7fb03e9`)

- **Clave de contrato:** `modData["CorazonPelican.Control"]` (`NpcShopkeeperLogic.CorazonPelicanControlKey` en Pelican 4Ever; `ReclamacionNpc.ClaveControlModData` en Corazón Pelican). Mientras exista en `npc.modData`, Pelican 4Ever considera el NPC controlado externamente; Corazón Pelican la escribe en `ReclamacionNpc.Reclamar` y la retira en `VolverARutina` / liberación de inicio de jornada.
- **Puntos protegidos en Pelican 4Ever (`fe76df7` + `4766210`, integrados en `main` junto con `d5a2aa3` de `dev`):**
  1. `Pelican4Ever.Core/World/NpcShopkeeperLogic.cs` (`CanMicroWander`) + `Pelican4Ever/Modules/WorldLifeModule.cs`: omite el *micro-wandering* cuando `npc.modData.ContainsKey(CorazonPelicanControlKey)`.
  2. `Pelican4Ever/Modules/WorldLifeModule.cs` (`Prefix_CheckSchedule`): cede sin limpiar `controller` ni hacer `Halt()` si el NPC lleva `CorazonPelicanControlKey` (evita cortar rutas externas `!NPCSchedule`).
  3. `Pelican4Ever/Modules/RealTimeClockModule.cs` (`CatchUpSchedule`): no reposiciona al NPC cuando lleva `CorazonPelicanControlKey`.
- **Verificación registrada tras integrar `main` (`7fb03e9`) y cerrar tarea espejo en `Stardew Valley MORPG/backlog.md`:** `check_harmony_patches.py` `errors=0, warnings=1` (preexistente en `WorldLifeModule.cs:84`), `dotnet build -p:EnableModDeploy=false -p:EnableModZip=false` `0 Advertencia(s), 0 Errores`, `dotnet test --no-build` `1809/1809`.

## Fuera de alcance

- Cualquier otra dependencia de Pelican hacia Corazón.
