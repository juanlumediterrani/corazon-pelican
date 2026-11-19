---
id: CP-901
titulo: Redactar ADR-0001 a ADR-0006
estado: hecha
fecha: 2026-11-19
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

- [x] Seis ficheros con contexto, decisión, consecuencias y alternativas descartadas.
- [x] Cada ADR enlaza la tabla de reglas de la GSS y sus tareas. El apartado de verificación de cada fichero permite seguir desde la decisión hasta las pruebas existentes o previstas.

La [ADS §15](../../architecture.md#15-decisiones-de-arquitectura) enlaza los seis ficheros. Antecedentes en el proyecto hermano Stardew Voyager ([el juego como fuente de verdad](../../../stardew-voyager/docs/adr/0002-el-juego-es-el-oraculo.md) y [responsabilidades del arnés](../../../stardew-voyager/docs/adr/0003-skill-dirige-arnes-ejecuta.md)).

Verificación del 2026-11-19 con los comandos de AGENTS.md y despliegue desactivado. Compilación con 0 advertencias y 0 errores. xUnit, 135 superados y 0 omitidos. pytest, 40 superados.

## Fuera de alcance

- Nuevas decisiones (van a CP-902 o a su propia ADR).
