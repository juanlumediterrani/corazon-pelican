---
id: CP-302
titulo: CuerpoNpc · vocabulario de acciones v1 sobre Leah 🎮
estado: hecha
fecha: 2026-11-22
prioridad: P1
hito: F3
depende_de: [CP-011, CP-012, CP-301]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
vivo: si
---

## Objetivo

Leah ejecuta `ir_a`, `sentarse`, `mirar`, `levantar_la_vista`, `dibujar`, `guardar_cuaderno`, `gesto`, `murmurar` y `esperar` con medios nativos.

## Contexto

GSS §5.2. La mente decide qué; el cuerpo decide cómo. Mecanismos de CP-011 (rutas, reclamación) y CP-012 ([`docs/spikes/v07-animaciones.md`](../../docs/spikes/v07-animaciones.md)). Nativos disponibles: `doEmote`, `showTextAboveHead`, `PathFindController`, `StartActivityRouteEndBehavior` / `EndActivityRouteEndBehavior` (esperar `!doingEndOfRouteAnimation.Value` antes de mover para no dejar `freezeMotion = true`; en `sentarse`, sin frame de sentado en Leah, v1 usa `ir_a` + `Halt()` + orientación del lugar).

## Alcance

- Una clase por acción con inicio, avance por tick, fin y causa de bloqueo.
- Prosa de acción hacia el búfer en cada acción.
- `mirar`/`levantar_la_vista` cambian el foco del sensorio (GSS §3.6).
- Evento «plan terminado» al acabar la última acción con duración del plan, que alimenta el disparador de hito (CP-201).

## Criterios de aceptación

- [x] FakeWorld: cada acción termina en cumplida o bloqueada con motivo; ninguna queda colgada más de su duración máxima (`CuerpoVocabularioTests`: `Cada_accion_del_vocabulario_v1_termina_en_cumplida_en_condiciones_validas`, `Cada_accion_invalida_o_imposible_termina_bloqueada_con_motivo`, `Ninguna_accion_queda_colgada_mas_de_su_duracion_maxima`, `Mirar_y_levantar_la_vista_cambian_el_foco_del_sensorio`, `Ejecutor_conectado_a_MotorHabitante_registra_prosa_en_bufer_y_dispara_hito_al_terminar_plan`).
- [x] En vivo: `ir_a(orilla_norte)` desde la casa de Leah llega por rutas nativas sin teletransporte visible (`[Cuerpo:leah] Plan 'int-orilla' iniciado desde LeahHouse@(3,7)` -> `recorrido=[LeahHouse@(3,7) -> Forest@(104,33) -> Town@(0,90) -> Mountain@(15,40) -> Mountain@(64,14)]`, pulso de hito `int-orilla` con prosa de acción `"Echas a andar hacia la orilla norte del lago."`).
- [x] En vivo: `dibujar` muestra la animación elegida en CP-012 y `guardar_cuaderno` la detiene (`cp_cuerpo plan leah int-dibujar dibujar:nueva` -> `continua=dibujar animActividad=True frame=35 atencion=0.6`, prosa `"Anotas formas y sombras en una obra nueva."`; `cp_cuerpo plan leah int-guardar guardar_cuaderno` -> tras 7 ticks de outro `continua=- animActividad=False frame=0 atencion=1.0`, prosa `"Cierras con cuidado el cuaderno y lo guardas."`).
- [x] En vivo: «voy a sentarme en la pasarela del lago» lleva a Leah hasta el punto `sentable` de la pasarela, se detiene orientada al lugar (fallback v1 de `sentarse` según CP-012) y emite una pulsión de hito en telemetría (`cp_cuerpo plan leah int-pasarela sentarse:pasarela_lago` -> `recorrido=[LeahHouse@(3,7) -> Forest@(104,33) -> Town@(0,90) -> Mountain@(15,40) -> Mountain@(57,26)]`, `sentada=True dir=2 frame=0`, y en `pulsos.jsonl`: `id=4 motivo=hito hito={"id":"int-pasarela","origen":"intencion","forma":"pensamiento"}` con búfer `"Buscas asiento en la pasarela de madera del lago."`).

## Fuera de alcance

- Reflejos automáticos (CP-304).
