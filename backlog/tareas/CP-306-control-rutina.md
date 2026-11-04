---
id: CP-306
titulo: El personaje controla su rutina · agenda del día, reequilibrio en cascada y cambios de hitos
estado: pendiente
prioridad: P2
hito: F3
depende_de: [CP-011, CP-301, CP-303]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-16]
---

## Objetivo

La mente decide al despertar la agenda del día, la reequilibra en cada hito y puede cambiar hitos próximos dentro de lo que el perfil permite; el cuerpo lo cumple sin romper el resto del día.

## Contexto

GSS §5.3: el personaje es dueño de su rutina. Leah: «hoy no voy al Saloon, quiero acabar el lago». Pierre (futuro): «hoy cierro para estar con la familia». En v1 solo cambios **sin efecto mecánico**; los que alteran el juego para los jugadores (cerrar una tienda) son pregunta abierta (GSS §14, CP-902).

## Alcance

- `Rutina.cs` en Core: validar `cambios_rutina` de la intención contra las entradas del horario de hoy y las anotaciones del perfil (`cambios`, `limites`, condiciones como «nunca en festival»).
- Agenda del día en el motor: la fija la respuesta al `despertar` (CP-209), viaja en cada pulso de hito y se reequilibra con los `cambios_rutina` de cada respuesta (GSS §4.1, pensamiento en cascada).
- Aplicación con el mecanismo de CP-011: saltar una entrada, moverla de hora o sustituirla por el plan de la intención.
- Efecto limitado a la intención y al día; al día siguiente, la rutina canónica.
- Registro en memoria («hoy no fui al Saloon») y en `eventos`.

## Criterios de aceptación

- [ ] `CambioRutinaTests`: un cambio sobre un hito sin `cambios`, de un tipo no permitido o fuera de `limites` se rechaza con motivo (R-16).
- [ ] FakeWorld: saltar un hito no altera los siguientes; al día siguiente la rutina es la canónica.
- [ ] FakeWorld: un día completo en cascada (despertar → hito 1 → hito 2 → … → acostarse) en el que la agenda se reequilibra en un hito tras un suceso (p. ej. empieza a llover) y el cuerpo sigue la agenda nueva.
- [ ] Con un cambio de rutina activo y la mente caída, Leah vuelve a su horario (degradación de CP-303).
- [ ] El esquema y el validador rechazan cambios con coordenadas o sobre hitos que no están en el horario de hoy.

## Fuera de alcance

- Cambios con efecto mecánico (tiendas, servicios) y cómo se avisa a los jugadores.
