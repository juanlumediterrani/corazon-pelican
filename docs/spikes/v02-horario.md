# Spike V-02 · Reclamar a un NPC, moverlo por rutas nativas y devolverlo a su horario (`CP-011`)

- **Estado:** completada y verificada en vivo (2026-11-22) sobre Stardew Valley `1.6.15.24356` + SMAPI `4.5.2`, partida de desarrollo aislada `CorazonDev_454975123` y prototipo [`SpikesModule.cs`](../../CorazonPelican/Modules/SpikesModule.cs).
- **Alimentación directa:**
  - [`CP-303`](../../backlog/tareas/CP-303-reclamacion-devolucion.md) (reclamación `modData["CorazonPelican.Control"]`, `ir_a` y `volver_a_rutina`);
  - [`CP-201`](../../backlog/tareas/CP-201-hitos-rutina.md) (detección del fin de cada entrada del horario y mapeo de `SchedulePathDescription`);
  - [`CP-306`](../../backlog/tareas/CP-306-saltar-sustituir-hito.md) (saltar o sustituir una entrada concreta del horario sin romper las siguientes).

---

## 1. Campos y métodos de `NPC` y `SchedulePathDescription` (`1.6.15.24356`)

### 1.1. Campos de `NPC` (`StardewValley/NPC.cs`)

| Miembro | Tipo y visibilidad | Líneas (`NPC.cs`) | Papel exacto en el horario y en la reclamación |
|---|---|---|---|
| `Schedule` | `public Dictionary<int, SchedulePathDescription>` (`private set`) | 482–492 | Mapa `horaHHMM -> SchedulePathDescription` cargado para el día actual por `TryLoadSchedule`. |
| `ScheduleKey` / `dayScheduleName` | `public string ScheduleKey` / `NetString dayScheduleName` | 305, 495–502 | Clave de `Characters\schedules\<NPC>` elegida hoy por el anfitrión (p. ej. `"spring"`, `"rain"`, `"Fri"`, `"fall"`). |
| `followSchedule` | `public bool` | 336 | Si es `false`, `NPC` no sigue horario (`GOTO no_schedule` o `ClearSchedule()`). |
| `ignoreScheduleToday` | `public bool` | 333 | Si es `true`, `NPC.checkSchedule(timeOfDay)` (`l. 4107`) y `Pelican4Ever.RealTimeClockModule.CatchUpSchedule` (`RealTimeClockModule.cs:279`) **ignoran al NPC sin borrar `Schedule`**. |
| `lastAttemptedSchedule` | `public int` | 361, 4112–4114, 6092 | Última hora `timeOfDay` procesada por `checkSchedule`. Se reinicia a `-1` en `NPC.dayUpdate(dayOfMonth)` (`l. 6092`), **no** en `resetForNewDay`. |
| `queuedSchedulePaths` | `public List<SchedulePathDescription>` | 358, 4118, 4131–4143 | Cola de tramos cuya hora ya llegó mientras seguía activo un `controller` anterior. |
| `DirectionsToNewLocation` | `public SchedulePathDescription` (respalda `private directionsToNewLocation`) | 122, 412–422, 4139 | Tramo horario actualmente en marcha; `PathFindController.moveCharacter` lo pone a `null` al agotar `pathToEndPoint` (`PathFindController.cs:280`). |
| `controller` | `public PathFindController` (heredado de `Character`) | `Character.cs`, `NPC.cs:4122, 4144` | Controlador de camino activo. **Detalle crítico:** cuando un tramo termina con animación de fin de ruta (`doingEndOfRouteAnimation.Value == true`), `controller` **no se pone a `null`** inmediatamente, sino que queda con `controller.pathToEndPoint.Count == 0` (`NPC.cs:4123` comprueba `pathFindController.pathToEndPoint?.Count > 0`). |
| `temporaryController` | `public PathFindController` | `Character.cs`, `NPC.cs:4137, 4215` | Controlador auxiliar (p. ej. salida de `FarmHouse` si está casada). Mientras no sea `null`, `checkSchedule` pospone el arranque de `DirectionsToNewLocation`. |
| `previousEndPoint` | `public Point` | 341, 4156 | Última casilla de destino alcanzada por un tramo de horario. |
| `doingEndOfRouteAnimation` / `goingToDoEndOfRouteAnimation` | `public readonly NetBool` | 364, 369, 4393, 4649 | Indican si el NPC va a iniciar o está reproduciendo una animación de `Data/animationDescriptions` al final del tramo (`leah_sculpt`, `leah_draw`, `leah_sleep`). |
| `endOfRouteBehaviorName` / `endOfRouteMessage` | `public readonly NetString` | 372, 391, 4632–4648 | Nombre de la animación o comportamiento (`square_*` o clave de `Data/animationDescriptions`) y diálogo de fin de ruta. |
| `EndActivityRouteEndBehavior()` | `public void` | 4612–4615 | Invoca `finishEndOfRouteAnimation()` (`l. 4163`), deteniendo limpiamente cualquier animación o paseo en cuadrado (`square_*`) del tramo anterior. |
| `StartActivityRouteEndBehavior(string, string)` | `public void` | 4617–4620 | Invoca `getRouteEndBehaviorFunction(behavior_name, end_message)?.Invoke(this, currentLocation)` para arrancar la animación o paseo final de un tramo. |
| `pathfindToNextScheduleLocation(...)` | `public virtual SchedulePathDescription` | 5344–5417 | Calcula la pila completa `Stack<Point> route` entre `(startingLocation, startingX, startingY)` y `(endingLocation, endingX, endingY)`, atravesando múltiples mapas mediante `WarpPathfindingCache.GetLocationRoute` y `PathFindController.findPathForNPCSchedules`. |

