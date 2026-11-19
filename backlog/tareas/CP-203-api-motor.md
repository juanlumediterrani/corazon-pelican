---
id: CP-203
titulo: API loopback del motor con long-poll, cola al hilo principal, idempotencia y época
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-002, CP-005]
tipo: code-light
complejidad: alta
duracion_min: 240
sensibilidad: public
reglas: [R-05]
---

## Objetivo

La mente habla con el motor por `http://127.0.0.1:25560/api/v1/` sin que el juego espere nunca.

## Contexto

ADS §1 (principios), §5. Pelican 4Ever ya resuelve el patrón: `HttpListener` en `AdminApiModule` y `MainThreadActionQueue` en Core; se reutiliza el diseño, no se comparte el puerto. Época (P-1) definida en CP-002.

## Alcance

- `GET salud`, `GET pulsos?since&espera_ms`, `POST respuestas`, `GET memoria/{p}`, `GET eventos?since`, `GET escena/{p}`.
- Solo escucha en loopback; token opcional.
- Toda mutación encolada y drenada en `UpdateTicked`.
- Idempotencia de `POST respuestas` por (`epoca`, `basada_en`); descarte de respuestas obsoletas (ADS §4.1).

## Criterios de aceptación

- [ ] `CuerpoNoBloqueaTests` (FakeWorld + mente lenta de 30 s): el tick del motor nunca tarda más de 2 ms (R-05).
- [ ] Al drenar una respuesta con reflexión, comprobar sus orígenes sobre el pulso correspondiente con el validador de CP-211 antes de aplicar cambios.
- [ ] Repetir un `POST respuestas` devuelve el mismo resultado sin reaplicar.
- [ ] Tras simular una recarga (época nueva), un pulso con `id` repetido no se confunde con el anterior.
- [ ] Un intento de conexión desde una IP no loopback es rechazado (test de configuración del listener).

## Fuera de alcance

- `POST obras/{id}/sprite` (CP-403).
