---
id: CP-304
titulo: Reflejos del perfil en menos de 100 ms
estado: hecha
fecha: 2026-11-22
prioridad: P2
hito: F3
depende_de: [CP-302]
tipo: code-light
complejidad: baja
duracion_min: 90
sensibilidad: public
---

## Objetivo

Reglas del perfil que producen gestos inmediatos sin la mente: levantar la vista, saludar, cerrar el cuaderno si alguien mira con pocos corazones.

## Contexto

GSS §2.1 y §6.4 (apertura según relación).

## Alcance

- Reglas declarativas en el perfil: condición sobre bandas/relación/estado → acción breve.
- Enfriamiento por regla para no repetir gestos.

## Criterios de aceptación

- [x] FakeWorld: el reflejo se aplica en el mismo paso de observación en que se cumple la condición (`ReflejosTests`: un único paso de `250 ms` en `BucleAtencion` ejecuta el reflejo en `< 100 ms` y actualiza el estado del cuerpo en ese mismo paso).
- [x] Con 0–2 corazones y el jugador en foco mientras dibuja, Leah guarda el cuaderno (regla de apertura) (`ReflejosTests.Con_0_a_2_corazones_y_jugador_en_foco_mientras_dibuja_guarda_el_cuaderno_en_el_mismo_paso(0, 1, 2)` y `Con_3_o_mas_corazones_en_foco_mientras_dibuja_no_oculta_el_cuaderno_y_levanta_la_vista`).
- [x] Ningún reflejo consume presupuesto de pulsos (`ReflejosTests.Enfriamiento_por_regla_evita_repetir_el_gesto_y_ningun_reflejo_consume_presupuesto`: `OrdinariosLibresGastados == 0`).

## Fuera de alcance

- Modelo local de reflejos con etiquetas cerradas (opcional, sin tarea).
