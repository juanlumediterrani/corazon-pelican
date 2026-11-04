---
id: CP-205
titulo: Voz de Leah y rúbrica de coherencia de carácter
estado: pendiente
prioridad: P1
hito: F2
depende_de: []
tipo: creative
complejidad: media
duracion_min: 180
sensibilidad: public
---

## Objetivo

Un `leah.voz.md` que define quién es Leah para la mente y una rúbrica con la que una persona puntúa muestras de pensamiento y diálogo.

## Contexto

GSS §12 remite la rúbrica de coherencia «al backlog»: es esta tarea. Riesgo principal del diseño: la *Leah servicial* (GSS §14). La voz debe apoyarse en el canon (diálogos, eventos de corazón, carta de los 14 corazones…) sin copiar texto con derechos más allá de lo necesario.

## Alcance

- `assets/personajes/leah.voz.md`: biografía breve, valores, manías, cómo habla, de qué no habla, cuándo se niega, cómo cambia con los corazones.
- Reglas comunes (no mencionar lo que no está en escena o memoria; puede negarse; no es asistente).
- Rúbrica (1–4 por eje): fidelidad al canon, no servilismo, anclaje en la escena, coherencia con la memoria, economía (dice poco), oficio (mira como artista).
- 12 escenas de prueba fijas para evaluarla (incluidas tres en las que lo correcto es negarse).

## Criterios de aceptación

- [ ] `leah.voz.md` y `docs/rubrica-caracter.md` existen y están enlazados desde el perfil.
- [ ] Las 12 escenas de prueba están en `mente/tests/escenas_caracter/` con la respuesta ideal resumida.
- [ ] La rúbrica tiene ejemplos de puntuación 1 y 4 para cada eje.

## Fuera de alcance

- Evaluación automática con LLM juez (se puede proponer después).
