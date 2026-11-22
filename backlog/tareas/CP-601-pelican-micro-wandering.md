---
id: CP-601
titulo: Pelican 4Ever · WorldLifeModule ignora NPCs reclamados por Corazón Pelican
estado: en-curso
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

## Criterios de aceptación

- [x] Test en Pelican: un NPC con la clave no recibe destinos de *micro-wandering*. (`NpcShopkeeperLogicTests.CanMicroWander_BlocksExternallyControlledNpc_CorazonPelican`, 40/40 superados en la rama `cp-601-micro-wandering` de `Stardew Valley MORPG`, commit `fe76df7` en worktree aislado sin interferir con el otro agente; pendiente fusionar en `main` e inscribir en su `backlog.md`)
- [x] La clave se documenta como contrato entre ambos mods (nombre exacto y semántica). (`NpcShopkeeperLogic.CorazonPelicanControlKey = "CorazonPelican.Control"` en Pelican 4Ever y `ReclamacionNpc.ClaveControlModData = "CorazonPelican.Control"` en Corazón Pelican)

## Fuera de alcance

- Cualquier otra dependencia de Pelican hacia Corazón.
