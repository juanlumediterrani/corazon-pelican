---
id: CP-212
titulo: Agenda del día y validador de cambios de rutina en Core (R-16)
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-003, CP-201]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-16]
fecha: 2026-11-19
---

## Objetivo

Modelar la agenda diaria en Core (`AgendaDia`) y validar `cambios_rutina` contra el horario vigente y las anotaciones/límites del perfil (GSS §5.3, R-16), desbloqueando el pensamiento en cascada (`CP-209`) sin depender del spike en vivo `CP-011`.

## Contexto

GSS §4.1 y §5.3; ADS §2 (`HitosRutina.cs`) y §3.4. Hasta ahora `Rutina.cs` y la guarda `CambioRutinaTests` (R-16) vivían dentro de `CP-306` (F3), bloqueados por el spike en vivo `CP-011` 🎮. Sin embargo, `CP-209` (F2) ya exige que la cadena de despertar construya una agenda válida y pase R-16. Extraer el modelo y validador puros a Core permite probar R-16 y las cadenas en F2 con `FakeWorld`.

## Alcance

- `CorazonPelican.Core/Intencion/Rutina.cs` (`AgendaDia` y `ValidadorCambiosRutina`):
  - Construye la agenda inicial del día a partir de `HorarioDia` y `Rutina` del perfil (`HitosRutina`).
  - Valida `CambioRutina` (`saltar`, `mover`, `sustituir`) y la lista `agenda` propuesta al despertar o al reequilibrar un hito: el hito debe existir en el horario del día, la anotación del perfil debe permitir ese tipo de cambio, respetando `limites.max_por_semana` (con historial de la semana) y `limites.nunca` (`festival`, `lluvia`, `boda`), y sin coordenadas ni cambios sobre `despertar`/`acostarse`.
  - Devuelve `MotivoRechazo` (`codigo`, `campo`, `prosa`) si un cambio no es válido.
  - Aplica cambios válidos sobre la agenda vigente del día sin afectar a los hitos posteriores ni al día siguiente (al nuevo día vuelve la rutina canónica).

## Criterios de aceptación

- [x] `CambioRutinaTests` (guarda de **R-16**): un cambio sobre un hito sin `cambios`, de un tipo no permitido, en una condición prohibida (`festival`, `lluvia`, `boda`) o fuera de `max_por_semana` se rechaza con `MotivoRechazo` en dos formas (`codigo`, `campo`, `prosa`). Evidencia (2026-11-19): `CorazonPelican.Tests/Intencion/CambioRutinaTests.cs` (`Hito_sin_cambios_permitidos_se_rechaza`, `Tipo_de_cambio_no_permitido_por_la_anotacion_se_rechaza`, `Cambio_en_condicion_prohibida_nunca_se_rechaza`, `Exceso_de_max_por_semana_se_rechaza_y_se_libera_a_los_7_dias`, `Dos_cambios_en_el_mismo_lote_que_superan_max_por_semana_se_rechazan_sin_aplicar_ninguno`).
- [x] El validador rechaza cambios sobre `despertar`/`acostarse` o sobre hitos inexistentes en el horario de hoy. Evidencia (2026-11-19): `CambioRutinaTests` (`Cambios_sobre_despertar_o_acostarse_se_rechazan`, `Cambio_sobre_hito_inexistente_en_horario_de_hoy_se_rechaza`, `Mover_sin_hora_valida_se_rechaza`, `Sustituir_sin_plan_o_con_acciones_o_coordenadas_invalidas_se_rechaza`, `Valida_agenda_propuesta_en_respuesta_de_despertar`).
- [x] FakeWorld: saltar o sustituir un hito no altera los posteriores de la agenda, y al cambiar al día siguiente la agenda parte de la rutina canónica. Evidencia (2026-11-19): `CambioRutinaTests.FakeWorld_saltar_o_sustituir_un_hito_no_altera_los_posteriores_y_al_dia_siguiente_vuelve_la_rutina_canonica`; suite completa en verde (`dotnet test`: 286/286 superados; `python3 -m pytest -q`: 44 passed).

## Fuera de alcance

- Suspender/reincorporar `NPC.Schedule` dentro de Stardew Valley y degradación física del cuerpo (`CP-011`, `CP-303`, `CP-306`).