### 1.2. Campos de `SchedulePathDescription` (`StardewValley.Pathfinding/SchedulePathDescription.cs`)

| Campo | Tipo | Uso para anotaciones por patrón del perfil (`CP-201`) y recálculo (`CP-303`/`CP-306`) |
|---|---|---|
| `time` | `public int` | Hora `HHMM` del tramo (p. ej. `900`, `1200`, `1500`, `1600`, `2340`, `2400`). |
| `targetLocationName` | `public string` | Nombre interno del mapa de destino (`"LeahHouse"`, `"Forest"`, `"SeedShop"`, `"Saloon"`, `"Town"`, `"Beach"`). |
| `targetTile` | `public Point` | Casilla `(X, Y)` final del tramo en `targetLocationName`. |
| `facingDirection` | `public int` | Orientación final (`0..3`) al llegar a `targetTile`. |
| `endOfRouteBehavior` | `public string` | Animación o patrón de llegada: `"leah_sculpt"`, `"leah_draw"`, `"leah_sleep"`, `"square_1_5_3"`, o `null`. |
| `endOfRouteMessage` | `public string` | Clave o texto de diálogo asociado al tramo, o `null`. |
| `route` | `public Stack<Point>` | **Pila mutable de casillas:** `PathFindController` hace `Pop()` en cada casilla recorrida (`PathFindController.cs:416`). Además, `parseMasterScheduleImpl` (`NPC.cs:5703`) la precalcula a las `06:00` asumiendo que el NPC sale del `targetTile` de la **entrada inmediatamente anterior**. |

> [!IMPORTANT]
> Como `parseMasterScheduleImpl` precalcula cada `SchedulePathDescription.route` a las `06:00` encadenando el destino de la entrada `i - 1` con el de la entrada `i`, si una entrada se salta/sustituye (`CP-306`) o el NPC empieza un tramo desde otra casilla tras haber sido reclamado (`CP-303`), **hay que recalcular `Schedule[hora]` con `npc.pathfindToNextScheduleLocation(...)` desde la posición real actual del NPC** antes de construir el `PathFindController`.

---

## 2. Selección de la clave de horario del día (`ScheduleKey`)

En `NPC.TryLoadSchedule()` (`NPC.cs:5780-5900`) y `parseMasterScheduleImpl` (`NPC.cs:5480-5560`), el anfitrión elige la clave de `Characters\schedules\<NPC>` en este orden de prioridad:

