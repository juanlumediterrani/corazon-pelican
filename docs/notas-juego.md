# Notas del juego y del entorno

Hechos comprobados el 2026-11-04 sobre el juego instalado, su decompilado y los proyectos
hermanos. Son la base de las verificaciones V-xx de la ADS §16 y de varias tareas del backlog.
Lo que aquí figura como comprobado se leyó en código o en datos del juego; lo que no, se dice.

## 1. Entorno local

| Qué | Dónde | Nota |
|---|---|---|
| Juego | `/home/mtx/.steam/debian-installation/steamapps/common/Stardew Valley/` | **1.6.15.24356**, SMAPI **4.5.2** |
| Decompilado completo del juego | `/home/mtx/.gemini/antigravity/brain/bb7844c8-360d-4d66-95b7-53ac5f845a71/scratch/decomp/` | Misma versión (`AssemblyFileVersion 1.6.15.24356`). Es una carpeta de trabajo de otra herramienta: puede desaparecer. Si falta, decompilar `Stardew Valley.dll` con `ilspycmd` |
| Lector de `.xnb` | [`tools/xnbdump/`](../tools/xnbdump/) | Vuelca `Dictionary<string,string>` de `Content/` (horarios, animaciones…) |
| .NET | `~/.dotnet/dotnet` | SDK con net8.0 |
| Mods instalados | `Mods/` del juego | Pelican4Ever, Pelican4Ever.NITVCompat, Dr4coVoyagerBridge, ContentPatcher, SpaceCore, FashionSense, NatureInTheValley… |
| Pelican 4Ever | `/home/mtx/Documentos/Projects/Stardew Valley MORPG/` | Ver §10 |
| Stardew Voyager | `/home/mtx/Documentos/Projects/stardew-voyager/` | Ver §10 |

Los `.xnb` de `Content/` están **comprimidos** (cabecera `XNBw`, bandera `0x81`): `strings` no
sirve. `tools/xnbdump` los lee con el `ContentManager` de MonoGame y un proveedor de servicios
nulo, sin escribir el contenido a disco. **El contenido del juego no se versiona.**

## 2. Conversación con un NPC (V-01) · CP-010, CP-503

`NPC.checkAction(Farmer who, GameLocation l)` es `virtual` (`StardewValley/NPC.cs`, ~l. 2465),
pero hace mucho más que mostrar diálogo. En orden aproximado:

1. Sale si el NPC es invisible, está dormido (emote) o el jugador no puede moverse.
2. Regalos: `who.ActiveObject != null && tryToReceiveActiveObject(who)`.
3. Misiones de socializar: `who.NotifyQuests(q => q.OnNpcSocialized(this))` (p. ej. *Introductions*).
4. Casos especiales (Krobus, Henchman, Jas en el desierto) y lógica de cónyuge.
5. Diálogo y `grantConversationFriendship(who, 20)` (+20 de amistad diarios; definido ~l. 2924).

**Consecuencia:** un prefijo de Harmony que devuelva `false` sin más pierde la amistad diaria y
el progreso de misiones. La intercepción debe cambiar solo *qué se dice* y dejar al juego el resto
(propuesta P-7, pendiente).

## 3. Horarios de los NPC (V-02) · CP-011, CP-201, CP-306

- El horario del día es `Dictionary<int, SchedulePathDescription> Schedule` (propiedad con
  setter privado). `dayScheduleName` (net, solo lo escribe el host) guarda la clave elegida.
- `checkSchedule(timeOfDay)` encola la ruta **solo en la hora exacta** de cada entrada y avanza
  `lastAttemptedSchedule`. Si el NPC está reclamado cuando pasa una entrada, **esa ruta se
  pierde**: `volver_a_rutina` tiene que calcular por sí mismo la ruta al tramo vigente.
- Campos y métodos útiles: `followSchedule`, `ignoreScheduleToday`, `controller`,
  `temporaryController`, `ClearSchedule()`, `TryLoadSchedule()`, `DirectionsToNewLocation`,
  `isMovingOnPathFindPath`.
