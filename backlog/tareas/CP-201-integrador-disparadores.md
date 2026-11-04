---
id: CP-201
titulo: Integrador con fuga, refractario y disparadores de pulsión, incluidos los hitos de rutina
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-102, CP-004, CP-003]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
---

## Objetivo

El motor decide cuándo algo merece pensamiento: pico, interlocución, deriva, hito, fin de intención, compromiso y transición.

## Contexto

GSS §4.1–4.2 y §5.3 (decisión P-2). La cadencia de pensamiento no se programa: emerge de estos disparadores y se mide en CP-208. **Los hitos los dan las rutinas, literalmente**: el horario que el juego elige cada día para el personaje (cambia con estación, día, clima, festivales, corazones, matrimonio y avance de la partida). Cada entrada de hoy es un plan finito («abrir la tienda», «ir al Saloon»), igual que un plan que pidió la mente («ir a la pasarela y sentarse»). El perfil no los lista: los anota por patrón para decir qué hechos acompañan al pulso y si se omiten cuando ya pensó por el camino. `despertar` y `acostarse` son hitos comunes que abren cadenas (CP-209). Refractario en tiempo real; presupuesto en CP-202.

## Alcance

- `Integrador.cs`, `Refractario.cs`, `Disparadores.cs` en Core.
- Prioridades entre disparadores; la interlocución siempre gana.
- Hitos de intención a partir del evento «plan terminado» del cuerpo (CP-302) y de rutina en el momento de disponerse con la siguiente entrada del horario de hoy (una entrada dura de su hora a la de la siguiente; CP-011), enriquecidos con la anotación del perfil que coincida.
- Cada pulso de hito lleva la agenda pendiente del día (pensamiento en cascada, GSS §4.1).
- Regla `omitir_si_ya_penso` por hito.
- Despertar al empezar el día; acostarse al irse a dormir o en el fin de día, lo que llegue antes.
- Transiciones: cambio de luz y de clima.

## Criterios de aceptación

- [ ] En FakeWorld, un festival simulado (50 entidades moviéndose) no produce más de una pulsión por periodo refractario salvo interlocución.
- [ ] Muchas saliencias menores sostenidas acaban en una pulsión de deriva; una sola menor, no.
- [ ] Un plan de la mente `ir_a` + `sentarse` produce un único pulso de hito al sentarse; si a mitad de camino salta una deriva y el hito tiene `omitir_si_ya_penso`, no se emite.
- [ ] Un hito de rutina sin `omitir_si_ya_penso` se emite aunque haya habido otra pulsión durante el plan, y lleva los hechos de su `contexto`.
- [ ] El pulso de un hito de rutina sale antes de que el cuerpo empiece la entrada siguiente, de modo que la respuesta todavía puede cambiarla.
- [ ] Cada día simulado produce exactamente un despertar y un acostarse, también si el día termina antes de que Leah se acueste.
- [ ] Core no contiene ningún nombre de hito concreto salvo `despertar` y `acostarse`.
- [ ] FakeWorld con dos horarios distintos (otoño soleado y lluvia) para el mismo perfil: los hitos emitidos siguen al horario de cada día sin cambiar el perfil.

## Fuera de alcance

- Pensamiento anticipado (CP-507). Contenido de las cadenas (CP-209). Cambios de rutina (CP-306).
