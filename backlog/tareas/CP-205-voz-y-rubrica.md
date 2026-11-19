---
id: CP-205
titulo: Voz de Leah y rúbrica de coherencia de carácter
estado: hecha
fecha: 2026-11-19
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
- 12 escenas fijas, con tres negativas y al menos seis momentos en que nadie le pide nada. Incluir una mañana en casa, un cambio de luz mientras trabaja y la decisión de salir tras una tarde a solas.
- Emparejar dos de esas escenas: misma tarde de lluvia y mismas opciones, con recuerdos previos distintos. Describir qué pequeño hábito podría variar los días siguientes y qué vería alguien que pasara por allí.
- En las escenas abiertas, describir también una segunda elección plausible. Puntuar qué detalle del momento y del carácter la sostiene. Que Leah dibuje en todas las escenas empobrecería la muestra.

## Criterios de aceptación

- [x] `leah.voz.md` y `docs/rubrica-caracter.md` existen y están enlazados desde el perfil. Se abrieron las rutas de `voz` y `rubrica_caracter` de `leah.json` tanto en la salida de compilación como en el ZIP local del mod. La rúbrica se empaqueta desde su único original en `docs/`.
- [x] Las 12 escenas de prueba están en `mente/tests/escenas_caracter/` con la conducta esperable resumida y, cuando quepan varias elecciones, el motivo que haría creíble cada una. Recuento de `leah.json`: 12 ids distintos, 8 escenas sin interlocución y 3 negativas. `test_la_pareja_de_lluvia_solo_varia_el_recuerdo_que_recibe_la_mente` comprueba el par de P-9.
- [x] La rúbrica tiene ejemplos de puntuación 1 y 4 para cada eje. La tabla de `docs/rubrica-caracter.md` contiene seis ejes y da pautas para puntuar 2 y 3.

## Verificación

- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false`. 0 advertencias, 0 errores.
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false`. 165 superados, 0 omitidos.
- `python3 -m pytest -q`. 44 superados. La valoración de respuestas de una mente real corresponde a CP-208.
- Se contrastaron las referencias biográficas y de oficio con los datos locales de diálogo y los eventos 50 y 51 de la cabaña. Las fichas usan redacción propia.

## Fuera de alcance

- Evaluación automática con LLM juez (se puede proponer después).
