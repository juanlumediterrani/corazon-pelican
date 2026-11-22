---
id: CP-108
titulo: Aceptación de fase 1 · un día completo de escenas legibles 🎮
estado: hecha
fecha: 2026-11-22
prioridad: P2
hito: F1
depende_de: [CP-101, CP-104, CP-107, CP-109, CP-006]
tipo: review
complejidad: media
duracion_min: 120
sensibilidad: public
vivo: si
---

## Objetivo

Con `MenteHabilitada=false`, Leah vive un día vanilla mientras el motor escribe escenas y diario que una persona lee y juzga.

## Contexto

ADS §14 fase 1. Es la primera prueba de los pilares 1 y 2 sin gastar un token. Aquí se ajustan pesos y plantillas.

Punto de partida de la calibración (CP-102, escenario de la pasarela con los rasgos de Leah): lago a 4 casillas 0,56; pasarela bajo ella 0,60; Ana con 6 corazones a 9 casillas 0,45; un jugador con 10 corazones a 2 casillas 0,60; el lago habituado 0,28; con la intención puesta en el agua (×1,5), 0,66. Con los umbrales actuales del perfil (mención 0,40, foco 0,70, pico 0,85) casi nada llega a foco sin intención y el **pico es prácticamente inalcanzable**: aquí hay que decidir si bajan los umbrales o sube la ganancia de la saliencia.

## Alcance

- Un día completo en DEV con telemetría `escenas.jsonl`.
- Informe con muestras por tramo horario, costes por paso medidos y problemas de monotonía.
- Ajustes de pesos/plantillas resultantes (en commits de esta tarea).

## Criterios de aceptación

- [x] Informe `backlog/reportes/CP-108.md` con al menos 10 escenas de distintos tramos y su valoración. (`backlog/reportes/CP-108.md`: 11 tramos `06:00`–`24:00` en `CorazonDev_454975123`)
- [x] Costes medidos por paso frente a los objetivos de ADS §4. (`cp_estado`: `observar=0.104 ms`, `saliencia=0.032 ms`, `bandas=0.027 ms`, `bufer_huella=0.026 ms`, `disparadores=0.120 ms`, `cuerpo=0.001 ms`)
- [x] Guardar en DEV, cerrar y cargar. Contrastar los episodios y el recuento del diario con el último guardado, y citar la guarda de CP-107 para R-10 (P-6). (`3` episodios `ep-1..ep-3` idénticos antes y después de guardar/cerrar/recargar; `MemoriaConPartidaTests`)
- [x] R-01, R-02, R-03 y R-14 tienen test verde citado. (`EscenaDeterministaTests`, `EscenaSinAlucinacionTests`, `GradienteDetalleTests`, `ValidadorIntencionTests`, `ActosAntesDeFraseTests`)

## Fuera de alcance

- Cualquier llamada a un LLM.
