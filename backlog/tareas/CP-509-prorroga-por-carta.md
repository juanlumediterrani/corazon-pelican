---
id: CP-509
titulo: Pedir una prórroga por carta y aplicar la respuesta del destinatario
estado: pendiente
prioridad: P2
hito: F5
depende_de: [CP-502]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-09]
---

## Objetivo

Si se agotan las plazas ordinarias y extra para un compromiso y el jugador está ausente, Leah le escribe. La carta identifica lo prometido, el plazo vigente y la nueva fecha que pide. La respuesta del destinatario decide la prórroga (P-12).

## Alcance

- Comprobar qué mecanismo de correo permite entregar una carta individual y ofrecer una respuesta en Stardew 1.6. Si el lector de cartas solo muestra texto, asociar a la carta una vía de respuesta que el destinatario pueda encontrar desde ella.
- El host vincula petición, compromiso y destinatario, y comprueba el estado y la hora de juego al procesar la respuesta. La petición nace sin emitir otra pulsión. La carta sigue legible después del vencimiento.
- Conservar con la partida la petición y su resolución. Registrar en el diario el incumplimiento al vencer el plazo vigente con el compromiso aún pendiente.

## Criterios de aceptación

- [ ] `ProrrogaCompromisoTests` (FakeWorld, R-09). Ante ambos cupos agotados se prepara una sola carta para el jugador ausente. La petición conserva el plazo original y las reservas de las cadenas.
- [ ] Antes del vencimiento, aceptar aplica la nueva fecha una sola vez. Rechazar mantiene la original. Un segundo jugador carece de autoridad para contestar.
- [ ] Al alcanzar el plazo sin prórroga aceptada, el compromiso pendiente queda incumplido. Una carta abierta entonces permite leerla. Si el plazo vence con la carta abierta, una respuesta tardía tampoco reactiva el compromiso.
- [ ] Guardar y cargar conserva plazo, carta y respuesta. Reintentar la entrega o repetir la aceptación conserva una sola carta y una sola modificación del plazo.
La comprobación de la entrega real, la respuesta en la interfaz y la lectura tras el vencimiento queda en la aceptación en DEV de [CP-508](CP-508-aceptacion-dialogo.md).

## Fuera de alcance

- Calibrar el tamaño de la bolsa extra (CP-202 y CP-208). Correo para otros sucesos del valle.
