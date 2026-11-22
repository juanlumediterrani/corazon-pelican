---
id: CP-606
titulo: Multijugador farmhand · puente de diálogo y cartas de prórroga + API loopback solo en anfitrión
estado: en-curso
prioridad: P1
hito: F6
depende_de: [CP-503, CP-509, CP-601]
tipo: code
complejidad: alta
duracion_min: 240
sensibilidad: public
---

## Objetivo

Permitir que clientes *farmhand* conversen con Leah a través de la mente (y respondan a sus cartas de prórroga) manteniendo toda la autoridad del motor y del LLM en el anfitrión, y evitar colisiones del puerto `25560` si host y cliente comparten la carpeta `Mods/`.

## Contexto

Detectado en auditoría de integración multijugador:
1. `DialogoModule.PrefixCheckAction` filtra `who.IsLocalPlayer && Context.IsMainPlayer`: cuando un *farmhand* pulsa sobre Leah en su cliente, `NPC.checkAction` corre en el cliente sin pasar por el motor del host y muestra diálogo vanilla.
2. `CorreoProrrogaModule` entrega cartas con `onlyPlayerId`, pero escucha `Display.MenuChanged` (`LetterViewerMenu`) e invalida `Data/Mail` solo cuando `Context.IsMainPlayer`: en un cliente *farmhand* (incluyendo lectura desde `TownMailboxModule` en Pelican 4Ever), el menú de aceptar/rechazar prórroga nunca aparece.
3. `ApiModule.OnGameLaunched` abre `http://127.0.0.1:25560/` antes de entrar a partida y solo lo detiene en `OnSaveLoaded` si `!Context.IsMainPlayer`, lo que choca en el puerto cuando se ejecutan host y cliente locales compartiendo `Mods/`.

## Alcance

- Canal SMAPI (`Helper.Multiplayer.ModMessageReceived`) cliente ↔ host en `DialogoModule` y `CorreoProrrogaModule`:
  - El cliente reenvía al anfitrión la solicitud de diálogo (`who` / `npc` / `texto libre`) o la apertura/respuesta de carta `CorazonPelican.Prorroga.*` (validando autoría y caducidad en el host según `R-07`/`R-09`).
  - El anfitrión envía de vuelta el reflejo/respuesta pensada o la pregunta de prórroga y sincroniza `Data/Mail` al cliente tras responder.
- En `ApiModule`, permitir diferir la apertura del servidor loopback a `SaveLoaded` (solo cuando `Context.IsMainPlayer`, o mediante opción explícita de configuración si hiciera falta diagnóstico en título) para evitar bind duplicado en clientes locales.
- Tests unitarios en `CorazonPelican.Tests` sobre protocolos/validaciones de turnos y cartas por `UniqueMultiplayerID` remoto.

## Criterios de aceptación

- [x] Tests en `CorazonPelican.Tests`: un turno de diálogo y una carta de prórroga iniciados por un `farmer:<id>` distinto al anfitrión completan el flujo en Core con todas las guardas (`R-07`, `R-09`, `R-15`). (`MultijugadorDialogoProrrogaTests`, `415/415` superados; protocolo `ProtocoloMultijugador` y puente SMAPI `ModMessageReceived` implementados en `DialogoModule` y `CorreoProrrogaModule`)
- [x] En instancia cliente (`!Context.IsMainPlayer`), `ApiModule` no mantiene abierto ni colisiona en `ApiPuerto`. (`ApiModule` solo enlaza el servidor HTTP loopback en `SaveLoaded` cuando `Context.IsMainPlayer` y lo detiene en `ReturnedToTitle`)
- [ ] Verificación en vivo 🎮 con host Pelican + 1 cliente local comprobando conversación de *farmhand* y carta en `TownMailbox` / buzón de residencia. *(Pendiente de sesión 🎮 autorizada con host + cliente).*

## Fuera de alcance

- Ejecutar `mente/` en los clientes ni exponer la API HTTP del motor fuera de loopback del host.
