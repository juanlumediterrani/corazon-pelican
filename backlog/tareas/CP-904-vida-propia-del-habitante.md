---
id: CP-904
titulo: Revisar el diseño y las aceptaciones desde la vida propia del habitante
estado: hecha
prioridad: P1
hito: D
depende_de: []
tipo: planning
complejidad: media
duracion_min: 60
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

Hacer observable la premisa del proyecto. Una experiencia del valle puede cambiar cómo se siente un personaje y qué hace los días siguientes, incluso cuando nadie le pide nada.

## Hallazgos de la revisión

| Evidencia actual | Qué faltaba ver | Dónde se comprobará |
|---|---|---|
| CP-103 y CP-201 ya prueban bandas y disparadores. La simulación de CP-201 encontró que incluso una escena tranquila podía disparar deriva al terminar cada refractario. | Que algo de esa tarde motive una elección comprensible para ese habitante. Contar más pensamientos no permite juzgarlo. | CP-205 prepara escenas abiertas. CP-208 contrasta el motivo del pulso con la elección de la mente real. |
| La huella de P-8 se reinicia al pensar. El contrato actual expresa los episodios del pulso y los recuerdos propuestos como cadenas. | Que la impresión de ayer conserve su procedencia y pueda influir en la conducta de mañana tras una recarga. | CP-206 lleva las referencias al contrato. CP-210 prueba con días simulados el humor definido en P-9. |
| CP-305 ya exigía que Leah pudiera cambiar un hito. CP-406 ya pedía una obra nacida de la escena. | Qué señal podría notar quien se cruza con Leah varios días, incluso si mantiene el destino que marca su horario. | En CP-305, quien observa describe primero lo que hizo Leah. Después se contrasta con la intención validada y su historia. |

## Criterios de aceptación

- [x] La [GSS](../../design.md) ilustra una decisión espontánea y define cómo reconocer un cambio entre días. La [ADS](../../architecture.md) distingue las responsabilidades de la mente y el motor.
- [x] P-9 registra el criterio del autor y [CP-210](CP-210-humor-y-conducta.md) especifica contrato, persistencia, límites y pruebas pendientes.
- [x] La ruta desde las escenas de carácter hasta la observación en el juego figura en el [índice](../README.md). CP-508 conserva el caso de la promesa como aceptación del diálogo.

Verificación del 2026-11-19 con los comandos de AGENTS.md y despliegue desactivado. Compilación con 0 advertencias y 0 errores. xUnit, 135 superados y 0 omitidos. pytest, 40 superados.
