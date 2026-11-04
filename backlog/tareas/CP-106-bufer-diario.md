---
id: CP-106
titulo: Búfer de prosa de acción, huella de atención y diario con consolidación temporal
estado: pendiente
prioridad: P2
hito: F1
depende_de: [CP-104]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

El búfer guarda lo que llegó a la banda de mención del personaje y se consolida con más detalle para lo reciente; la huella resume a qué ha atendido desde el último pensamiento; el diario recibe los episodios.

## Contexto

GSS §4.4 y §9.2. Sin LLM resumidor: la consolidación es la fóvea/periferia aplicada al tiempo. Búfer y huella son propios de cada personaje: salen de su saliencia (CP-102), sus bandas (CP-103) y sus plantillas (CP-104). La huella hace que lo vivido entre hitos, aunque no dispare nada, llegue al siguiente pensamiento y tiña su perspectiva.

## Alcance

- `Bufer.cs`: inserción (solo desde la banda de mención), consolidación, tamaño máximo.
- `Huella.cs`: atención acumulada por entidad (tiempo en foco o mención × saliencia, con fuga), reiniciada en cada pulso; minutos de juego desde el último pensamiento; frase con las plantillas del perfil.
- Diario de episodios con fecha de juego, saliencia y entidades.
- Envejecimiento y poda con tope (`Consolidacion.cs`).

## Criterios de aceptación

- [ ] El búfer consolidado nunca supera N entradas configurables y conserva siempre las de mayor saliencia de la última hora de juego.
- [ ] Ningún texto del búfer consolidado no aparecía literalmente en el búfer original (no se inventa).
- [ ] El diario respeta `TamanoMaxMemoriaKB` con un test de un año simulado.
- [ ] Huella: tras una tarde simulada mirando el lago con un paso breve del jugador, el lago encabeza la huella; con la misma tarde y los rasgos de otro perfil (p. ej. `persona` alto), el orden cambia.
- [ ] La huella solo nombra entidades que estuvieron en la observación y se reinicia al emitir un pulso.

## Fuera de alcance

- Reflexión diario → recuerdos (CP-206).