1. **Festival pasivo activo** (`ActivePassiveFestivals`, p. ej. Mercado Nocturno o Festival del Desierto) si no está casada.
2. **Matrimonio (`isMarried()`):** `"marriage_" + dayName` → `"marriageJob"` → sin horario.
3. **Fecha exacta de estación:** `"<currentSeason>_<dayOfMonth>"` (p. ej. `"winter_15"`, `"winter_16"`, `"winter_17"`).
4. **Día del mes + corazones (`friendship` descendente de `14..1`) o día del mes:** `"<dayOfMonth>_<hearts>"` → `"<dayOfMonth>"`.
5. **Lluvia (`Game1.isRaining`):**
   - Si existe `"rain2"` y `Game1.random.NextBool()` (50 %): carga `"rain2"`.
   - Si no (o no existe `"rain2"`): carga `"rain"`.
6. **Día de la semana + corazones o día de la semana:** `"<shortDayName>_<hearts>"` → `"<shortDayName>"` (`"Mon"`, `"Tue"`, `"Wed"`, `"Thu"`, `"Fri"`, `"Sat"`, `"Sun"`).
7. **Estación actual:** `"<currentSeason>"` (`"spring"`, `"summer"`, `"fall"`, `"winter"`).
8. **Respaldo de primavera / por defecto:** `"spring_<shortDayName>"` → `"spring"` → `"default"`.
9. **Directivas internas del script (`parseMasterScheduleImpl`, `NPC.cs:5514-5555`):**
   - `NOT friendship <NPC> <corazones>` salta al sub-bloque siguiente si se supera el umbral;
   - `MAIL <flag>` ramifica según cartas recibidas (`Game1.MasterPlayer.mailReceived` o `NetWorldState`);
   - `GOTO <clave>` o `GOTO season` redirige a otra entrada (o `GOTO no_schedule` desactiva `followSchedule`).

---

## 3. Protocolo verificado de reclamación, movimiento, devolución y salto

```mermaid
sequenceDiagram
    participant Motor as Corazón Pelican
    participant NPC as NPC (Leah)
    participant P4E as Pelican4Ever (RealTimeClock)
    participant PFC as PathFindController

    Note over Motor,NPC: 1. Reclamación (cp_v02_reclamar)
    Motor->>NPC: modData["CorazonPelican.Control"] = "reclamado@HHMM"
    Motor->>NPC: ignoreScheduleToday = true
    Motor->>NPC: controller = null; temporaryController = null; queuedSchedulePaths.Clear()
    Motor->>NPC: EndActivityRouteEndBehavior(); Halt()
    Note right of P4E: CatchUpSchedule ve ignoreScheduleToday == true\ny no teletransporta a Leah

    Note over Motor,PFC: 2. Movimiento nativo (cp_v02_ir_a)
    Motor->>NPC: ruta = pathfindToNextScheduleLocation(ScheduleKey, locActual, x, y, locDest, dx, dy, dir, null, null)
    Motor->>PFC: npc.controller = new PathFindController(ruta.route, npc, npc.currentLocation)
    PFC->>NPC: Camina casilla a casilla y cruza puertas (handleWarps) sin teletransporte en vista

    Note over Motor,PFC: 3. Devolución al horario (cp_v02_volver)
    Motor->>NPC: modData.Remove("CorazonPelican.Control"); ignoreScheduleToday = false
    Motor->>NPC: lastAttemptedSchedule = Game1.timeOfDay
    Motor->>NPC: Busca tramoVigente (max Key <= timeOfDay en npc.Schedule)
    Motor->>NPC: rutaRetorno = pathfindToNextScheduleLocation(..., locActual, x, y, tramoVigente.target...)
    Motor->>PFC: npc.controller = new PathFindController(rutaRetorno.route, ...) + endBehavior del tramo
```

### Garantías del protocolo