- **Cómo elige el juego el horario de cada día** (`TryLoadSchedule()`, por prioridad; `hearts` =
  amistad/250):
  1. `GreenRain` (año 1) y festivales pasivos (`marriage_<fest>_<día>`, `<fest>_<día>`…).
  2. Casado: `marriage_<estación>_<día>`, `marriage_<díaSemana>`…
  3. `<estación>_<día>` → `<día>_<hearts>` → `<día>`.
  4. Casos de progreso (p. ej. Pam y `bus` tras la carta `ccVault`).
  5. `rain2` / `rain` si llueve.
  6. `<estación>_<díaSemana>_<hearts>` → `<estación>_<díaSemana>` → `<díaSemana>_<hearts>` → `<díaSemana>`.
  7. `<estación>`, y `spring` por defecto.
- Dentro de un horario hay directivas: `GOTO <clave>` (y `GOTO season`, `GOTO no_schedule`, que
  pone `followSchedule = false`), `NOT friendship …` y `MAIL <carta>` (el avance de la partida
  cambia la rutina).

### 3.1. Horario real de Leah (no casada)

Leído con `tools/xnbdump` de `Characters/schedules/Leah`. Formato de cada entrada: hora,
ubicación, casilla, orientación y animación final opcional.

| Clave (cuándo aplica) | Entradas |
|---|---|
| `spring` | 9:00 casa (3,7) `leah_sculpt` · 12:00 Forest (68,50) · 15:00 Forest (33,25) `leah_draw` · 18:30 Forest (21,29) · 19:30 casa · 22:00 casa · 24:00 cama `leah_sleep` |
| `summer` (con `MAIL beachBridgeFixed`) | 9:00 `leah_sculpt` · 12:00 Beach (72,18) `leah_draw` · 19:00 casa · 22:00 casa · 24:00 cama |
| `summer_noBridge` | igual, pero dibuja en Beach (38,24) |
| `fall` | 9:00 `leah_sculpt` · 12:00 Town (86,66) `square_1_5_3` · 19:00 casa · 22:00 casa · 24:00 cama |
| `Mon` (cualquier estación: gana a `<estación>`) | 9:00 `leah_sculpt` · 11:00 SeedShop (11,23) · 17:00 casa · 22:00 casa · 24:00 cama |
| `Fri`; `Sat`, `rain` y `winter` → `GOTO Fri` | 9:00 `leah_sculpt` · 12:00 casa · 14:00 casa · 16:00 Saloon (2,20) · 23:40 cama |
| Días especiales | `spring_16` (clínica 10:30–13:30, Saloon 16:00), `winter_15` (Beach 16:00), `GreenRain`, `DesertFestival_1`, `marriage_*` |

La pasarela del lago (Mountain) no aparece en su horario: es un lugar para intenciones propias.

### 3.2. Cadencia estimada a partir de ese horario · CP-208

- Todas las entradas como hito: 6–8 pulsos por día de juego (la entrada de la cama es el propio
  acostarse), uno cada 2–3 h de juego.
- Solo cambios de sitio o de actividad, más despertar y acostarse: **4–6 por día de juego**, uno
  cada 3–4 h de juego. Los huecos largos son las tardes (12:00–19:00 en la playa o la plaza; el
  Saloon hasta las 23:40), donde actúan la deriva y las interrupciones.
- Pelican (1:1): uno cada 3–4,5 h reales, en línea con la expectativa de 4–6 h.
- Vanilla (día ≈ 14 min reales): uno cada 2–3 min reales, algo por encima de la expectativa de
  5–15 min. Medidas en días de juego, las dos expectativas coinciden (≈ 3–4 por día): basta con
  anotar menos hitos en vanilla si hace falta.
- Coste: cada cadena son ~3–4 llamadas; un día de rutina, ~10–14 llamadas. El presupuesto de 24
  pulsos/día del perfil de ejemplo queda holgado; probablemente basten 10–12.
- Sin medir: el hito salta al disponerse con la entrada siguiente (más el trayecto); en
  festivales no hay horario; las intenciones propias suman pulsos.

## 4. Animaciones de Leah (V-07) · CP-012, CP-302

De `Data/animationDescriptions` (formato `inicio/bucle/fin[/texto]`, en frames del sprite):

