---
id: CP-507
titulo: Pensamiento anticipado y su cancelación
estado: en-curso
prioridad: P2
hito: F5
depende_de: [CP-503, CP-201]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
---

## Objetivo

Cuando el jugador se acerca, la respuesta ya está pensada al hablarle.

## Contexto

GSS §4.3 y §7. Imprescindible en vanilla (p50 < 3 s, p95 < 8 s). Cuesta presupuesto: una anticipación que no termina en encuentro debe contar, o no, de forma explícita.

## Alcance

- Detección de saliencia en subida hacia el umbral de pico con interlocutor probable.
- Pulso `anticipado: true`; cancelación en `eventos` si no hay encuentro.
- Reutilización en el pulso de interlocución si la escena no ha cambiado de forma relevante.

## Criterios de aceptación

- [x] FakeWorld: jugador que se acerca y habla → se usa la respuesta anticipada sin nuevo pulso. (`PensamientoAnticipadoTests.Jugador_que_se_acerca_emite_pulso_anticipado_y_hablar_reutiliza_respuesta_sin_nuevo_pulso`)
- [x] Jugador que pasa de largo → cancelación registrada y la mente descarta el resultado. (`PensamientoAnticipadoTests.Jugador_que_pasa_de_largo_cancela_anticipacion_y_descarta_respuesta`)
- [ ] En vivo, vanilla: p50 de respuesta pensada < 3 s en 10 conversaciones.

## Fuera de alcance

- Varios interlocutores (GSS §10).
