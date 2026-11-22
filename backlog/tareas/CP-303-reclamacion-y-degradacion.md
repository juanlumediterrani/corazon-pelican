---
id: CP-303
titulo: Reclamación, volver_a_rutina y degradación a la Leah vanilla
estado: hecha
fecha: 2026-11-22
prioridad: P1
hito: F3
depende_de: [CP-011, CP-302]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-11]
---

## Objetivo

Pase lo que pase con la mente, Leah vuelve a su rutina canónica y nunca queda congelada, muda o rota (pilar 5).

## Contexto

GSS §5.3 y §11. Mecanismo validado en CP-011.

## Alcance

- `ReclamacionNpc.cs` con `modData["CorazonPelican.Control"]`.
- `volver_a_rutina` al tramo vigente.
- Degradación automática: mente caída, sin presupuesto, `cp_mente off`, error en un módulo.

## Criterios de aceptación

- [x] `DegradacionVanillaTests`: con la mente apagada, FakeWorld no registra ninguna acción del motor sobre el NPC (R-11) (`DegradacionVanillaTests.Con_la_mente_apagada_FakeWorld_no_registra_ninguna_accion_del_motor_sobre_el_npc`).
- [x] Matar el proceso de la mente con Leah reclamada la devuelve a la rutina en ≤ 1 tramo de 10 min de juego (`DegradacionVanillaTests.Matar_el_proceso_de_la_mente_con_npc_reclamado_lo_devuelve_a_la_rutina_en_un_tramo_de_10_min_o_menos` y `Error_en_modulo_o_presupuesto_agotado_devuelve_al_personaje_reclamado_a_su_rutina`).
- [x] En vivo: `cp_mente off` a mitad de una intención devuelve a Leah a su horario (`DegradacionVanillaTests.Cp_mente_off_a_mitad_de_una_intencion_cancela_el_plan_y_devuelve_al_horario` y prueba en vivo en `CorazonDev_454975123`: a mitad de `int-mitad` en `Forest@(92,28)`, `cp_mente off` emitió `[ReclamacionNpc] Leah devuelta a rutina (volver_a_rutina, previo=ir_a@1300): Forest@{X:92 Y:28} -> tramo 1200 Forest@{X:68 Y:50} (pasos=47)` y `[Habitante:leah] Degradación automática a rutina canónica (motivo=mente_deshabilitada)`).

## Fuera de alcance

- Integración con el *micro-wandering* de Pelican (CP-601).
