---
id: CP-101
titulo: ObservadorJuego · del estado del juego a una Observacion de solo lectura
estado: hecha
fecha: 2026-11-22
prioridad: P1
hito: F1
depende_de: [CP-003, CP-005]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
---

## Objetivo

Cada `SensorioCadenciaTicks`, el adaptador produce una `Observacion` de la ubicación de Leah con identidades estables y sin mutar nada.

## Contexto

GSS §3.1 y ADS §4 (pasos 3 y 4). CP-004 ya creó `Core/Sensorio/Observacion.cs` (`Observacion`, `EntidadObservada` con `Ref`, categoría, casilla, etiquetas de rasgo y corazones) y lo rellena el FakeWorld; esta tarea lo rellena desde el juego y lo amplía si hace falta (sucesos, línea de visión). Las identidades (`farmer:<id>`, `npc:<nombre>`, `obj:<ubicación>:<x>,<y>:<id>`…) son las que luego usan léxico, actos y memoria.

## Alcance

- `Observacion` (Core) y `ObservadorJuego` (mod): granjeros, NPCs, animales y mascotas, objetos y muebles, agua/terreno relevante, clima, luz, hora y sucesos locales (regalo recibido).
- Línea de visión simplificada y distancia en casillas.
- Contador de coste por paso del bucle (ADS §4) visible con `cp_estado`.

## Criterios de aceptación

- [x] Ninguna llamada del observador modifica el estado (revisión + test que compara un FakeWorld antes/después). — Evidencia: revisión de solo lectura en `CorazonPelican/Adaptador/ObservadorJuego.cs` (sin escrituras ni llamadas mutadoras sobre `NPC`, `GameLocation`, `Farmer`, mapas ni `Objects`) y test `ObservadorTests.Observar_no_muta_ningun_campo_ni_entidad_del_mundo` (`dotnet test`: 215/215 superados).
- [x] Identidades estables: la misma entidad tiene la misma `ref` en dos observaciones consecutivas y tras guardar/cargar. — Evidencia: `CorazonPelican.Core/Sensorio/IdentidadesObservacion.cs` + `ObservadorTests.Identidades_estables_entre_observaciones_consecutivas_y_tras_guardar_y_cargar` (`dotnet test`: 215/215 superados).
- [x] En vivo (partida DEV): coste medio del paso «observar» < 1 ms en la pasarela del lago con el jugador cerca. — Evidencia en vivo en `CorazonDev_454975123` (`cp_estado` en `Forest (34, 25)`): `Coste [observar]: medio=0.064 ms, último=0.008 ms, máx=4.437 ms (82 pasos)`.

## Notas

- Implementación, pruebas unitarias y compilación listas (`dotnet build` 0 advertencias / 0 errores; `dotnet test`: 215/215 superados; `python3 -m pytest -q`: 44 superados).
- `ModEntry` ejecuta `ObservadorJuego.Observar` cada `SensorioCadenciaTicks` registrando tiempos en `ModContext.CostesBucle` (`"observar"`), y `ConsolaModule` los imprime en `cp_estado`.
- Solo resta medir el tercer criterio en una sesión en vivo 🎮 en la partida DEV (pasarela del lago con el jugador cerca) y comprobar `< 1 ms` con `cp_estado`.

## Fuera de alcance

- Sucesos de Pelican (CP-602).
