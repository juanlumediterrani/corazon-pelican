---
id: CP-503
titulo: DialogoModule · reflejo inmediato, respuesta pensada y caída a vanilla 🎮
estado: hecha
fecha: 2026-11-22
prioridad: P1
hito: F5
depende_de: [CP-010, CP-501, CP-304]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
vivo: si
---

## Objetivo

Hablar con Leah produce una reacción visible en < 300 ms y una frase pensada o, si caduca, su diálogo vanilla del día.

## Contexto

GSS §8.3, ADS §8.2, enganche de CP-010. Según [notas](../../docs/notas-juego.md) §7: en partida de un jugador `Game1.shouldTimePass()` devuelve `false` con cualquier `activeClickableMenu` abierto, así que la espera no consume tiempo de juego pero sí paciencia real; en multijugador el tiempo sigue. La espera debe leerse como «está pensando» (propuesta P-5, pendiente).

## Alcance

- Reflejo: emote + `DialogueBox` con línea breve del perfil.
- Pulso de interlocución con `caduca_en_ms`; reutilización del anticipado si existe.
- Caducidad → «…» y diálogo vanilla; jugador que se aleja → «lo que iba a decir» en memoria.
- Condiciones para interceptar (mente, presupuesto, ValleyTalk, eventos).

## Criterios de aceptación

- [x] En vivo: primera reacción visible < 300 ms medida en telemetría (criterio GSS §12) (`CorazonDev_454975123`: `dialogos.jsonl` registró `evento="reflejo"`, `reaccion_ms=5.327`, reflejo visible total en `31.63 ms`, apertura de `DialogueBox` con línea del perfil y actualización a `"Que alegría verte por aquí."`).
- [x] FakeWorld: con mente caída se muestra el diálogo vanilla del día y la amistad diaria sube igual (`DialogoIntercepcionTests.Con_mente_caida_o_apagada_se_muestra_el_dialogo_vanilla_y_no_se_intercepta` y `Si_el_pulso_caduca_cae_a_vanilla_con_emote_suspensivo_y_si_se_aleja_guarda_en_memoria`).
- [x] La mente nunca ve un pulso de interlocución durante un evento con guion (`DialogoIntercepcionTests.La_mente_nunca_ve_un_pulso_de_interlocucion_durante_un_evento_con_guion`).

## Fuera de alcance

- Opciones del jugador y texto libre (CP-504).
