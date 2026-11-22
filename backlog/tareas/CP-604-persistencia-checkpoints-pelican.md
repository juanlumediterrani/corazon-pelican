---
id: CP-604
titulo: Persistencia de Corazón Pelican en checkpoints y guardados de rollover de Pelican 4Ever
estado: hecha
fecha: 2026-11-22
prioridad: P1
hito: F6
depende_de: [CP-107, CP-602]
tipo: code
complejidad: media
duracion_min: 120
sensibilidad: public
proyecto_destino: ambos (Corazón Pelican + Stardew Valley MORPG)
---

## Objetivo

Garantizar que `EstadoPartida` (diario, episodios, humor y compromisos) se vuelque antes de cada `SaveGame.Save()` ejecutado por `CheckpointModule` y `DailyRolloverModule` en Pelican 4Ever.

## Contexto

`CheckpointModule.cs` y `DailyRolloverModule.cs` (`preCloseSave` y `postCloseSave`) invocan `SaveGame.Save()` directamente. Como documenta `Pelican4Ever/Modules/PersistenceModule.cs` (`TASK-1602, gap G1`), SMAPI no dispara `GameLoop.Saving` en esos guardados fuera del menú nativo, por lo que `PersistenciaModule.OnSaving` en Corazón Pelican no llega a ejecutar `WriteSaveData(PersistenciaEstado.ClaveGuardado, ...)` durante checkpoints periódicos ni antes del cierre de peers en rollover.

## Alcance

- **Lado Pelican 4Ever (`Stardew Valley MORPG` · `TASK-2601`):** exponer un hook/notificación síncrona antes de `SaveGame.Save()` en `IPelicanApi` (`PelicanApi.cs`) —o invocar a los suscriptores registrados antes de `FlushToSaveData` / `SaveGame.Save()` en `CheckpointModule` y `DailyRolloverModule`— sin introducir dependencias de Pelican hacia Corazón Pelican.
- **Lado Corazón Pelican:** suscribir `PersistenciaModule` (vía `Helper.ModRegistry.GetApi<IPelicanApi>("Pelican4Ever.SVRTMP")` cuando Pelican esté cargado) para ejecutar `gestor.Guardar(...)` en cada pre-checkpoint / pre-rollover manteniendo la protección de `CP-110`.

## Criterios de aceptación

- [x] Test en `CorazonPelican.Tests`: invocar el callback de pre-guardado dispara el volcado de `EstadoPartida` con las guardas de `CP-110` intactas. (`ProteccionGuardadoTests.Hook_pre_guardado_vuelca_el_estado_antes_de_checkpoint_y_respeta_proteccion_cp110`, `413/413` superados; `PersistenciaModule` suscribe `IPelicanApi.RegisterPreSaveHook` al cargar y comparte `GuardarEnPartida`)
- [x] Test o verificación en `Pelican4Ever.Tests`: los puntos de `CheckpointModule` y `DailyRolloverModule` previos a `SaveGame.Save()` notifican el hook antes de serializar. (`PreSaveHookRegistryTests`, `1827/1827` superados; `PersistenceModule.FlushToSaveData` dispara `PreSaveHooks.InvokeAll` antes de `SaveGame.Save()`)
- [ ] Verificación de que tras un checkpoint manual (`p4e_checkpoint` o equivalente) y recarga, Corazón Pelican conserva diario, humor y compromisos posteriores al último amanecer. *(Pendiente de comprobación en juego vivo cuando se autorice sesión 🎮; cubierto por tests unitarios en ambos repos).*

## Fuera de alcance

- Cambiar el formato v2 de `PersistenciaEstado` o el esquema XML de `SaveBackupService`.