| Clave | Inicio | Bucle | Fin | Uso en su horario |
|---|---|---|---|---|
| `leah_draw` | 35 | 32–35 (larga, con pausas en 35) | 35 | Bosque (primavera 15:00), playa (verano 12:00) |
| `leah_sculpt` | 19 | 16 17 18 19 19… | 19 | Cada mañana a las 9:00 en casa |
| `leah_sleep` | 51 | 51 | 51 | Cama |
| `leah_beach_drink` | 16 | 16… 17 18 18 18 17 … 19 | 16 | Sin uso en los horarios leídos |

Leah es zurda; se nota en estas animaciones.

## 5. Obras como muebles (V-03, V-04) · CP-013, CP-015, CP-403

- `FurnitureDataDefinition` (`StardewValley.ItemTypeDefinitions/`) lee de `Data/Furniture`: campo
  **8** índice de sprite, campo **9** textura (por defecto `TileSheets\furniture`), campo **10**
  booleano «fuera de venta aleatoria», campo 7 nombre mostrado, campo 0 nombre. Campo 2 es el
  tamaño en casillas (sin medir aún para los cuadros: V-04).
- Los IDs de objeto en 1.6 son cadenas: usar un espacio de nombres propio para las obras.

## 6. Texto libre (V-06) · CP-014

`StardewValley.Menus.TextEntryMenu` es el **teclado en pantalla para mando** (ventana de 168×88
con teclas), no un campo de texto de PC. Candidatos: `NamingMenu` (usa `TextBox` y botón OK) o un
`IClickableMenu` propio con `TextBox`. También existen `ChatTextBox` y `TitleTextInputMenu`.

## 7. Tiempo y pausa · CP-503

`Game1.shouldTimePass()`: no pasa el tiempo en festival, boda, `farmEvent`, evento, pausa o
`freezeControls`. **En partida de un jugador, cualquier `activeClickableMenu` abierto (salvo
`BobberBar`) para el reloj**: la espera de la mente con una `DialogueBox` abierta no cuesta tiempo
de juego, pero sí paciencia real. **En multijugador** decide `netWorldState.IsTimePaused` y el
reloj sigue.

Escalas: vanilla, 10 min de juego ≈ 7 s reales (1 h ≈ 42 s; un día de 6:00 a 2:00 ≈ 14 min).
Pelican: 1:1, con el reloj retenido en `2550` de 01:50 a 06:00 reales.

## 8. Persistencia en multijugador (V-05) · CP-107, CP-015

