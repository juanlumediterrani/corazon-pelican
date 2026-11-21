---
id: CP-509
titulo: Pedir una prórroga por carta y aplicar la respuesta del destinatario
estado: hecha
prioridad: P2
hito: F5
depende_de: [CP-502]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
reglas: [R-09]
fecha: 2026-11-21
---

## Objetivo

Si se agotan las plazas ordinarias y extra para un compromiso y el jugador está ausente, Leah le escribe. La carta identifica lo prometido, el plazo vigente y la nueva fecha que pide. La respuesta del destinatario decide la prórroga (P-12).

## Alcance

- Comprobar qué mecanismo de correo permite entregar una carta individual y ofrecer una respuesta en Stardew 1.6. Si el lector de cartas solo muestra texto, asociar a la carta una vía de respuesta que el destinatario pueda encontrar desde ella.
- El host vincula petición, compromiso y destinatario, y comprueba el estado y la hora de juego al procesar la respuesta. La petición nace sin emitir otra pulsión. La carta sigue legible después del vencimiento.
- Conservar con la partida la petición y su resolución. Registrar en el diario el incumplimiento al vencer el plazo vigente con el compromiso aún pendiente.

## Criterios de aceptación

- [x] `ProrrogaCompromisoTests` (FakeWorld, R-09). Ante ambos cupos agotados se prepara una sola carta para el jugador ausente. La petición conserva el plazo original y las reservas de las cadenas. Evidencia: [`ProrrogaPorCarta.cs`](../../CorazonPelican.Core/Dialogo/ProrrogaPorCarta.cs) + [`MotorHabitante.cs`](../../CorazonPelican.Core/Atencion/MotorHabitante.cs) + `Cupos_agotados_con_destinatario_ausente_prepara_una_sola_carta_sin_pulso_y_conserva_plazo_y_cadenas` en [`ProrrogaCompromisoTests.cs`](../../CorazonPelican.Tests/Dialogo/ProrrogaCompromisoTests.cs) y estudio Stardew 1.6 en [`docs/notas-juego.md`](../../docs/notas-juego.md) §13 + [`CorreoProrrogaModule.cs`](../../CorazonPelican/Modules/CorreoProrrogaModule.cs) + [`ModEntry.cs`](../../CorazonPelican/ModEntry.cs).
- [x] Antes del vencimiento, aceptar aplica la nueva fecha una sola vez. Rechazar mantiene la original. Un segundo jugador carece de autoridad para contestar. Evidencia: `Antes_del_vencimiento_aceptar_aplica_una_sola_vez_rechazar_mantiene_plazo_y_otro_jugador_no_autorizado` en [`ProrrogaCompromisoTests.cs`](../../CorazonPelican.Tests/Dialogo/ProrrogaCompromisoTests.cs).
- [x] Al alcanzar el plazo sin prórroga aceptada, el compromiso pendiente queda incumplido. Una carta abierta entonces permite leerla. Si el plazo vence con la carta abierta, una respuesta tardía tampoco reactiva el compromiso. Evidencia: `Vencimiento_marca_incumplido_con_diario_carta_sigue_legible_y_respuesta_tardia_no_reactiva` en [`ProrrogaCompromisoTests.cs`](../../CorazonPelican.Tests/Dialogo/ProrrogaCompromisoTests.cs).
- [x] Guardar y cargar conserva plazo, carta y respuesta. Reintentar la entrega o repetir la aceptación conserva una sola carta y una sola modificación del plazo. Evidencia: `Guardar_y_cargar_conserva_plazo_carta_y_resolucion_y_reintentos_son_idempotentes` en [`ProrrogaCompromisoTests.cs`](../../CorazonPelican.Tests/Dialogo/ProrrogaCompromisoTests.cs).
La comprobación de la entrega real, la respuesta en la interfaz y la lectura tras el vencimiento queda en la aceptación en DEV de [CP-508](CP-508-aceptacion-dialogo.md).

Verificación sin juego vivo al cerrar:
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores).
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 366, Con error: 0, Omitido: 0, Total: 366.
- `python3 -m pytest -q` → 113 passed.

## Fuera de alcance

- Calibrar el tamaño de la bolsa extra (CP-202 y CP-208). Correo para otros sucesos del valle.