1. **Cero teletransporte dentro de la vista del jugador:**
   - Ni la reclamación, ni `ir_a`, ni `volver_a_rutina` llaman a `Game1.warpCharacter`.
   - Todo desplazamiento usa `npc.pathfindToNextScheduleLocation` + `new PathFindController(ruta.route, npc, npc.currentLocation)`.
   - Los cambios de mapa ocurren únicamente cuando el NPC pisa físicamente la puerta o borde de mapa y `PathFindController.handleWarps` (`PathFindController.cs:350-423`) ejecuta la transición de puerta nativa (con sonido `"doorClose"` si el jugador está presente).
2. **Convivencia con `Pelican4Ever.RealTimeClockModule`:**
   - `RealTimeClockModule.CatchUpSchedule` (`RealTimeClockModule.cs:279`) sale inmediatamente cuando `npc.ignoreScheduleToday == true`.
   - Al activar `npc.ignoreScheduleToday = true` junto con `modData["CorazonPelican.Control"]`, `Pelican4Ever` nunca recoloca por salto a un NPC reclamado.
3. **Detección del fin de cada entrada del horario (`CP-201`):**
   - Un `HarmonyPostfix` sobre `NPC.checkSchedule(int timeOfDay)` envuelve `npc.controller.endBehaviorFunction` cuando se asigna un controlador de ruta nuevo (`DirectionsToNewLocation != null`).
   - Al llegar a `targetTile` e invocarse `endBehaviorFunction` (`PathFindController.cs:283`), se emite el evento `[V02-HitoFin]` con `(ScheduleKey, hora, targetLocationName, targetTile, facingDirection, endOfRouteBehavior, endOfRouteMessage)`.
4. **Saltar o sustituir una entrada (`CP-306`):**
   - Eliminar `npc.Schedule.Remove(hora)` o reemplazar `npc.Schedule[hora]` y recalcular el origen de la siguiente entrada (`horaSiguiente > hora`) con `pathfindToNextScheduleLocation` permite omitir el Saloon (`1600`) dejando a Leah en su ubicación previa (o llevándola a otra) y logrando que la entrada de las `2340` (`leah_sleep`) se cumpla intacta.
5. **Persistencia y cambio de día con NPC reclamada:**
   - `modData` se serializa en el XML de la partida, mientras que `ignoreScheduleToday` es `[XmlIgnore]`.
   - En `DayStarted` y `SaveLoaded`, si el NPC conserva `modData["CorazonPelican.Control"]` del día o sesión anterior, se limpia la clave, se asegura `ignoreScheduleToday = false`, `followSchedule = true` y, si `Schedule == null`, se invoca `TryLoadSchedule()`. Así nunca amanece congelada al día siguiente.

---

## 4. Verificación en vivo (`CorazonDev_454975123`)

Pruebas ejecutadas en la partida DEV `CorazonDev_454975123` con [`SpikesModule.cs`](../../CorazonPelican/Modules/SpikesModule.cs):

### 4.1. Reclamación 13:00–15:10 cruzando la entrada de las 15:00 y devolución antes de las 16:00 (`spring 2`, martes soleado)