El `DataHelper` de SMAPI lanza `InvalidOperationException` si no se es el jugador principal
(«save files are stored on the main player's computer»). Visto en un log de SpaceCore, no en la
documentación oficial: confirmar en vivo. Encaja con la autoridad del host.

## 9. ValleyTalk (V-08) · CP-505

Mod de Nexus **30319**, autor **dandm1**; diálogo generado por LLM para todo el pueblo. No se
encontró su UniqueID publicado: leerlo del `manifest.json` del mod instalado.

## 10. Proyectos hermanos: qué reutilizar

**Pelican 4Ever** (`Stardew Valley MORPG/`):

- `Pelican4Ever/Modules/AdminApiModule.cs`: servidor `HttpListener` (patrón para `ApiModule`).
- `Pelican4Ever.Core/Admin/MainThreadActionQueue.cs`: cola al hilo principal.
- `Pelican4Ever/Framework/IModule.cs`: registro de módulos aislados.
- `Pelican4Ever/Modules/WorldLifeModule.cs`: *micro-wandering* que debe ignorar
  `modData["CorazonPelican.Control"]` (CP-601).
- `docs/host-api.md`: `GET /api/v1/host/events?since=N` (CP-602).
- Ojo: compilar con `-p:EnableModDeploy=false` para no desplegar al juego en comprobaciones.

**Stardew Voyager** (`stardew-voyager/`):

- Formato de tareas de este backlog: `.dr4co/plantilla-tarea.md`; índice `backlog/README.md`;
  ciclo en `AGENTS.md` (una tarea, un commit `<ID>: …`).
- `docs/dialoguebox-notes.md` y `docs/menu-contract.md`: cómo opera menús nativos un agente.
- `voyager/data/npc_schedules.json`: solo Penny y Alex, desde la wiki.
- Piezas para CP-911: `@skill` con pre y postcondiciones, `/path` y colisión nativas (SV-025),
  `/can_act` (SV-026), resolver en vez de rechazar (SV-028), `DayRunner` (refactor v4).

## 11. Generadores de imagen candidatos (P-3) · CP-013

Vistos, sin probar. Ninguno documenta control de paleta (lo pone el script de asset):

- [UmeAiRT · FLUX.1-dev Modern Pixel art](https://huggingface.co/UmeAiRT/FLUX.1-dev-LoRA-Modern_Pixel_art/tree/main/images) (LoRA).
- [deAPI · pixel art FLUX](https://huggingface.co/deAPI-ai/pixelartdotfluxlora/blob/main/README.md) — licencia FLUX.1 [dev] **no comercial**.
- [Mystic07 · spritesheets sobre FLUX.2-klein](https://huggingface.co/Mystic07/flux-lora-spritesheet).
- [Space gsbm/sdxl-pixelart](https://huggingface.co/spaces/gsbm/sdxl-pixelart/tree/main) — daba error de ejecución.

## 12. Fuentes externas

- [ValleyTalk en Nexus](https://nexusmods.com/stardewvalley/mods/30319) y [nota de prensa](https://www.gamingbible.com/news/platform/pc/stardew-valley-valleytalk-endless-dialogue-mod-pc-961075-20251223).
- [Hilo de SpaceCore con el error del `DataHelper`](https://forums.playstarbound.com/threads/spacecore.159776/).
- La wiki de Stardew (`stardewvalleywiki.com`) está detrás de un control anti-bots: los datos de
  Leah se leyeron del juego instalado en su lugar.

## 13. Correo individual y respuesta en Stardew 1.6 · CP-509

Comprobado en el decompilado local de Stardew Valley 1.6.15 (`GameLocation.cs`, `Game1.cs`,
`FarmerTeam.cs`, `SetMailRequest.cs`, `LetterViewerMenu.cs` y `CollectionsPage.cs`):

- **Contenido dinámico (`Data/Mail`):** el diccionario `Data/Mail` (`Dictionary<string, string>`,
  formato `cuerpo[#]Título` con `^` como salto de línea) se amplía desde SMAPI con
  `Content.AssetRequested` e invalidación de caché (`GameContent.InvalidateCache("Data/Mail")`)
  cuando nace una carta nueva.
- **Entrega individual (`SetMailRequest`):** `Game1.player.team.RequestSetMail(PlayerActionTarget.All, mailId, MailType.Now, add: true, onlyPlayerId: farmer.UniqueMultiplayerID)`
  encola la carta exclusivamente en `farmer.mailbox` del destinatario indicado (aplicación local
  inmediata si es el anfitrión y envío por red por `onlyPlayerId` en multijugador). Conviene
  comprobar `!farmer.hasOrWillReceiveMail(mailId)` para evitar entregas repetidas.
- **Legibilidad tras el vencimiento:** al leer la carta del buzón, Stardew mueve su `mailId` a
  `farmer.mailReceived`, por lo que sigue accesible para lectura desde Colecciones
  (`CollectionsPage` → `LetterViewerMenu(mail, mailTitle, fromCollection: true)`).
- **Vía de respuesta:** `LetterViewerMenu` solo muestra texto y su campo público `mailTitle`
  conserva el `mailId` original (`GameLocation.mailbox()`). No ofrece botones propios; para que
  el destinatario responda desde la propia carta, `Display.MenuChanged` detecta el cierre
  (`e.OldMenu is LetterViewerMenu letter && e.NewMenu is null`) con el prefijo
  `CorazonPelican.Prorroga.` y, si la petición sigue abierta para ese lector, abre
  `GameLocation.createQuestionDialogue(pregunta, respuestas, callback)` (operable tanto por un
  humano como por el arnés de menú de Voyager). El callback revalida destinatario, estado y hora
  viva de juego al confirmarse la elección.
