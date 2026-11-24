# Contrato de Interoperabilidad · `Corazón Pelican ↔ Pelican 4Ever`

* **Estado:** Vigente (`EST-001` v1.2)
* **Última actualización:** 2026-11-24
* **Propietario:** Corazón Pelican (`repo_id="corazon-pelican"`) · Contraparte en `MCP Pelican`: `Pelican 4Ever` (`docs/host-api.md`, `ADR-0001`, `ADR-0002`, `ADR-0013`)

---

## 1. Descubrimiento Sin Acoplamiento de Ensamblados (`IPelicanApi`)

`Corazón Pelican` nunca enlaza DLLs de `Pelican 4Ever` en tiempo de compilación. El acoplamiento es completamente opcional vía `Helper.ModRegistry.GetApi<IPelicanApi>("Pelican4Ever.SVRTMP")` (`CorazonPelican/Framework/ModContext.cs` e `IPelicanApi.cs`).

Métodos consumidos sobre `Pelican4Ever.SVRTMP`:
- `RegisterPreSaveHook(Action)` / `UnregisterPreSaveHook(Action)` (`PersistenciaModule.cs`): sincroniza el volcado del estado del habitante justo antes de un checkpoint/guardado gestionado por `Pelican 4Ever`.
- `RegisterTimeJumpHook(Action<int, int>)` / `UnregisterTimeJumpHook(Action<int, int>)` (`HabitanteModule.cs`): notifica saltos de reloj autoritativos del host para coherencia de ventana y cadencia de pulsión.
- `bool IsNightHold()` y `bool IsRolloverBusy()`: congelan o degradan de forma segura decisiones de movimiento/diálogo cuando el servidor se encuentra en retención nocturna o transición de día (*rollover*).

Cuando `Pelican4Ever.SVRTMP` no está presente, `ModoRelojEfectivo` degrada limpiamente a modo autónomo vanilla sin errores en consola.

---

## 2. Bus de Eventos del Host (`IntegracionPelicanModule`)

Cuando `CorazonConfig.PelicanApiUrl` está configurado (`CorazonPelican.Core/Config/CorazonConfig.cs`):
- `IntegracionPelicanModule` consulta en segundo plano `GET /api/v1/host/events?since={ultimoEventoId}` cada `2_000 ms` (`timeout = 5 s`), enviando opcionalmente `Authorization: Bearer <PelicanApiToken>`.
- Los eventos deserializados por `ConversorSucesosPelican.ParsearRespuestaJson` se encolan en `Context.SucesosPelican` (`CorazonPelican.Core/Sensorio/SucesosPelican.cs`) para su consumo seguro en el hilo principal en el siguiente tick de observación.
- Si `PelicanApiUrl` no está configurado o el host aún no arranca, el sondeo se omite o reintenta en silencio sin degradar la partida local.

---

## 3. Control de NPC y Convivencia (`modData["CorazonPelican.Control"]`)

- `ReclamacionNpc.ClaveControlModData = "CorazonPelican.Control"` se escribe en `NPC.modData` con `<motivo>@<timeOfDay>` mientras `Corazón Pelican` gobierna un habitante (junto con `ignoreScheduleToday = true` y gestión de `PathFindController` sin teletransporte visible).
- `CuerpoNpc` coordina movimientos, pausas y devolución de control sin destruir `NPC.Schedule`.
- En entornos headless/servidor (`Pelican 4Ever` / `HeadlessEventGuard`), el anfitrión ejecuta el bucle autoritativo (`HabitanteModule`, `PersistenciaModule`) mientras los farmhands reciben estado vía mensajes SMAPI.

---

## 4. Protocolo Multijugador de Diálogo y Prórroga por Carta

Definido en `CorazonPelican.Core/Dialogo/ProtocoloMultijugador.cs` para `DialogoModule` y `CorreoProrrogaModule`:
- **Claves en `modData`:** `CorazonPelican.DialogoPreparado`, prefijo `CorazonPelican.DialogoUltimo.<playerId>` y `CorazonPelican.DialogoActivo`.
- **Mensajes de diálogo libre:** `CorazonPelican.Dialogo.Iniciar`, `CorazonPelican.Dialogo.Resultado`, `CorazonPelican.Dialogo.Consumido`, `CorazonPelican.Dialogo.EntregarIniciativa`, `CorazonPelican.Dialogo.ClienteIdioma`.
- **Mensajes de prórroga por carta:** `CorazonPelican.Prorroga.SincronizarMail`, `CorazonPelican.Prorroga.AbrirCarta`, `CorazonPelican.Prorroga.MostrarPregunta`, `CorazonPelican.Prorroga.ResponderCarta`, `CorazonPelican.Prorroga.InvalidarMail`.
- Invariante de frontera: este protocolo transporta estado y texto entre cliente y anfitrión dentro de Stardew; no mezcla canales de pensamiento de `mente/` entre jugadores.

---

## 5. Pruebas de Referencia y *Write-Back*

- Cualquier cambio en `IPelicanApi`, `IntegracionPelicanModule`, `ConversorSucesosPelican` o `ProtocoloMultijugador` obliga a revisar compatibilidad con `Pelican4Ever` (`read_contract("pelican4ever", "host-api")` en `MCP Pelican`) y actualizar este documento en el mismo commit.
- Suite de referencia: `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (`SucesosPelicanTests`, `ProtocoloMultijugador`, `DegradacionVanillaTests`, `LimpiezaControlCP610Tests`).
