---
id: CP-015
titulo: Spike V-03/V-05 · una textura generada como mueble colocable que sobrevive a guardar, cargar y multijugador 🎮
estado: dormida
prioridad: P1
hito: S
depende_de: [CP-006]
tipo: analysis
complejidad: media
duracion_min: 150
sensibilidad: public
vivo: si
---

## Objetivo

Demostrar el camino de una PNG cualquiera a un cuadro colgado en una pared que persiste y se ve en otro cliente.

## Contexto

V-03 resuelto en el decompilado: `FurnitureDataDefinition` lee la textura del campo 9 de `Data/Furniture` (por defecto `TileSheets\furniture`) y el índice de sprite del campo 8. V-05: SMAPI solo permite `ReadSaveData`/`WriteSaveData` al jugador principal (lanza `InvalidOperationException` en granjeros), compatible con la autoridad del host (ADS §10) pero hay que confirmarlo. Falta probar el ciclo completo y qué pasa al desinstalar.

## Alcance

- Registrar `Mods/CorazonPelican/Obras/{id}` con `AssetRequested` y una entrada `Data/Furniture` de tipo cuadro con ID de cadena con espacio de nombres.
- Colocar, guardar, cerrar, cargar.
- Enviar la textura a un cliente con `Helper.Multiplayer.SendMessage` y registrarla allí.
- Desinstalar el mod y cargar: observar qué queda (¿Error Item conservable? ¿se pierde?). Reinstalar y cargar.

## Criterios de aceptación

- [ ] `docs/spikes/v03-obra-mueble.md` con el formato exacto de la entrada `Data/Furniture` usada.
- [ ] En vivo: el cuadro sigue colgado tras guardar y cargar.
- [ ] En vivo: un cliente con el mod ve la textura correcta; uno sin él, lo que sea, documentado.
- [ ] Documentado qué ocurre al desinstalar y si reinstalar recupera las obras (entrada para CP-405).

## Fuera de alcance

- Portafolio y taller reales (CP-401, CP-403).

## Pospuesta a Fase 2 (2026-11-21)

Según el acuerdo [`P-13`](../../docs/decisiones.md), la generación visual de obras pasa a Fase 2 (después de validar primero obras en texto con **Elliott** en `CP-410`).
