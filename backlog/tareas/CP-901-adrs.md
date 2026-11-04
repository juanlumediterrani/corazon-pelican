---
id: CP-901
titulo: Redactar ADR-0001 a ADR-0006
estado: pendiente
prioridad: P2
hito: D
depende_de: [CP-001]
tipo: planning
complejidad: baja
duracion_min: 90
sensibilidad: public
---

## Objetivo

Fijar como ADRs las seis decisiones que ya sostienen la ADS (§15).

## Contexto

Voyager y Pelican mantienen `docs/adr/`. Las ADRs 0002 y 0003 de Voyager se citan como filosofía compartida; conviene enlazarlas.

## Alcance

- `docs/adr/0001` motor en el host, mente fuera, el juego nunca espera.
- `0002` percepción con plantillas deterministas.
- `0003` actos validados antes de mostrar la frase.
- `0004` memoria única que viaja con la partida.
- `0005` contrato en JSON Schema con época de mundo (P-1, aceptada).
- `0006` la cadencia de pensamiento emerge de los disparadores y se mide; sin temporizador (P-2).

## Criterios de aceptación

- [ ] Seis ficheros con contexto, decisión, consecuencias y alternativas descartadas.
- [ ] Cada ADR enlaza las reglas R-xx y las tareas que la hacen cumplir.

## Fuera de alcance

- Nuevas decisiones (van a CP-902 o a su propia ADR).
