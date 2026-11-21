---
id: CP-405
titulo: cp_preparar_desinstalacion · exportar obras y sustituirlas por cuadros vanilla
estado: dormida
prioridad: P3
hito: F4
depende_de: [CP-403]
tipo: code-light
complejidad: baja
duracion_min: 90
sensibilidad: public
---

## Objetivo

Quien desinstale el mod conserva las obras como PNG y no deja objetos de error en la partida.

## Contexto

GSS §11, ADS §9. El comportamiento real sin el mod se observa en CP-015.

## Alcance

- Exportar PNG + metadatos a una carpeta.
- Sustituir obras colocadas y en inventarios/cofres por un cuadro vanilla del mismo tamaño.

## Criterios de aceptación

- [ ] Tras el comando y sin el mod, cargar la partida no muestra objetos de error.
- [ ] La carpeta exportada contiene una PNG por obra terminada con su título.
- [ ] El comando pide confirmación y es idempotente.

## Fuera de alcance

- Reimportar obras.

## Pospuesta a Fase 2 (2026-11-21)

Según el acuerdo [`P-13`](../../docs/decisiones.md), la exportación/sustitución de obras colocables pasa a Fase 2 junto con `CP-403` tras las obras en texto de **Elliott** (`CP-410`).
