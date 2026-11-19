---
id: CP-210
titulo: Conservar el humor nacido de lo vivido y observar su efecto en la conducta
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-107, CP-206, CP-209]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
---

## Objetivo

Una experiencia que el personaje interpreta a la luz de su historia puede influir durante varios días en una elección pequeña. P-9 fija quién interpreta la experiencia y quién custodia y limita su efecto.

## Alcance

- Partir de episodios identificables del diario y recuerdos disponibles en la pulsión. La mente puede explicar cómo le sentaron, conservar su humor previo o revisarlo. Reutilizar los pensamientos y reflexiones del día.
- Ampliar el contrato compartido con la impresión y sus límites. Reutilizar los episodios identificados y la comprobación de origen de CP-211. CP-206 conserva sus referencias al guardar un recuerdo. Mantener la lectura de perfiles y partidas anteriores.
- El host comprueba origen, época y límites del perfil, persiste la impresión con el personaje y la devuelve en pulsos posteriores. Concretar en el perfil su duración e intensidad y documentar los valores ensayados. En los días tranquilos que siguen, el efecto se atenúa poco a poco con el tiempo vivido; una experiencia significativa puede cambiarlo antes (aunque el episodio siga recordándose).
- La mente puede elegir una atención, una pausa o una pequeña variación permitida de la rutina. Toda acción efectiva sigue el camino normal de intención y validación. Los detalles de Leah pertenecen a su perfil y a sus escenas.

## Criterios de aceptación

- [ ] Reproducir la [prueba de dos días de CP-905](CP-905-revision-argon.md#prueba-de-dos-días) con la configuración de componentes del mod y la entrada normal de la mente. Seguir el id del episodio hasta el prompt de un hito ordinario después de recargar.

- [ ] `HumorPersonajeTests` y pruebas de contrato. Una impresión vinculada a un episodio que el personaje vivió llega al despertar siguiente tras guardar y cargar. Un origen desconocido o una respuesta de otra época se rechaza. Reintentar la respuesta conserva un solo efecto.
- [ ] Con reloj virtual, comprobar la atenuación gradual durante varios días tranquilos, su sustitución anticipada ante una experiencia significativa, el límite elegido y el comportamiento de una partida anterior sin impresión. Cambiar de día o recargar conserva la evolución que corresponde al tiempo de juego.
- [ ] Con FakeWorld y mente guionizada, repetir la misma tarde con el mismo perfil, horario, lugar y oportunidades. Variar solo la experiencia anterior. Comprobar que el contexto llega a la mente y que las dos elecciones propuestas por la mente guionizada solo usan las opciones del pulso.

## Verificación en fases posteriores

En CP-208, revisar con mente real si la interpretación y la elección encajan con la voz de Leah. En CP-305, cotejar la acción ejecutada con lo que pudo ver un observador. Una mera frase sobre su ánimo deja pendiente la comprobación corporal.

## Fuera de alcance

- Gobernar a un segundo NPC en vivo, añadir un reloj de consultas o fijar un ánimo universal para cada suceso.