```text
[Corazón Pelican] [V02-Escenario] Configurado spring día 2 (Tue) lluvia=False hora=0600 -> npc=Leah ScheduleKey=spring entradas=7 pos=LeahHouse@{X:2 Y:4}
[Corazón Pelican] [V02-Horario]   0900 -> LeahHouse@(3,7) dir=3 anim=leah_sculpt msg=- pasosRuta=1
[Corazón Pelican] [V02-Horario]   1200 -> Forest@(68,50) dir=2 anim=- msg=- pasosRuta=62
[Corazón Pelican] [V02-Horario]   1500 -> Forest@(33,25) dir=2 anim=leah_draw msg=- pasosRuta=77
[Corazón Pelican] [V02-Horario]   1830 -> Forest@(21,29) dir=1 anim=- msg=- pasosRuta=33
[Corazón Pelican] [V02-Horario]   1930 -> LeahHouse@(8,5) dir=0 anim=- msg=- pasosRuta=111
[Corazón Pelican] [V02-Horario]   2200 -> LeahHouse@(13,10) dir=2 anim=- msg=- pasosRuta=11
[Corazón Pelican] [V02-Horario]   2400 -> LeahHouse@(2,4) dir=3 anim=leah_sleep msg=- pasosRuta=18
[Corazón Pelican] [V02-Reclamar] npc=Leah reclamada a las 1300 | modData[CorazonPelican.Control]=reclamado@1300 | posAntes=Forest@{X:68 Y:50} posDespues=Forest@{X:68 Y:50} sinTeletransporte=True ScheduleConservado=7 entradas (clave=spring)
[Corazón Pelican] [V02-IrA] npc=Leah iniciando ruta nativa Forest@(68,50) -> LeahHouse@(8,8) dir=2 | pasos=58 sinTeletransporte=True
[Corazón Pelican] [V02-IrA-Fin] npc=Leah llegó por PathFindController a LeahHouse@{X:8 Y:8} dir=2
[Corazón Pelican] [V02-Caminar] npc=Leah ticks=1801 completado=True recorrido=[Forest@{X:68 Y:50} -> LeahHouse@{X:7 Y:9} -> LeahHouse@{X:8 Y:8}] dir=2 animFin=False
[Corazón Pelican] [V02] Entrada 1500 (Forest@{X:33 Y:25}) omitida mientras Leah está reclamada (reclamado@1300).
[Corazón Pelican] [V02-Reloj] Reloj 1300 -> 1510 | npc=Leah pos=LeahHouse@{X:8 Y:8} enRuta=False lastAttempted=1510 animFin=False
[Corazón Pelican] [V02-Volver] npc=Leah liberada a las 1510 -> retornando al tramo vigente 1500 (Forest@(33,25), anim=leah_draw, pasos=100, siguienteTramo=1830, sinTeletransporte=True)
[Corazón Pelican] [V02-Volver-Fin] npc=Leah reincorporada al tramo 1500 en Forest@{X:33 Y:25} dir=2 anim=leah_draw
[Corazón Pelican] [V02-Caminar] npc=Leah ticks=3172 completado=True recorrido=[LeahHouse@{X:8 Y:8} -> Forest@{X:104 Y:33} -> Forest@{X:33 Y:25}] dir=2 animFin=True
[Corazón Pelican] [V02-Horario] npc=Leah fecha=spring_2_Y1 (Tue) hora=1600 lluvia=False | ScheduleKey=spring follow=True ignoreToday=False lastAttempted=1600 control=(libre) | pos=Forest@{X:33 Y:25} dir=2 enRuta=True pasos=0 animFinRuta=True
```

- **Resultado:** a las `16:00` Leah está en `Forest@{X:33 Y:25}` con orientación `2` y animación `leah_draw` (`animFinRuta=True`), exactamente donde vanilla la tiene en ese tramo, sin ningún teletransporte (`sinTeletransporte=True`, cruzando la puerta `LeahHouse -> Forest@{X:104 Y:33}`).

### 4.2. Dos estaciones (`spring`, `fall`) y día de lluvia (`rain`) + eventos de fin de entrada

1. **Día de lluvia en primavera (`spring 3`, miércoles lluvioso → `ScheduleKey = rain`):**
   ```text
   [Corazón Pelican] [V02-Escenario] Configurado spring día 3 (Wed) lluvia=True hora=0600 -> npc=Leah ScheduleKey=rain entradas=5 pos=LeahHouse@{X:3 Y:7}
   [Corazón Pelican] [V02-HitoInicio] npc=Leah clave=rain hora=0900 -> destino=LeahHouse@(3,7) dir=3 anim=leah_sculpt pasos=1
   [Corazón Pelican] [V02-HitoFin] npc=Leah clave=rain hora=0900 destino=LeahHouse@(3,7) posReal=LeahHouse@{X:3 Y:7} dir=3 anim=leah_sculpt msg=-
   ```
