---
id: CP-101
titulo: ObservadorJuego · del estado del juego a una Observacion de solo lectura
estado: pendiente
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

- [ ] Ninguna llamada del observador modifica el estado (revisión + test que compara un FakeWorld antes/después).
- [ ] Identidades estables: la misma entidad tiene la misma `ref` en dos observaciones consecutivas y tras guardar/cargar.
- [ ] En vivo (partida DEV): coste medio del paso «observar» < 1 ms en la pasarela del lago con el jugador cerca.

## Fuera de alcance

- Sucesos de Pelican (CP-602).
