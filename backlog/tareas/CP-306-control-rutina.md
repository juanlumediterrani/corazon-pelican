---
id: CP-306
titulo: El personaje controla su rutina · agenda del día, reequilibrio en cascada y cambios de hitos
estado: hecha
fecha: 2026-11-22
prioridad: P2
hito: F3
depende_de: [CP-011, CP-212, CP-301, CP-303]
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

- Conectar `AgendaDia` y `ValidadorCambiosRutina` de Core (`CP-212`, R-16) al adaptador del juego: fijar la agenda en el despertar (`CP-209`), reequilibrarla en cada pulso de hito con `cambios_rutina` y ejecutar saltar/mover/sustituir mediante el mecanismo de `CP-011`.
- Registro en memoria («hoy no fui al Saloon») y en `eventos`, más degradación a horario vanilla si la mente cae (`CP-303`).

## Criterios de aceptación

- [x] `CambioRutinaTests`: un cambio sobre un hito sin `cambios`, de un tipo no permitido o fuera de `limites` se rechaza con motivo (R-16) (`CambioRutinaTests`).
- [x] FakeWorld: saltar un hito no altera los siguientes; al día siguiente la rutina es la canónica (`ControlRutinaCascadaTests.Saltar_un_hito_no_altera_los_siguientes_y_al_dia_siguiente_la_rutina_es_la_canonica`).
- [x] FakeWorld: un día completo en cascada (despertar → hito 1 → hito 2 → … → acostarse) en el que la agenda se reequilibra en un hito tras un suceso (p. ej. empieza a llover) y el cuerpo sigue la agenda nueva (`ControlRutinaCascadaTests.Dia_completo_en_cascada_reequilibra_la_agenda_tras_lluvia_y_el_cuerpo_sigue_la_nueva`).
- [x] Con un cambio de rutina activo y la mente caída, Leah vuelve a su horario (degradación de CP-303) (`ControlRutinaCascadaTests.Con_cambio_de_rutina_activo_y_mente_caida_restaura_horario_canonico_y_degrada`).
- [x] El esquema y el validador rechazan cambios con coordenadas o sobre hitos que no están en el horario de hoy (`CambioRutinaTests`, `ValidadorCambiosRutina`, `schemas/respuesta.schema.json`).

## Fuera de alcance

- Cambios con efecto mecánico (tiendas, servicios) y cómo se avisa a los jugadores.