2. **Otoño (`fall 9`, martes soleado → `ScheduleKey = fall`; `fall 12`, viernes soleado → `ScheduleKey = Fri`):**
   ```text
   [Corazón Pelican] [V02-HitoInicio] npc=Leah clave=fall hora=1200 -> destino=Town@(86,66) dir=3 anim=square_1_5_3 pasos=166
   [Corazón Pelican] [V02-HitoFin] npc=Leah clave=fall hora=1200 destino=Town@(86,66) posReal=Town@{X:86 Y:66} dir=3 anim=square_1_5_3 msg=-
   [Corazón Pelican] [V02-Caminar] npc=Leah ticks=5291 completado=True recorrido=[LeahHouse@{X:3 Y:7} -> Forest@{X:104 Y:33} -> Town@{X:0 Y:90} -> Town@{X:86 Y:66}] dir=3 animFin=False
   ```

### 4.3. Saltarse la entrada del Saloon (`1600`) cumpliendo la siguiente (`2340`)

En `fall 12` (`ScheduleKey = Fri`, entradas `0900`, `1200`, `1400`, `1600 Saloon@(2,20)`, `2340 LeahHouse@(2,4)`):

```text
[Corazón Pelican] [V02-Saltar] Eliminada entrada 1600 (Saloon@(2,20)) de Leah | se queda en LeahHouse@(9,5) | posterioresConservadas=[2340]
[Corazón Pelican] [V02-HitoInicio] npc=Leah clave=Fri hora=1400 -> destino=LeahHouse@(9,5) dir=0 anim=- pasos=4
[Corazón Pelican] [V02-HitoFin] npc=Leah clave=Fri hora=1400 destino=LeahHouse@(9,5) posReal=LeahHouse@{X:9 Y:5} dir=0 anim=- msg=-
[Corazón Pelican] [V02-Reloj] Reloj 1230 -> 1630 | npc=Leah pos=LeahHouse@{X:9 Y:5} enRuta=False lastAttempted=1630 animFin=False
[Corazón Pelican] [V02-HitoInicio] npc=Leah clave=Fri hora=2340 -> destino=LeahHouse@(2,4) dir=3 anim=leah_sleep pasos=9
[Corazón Pelican] [V02-HitoFin] npc=Leah clave=Fri hora=2340 destino=LeahHouse@(2,4) posReal=LeahHouse@{X:2 Y:4} dir=3 anim=leah_sleep msg=-
[Corazón Pelican] [V02-Caminar] npc=Leah ticks=259 completado=True recorrido=[LeahHouse@{X:9 Y:5} -> LeahHouse@{X:2 Y:4}] dir=3 animFin=True
```

- **Resultado:** a las `16:30` Leah permanece en `LeahHouse@{X:9 Y:5}` en lugar de ir al Saloon, y a las `23:40` la ruta recalculada (`9` pasos internos en lugar de los `132` desde el Saloon) la lleva a su cama `LeahHouse@{X:2 Y:4}` con `leah_sleep`.

### 4.4. Guardar y cambiar de día con Leah reclamada (`fall 9` → `fall 10`)

```text
[Corazón Pelican] [V02-Horario] npc=Leah fecha=fall_9_Y1 (Tue) hora=1630 lluvia=False | ScheduleKey=fall follow=True ignoreToday=True lastAttempted=1630 control=reclamado@1630 | pos=LeahHouse@{X:8 Y:8}
[Corazón Pelican] [V02] Guardando partida con Leah reclamada (CorazonPelican.Control=reclamado@1630) en LeahHouse@{X:3 Y:7}. Se liberará limpiamente al iniciar el nuevo día.
[Corazón Pelican] [V02] (DayStarted) Reclamación previa de Leah liberada (marca=reclamado@1630) -> ignoreScheduleToday=False, ScheduleKey=fall, entradas=5.
[Corazón Pelican] [V02-Horario] npc=Leah fecha=fall_10_Y1 (Wed) hora=1630 lluvia=False | ScheduleKey=fall follow=True ignoreToday=False lastAttempted=1630 control=(libre) | pos=Town@{X:86 Y:66} dir=3 enRuta=False pasos=0 animFinRuta=False
```

- **Resultado:** al guardar y pasar al día siguiente con Leah reclamada, `DayStarted` limpia `CorazonPelican.Control`, restablece `ignoreScheduleToday = false` y Leah sigue su horario normal del nuevo día sin quedar congelada.
