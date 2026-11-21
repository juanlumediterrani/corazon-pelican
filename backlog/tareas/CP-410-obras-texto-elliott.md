---
id: CP-410
titulo: Segundo personaje y obras en texto · Elliott como primera entrega de Fase 2
estado: dormida
prioridad: P2
hito: F4
depende_de: [CP-508]
tipo: code-light
complejidad: media
duracion_min: 180
sensibilidad: public
reglas: [R-10]
---

## Objetivo

Inaugurar la **Fase 2** (`P-13`) con un segundo personaje (**Elliott**) cuya obra sea **texto** (un poema, carta o apunte literario), validando que el concepto de «obra» en *Corazón Pelican* es independiente de sprites e imágenes antes de construir el taller visual de Leah (`CP-013`, `CP-015`, `CP-401`–`CP-406`).

## Contexto

Decisión [`P-13`](../../docs/decisiones.md) (`2026-11-21`, `GSS §14.3`): la generación de obras pasa a Fase 2 y comienza por Elliott en texto. Permite probar con un personaje distinto en `assets/personajes/` que el motor permanece agnóstico respecto al personaje (`ArquitecturaTests`) y que el portafolio, la decisión, la autocrítica y los actos (`regala`) funcionan también cuando la obra no es una PNG.

## Alcance

- Añadir el perfil `elliott.json`, su voz y sus plantillas de prosa como datos en `CorazonPelican/assets/personajes/`, sin tocar `CorazonPelican.Core/`.
- Soportar formatos de obra en texto en el esquema del portafolio (`poema` / `manuscrito` / `apunte`) junto con generación/autocrítica y persistencia (`R-10`).
- Permitir enseñar, regalar (`R-08`) o vender a precio fijo por formato (`P-13`) obras de texto dentro del juego sin generar muebles PNG todavía.

## Criterios de aceptación

- [ ] `PerfilValidatorTests` y `test_contrato.py`: `elliott.json` pasa la validación de esquema y reglas del motor sin añadir código específico en `CorazonPelican.Core/`.
- [ ] `ObraTextoTests` (pytest / FakeWorld): Elliott decide una obra desde su escena, pasa la autocrítica (máx. 2 intentos), entra en el portafolio persistido (`R-10`) y puede entregarse o regalarse (`R-08`).
- [ ] Aceptación: en partida DEV o simulación con Elliott gobernado, genera una obra de texto legible coherente con su escena y la conserva al guardar y cargar.

## Pospuesta a Fase 2 (2026-11-21)

Pendiente del cierre de v1 (`CP-508`). Será la primera tarea en reactivarse al comenzar la Fase 2, antes del taller visual (`CP-013`, `CP-401`–`CP-406`).

## Fuera de alcance

- Generación de imágenes PNG, paletas o muebles colocables (paso siguiente de Fase 2 con Leah).
