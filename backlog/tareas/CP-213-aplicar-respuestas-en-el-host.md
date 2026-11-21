---
id: CP-213
titulo: Confirmar las respuestas de memoria después de aplicarlas en el host
estado: hecha
prioridad: P1
hito: F2
depende_de: [CP-203, CP-206, CP-109]
tipo: code-light
complejidad: media
duracion_min: 90
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Confirmar las respuestas de memoria después de aplicarlas en el host.

## Evidencia

En el registro de módulos del host falta el aplicador de respuestas. En la reproducción, `ApiMotor` confirmó una reflexión y quedaron cero recuerdos guardados. La prueba de CP-206 registra el aplicador desde el propio test. [Revisión de Argón](CP-905-revision-argon.md).

## Alcance

- `CorazonPelican.Core/Atencion/MotorHabitante.cs`: `AplicarRespuesta(Pulso, Respuesta)` con validación atómica previa (origen de diario, memoria, intención/cambios de rutina y actos) antes de mutar estado y telemetría.
- `CorazonPelican.Core/Api/ApiMotor.cs` y `CoordinadorApiAnfitrion.cs`: detección explícita de `TieneEfectos(Respuesta)`, rechazo `sin_aplicador` cuando falta el aplicador en respuestas con efectos y registro unificado de estado, escena y aplicador del anfitrión.
- `CorazonPelican/Modules/HabitanteModule.cs`: conexión de `HabitanteModule` y `ApiModule` mediante `CoordinadorApiAnfitrion.Conectar(...)`.
- `CorazonPelican.Tests/Api/AplicacionRespuestasHostTests.cs`: pruebas end-to-end con `FakeWorld`, `ServidorApiMotor`, `MotorHabitante` y `ApiMotor`.

## Criterios de aceptación

- [x] Ejercitar el registro que usa el mod con FakeWorld. Enviar una reflexión con origen válido y leerla mediante la API de memoria. Repetir el envío conserva una sola copia. — Evidencia: `AplicacionRespuestasHostTests.Registro_del_host_confirma_reflexion_en_memoria_y_repetir_conserva_una_sola_copia` (`dotnet test`: 342/342 superados, 0 omitidos).
- [x] Cuando falte el aplicador para una respuesta con efectos, devolver un motivo y mantener el estado. Verificar que un rechazo de origen tampoco deja cambios parciales. — Evidencia: `AplicacionRespuestasHostTests.Respuesta_con_efectos_sin_aplicador_se_rechaza_y_rechazo_de_origen_no_deja_cambios_parciales` (`dotnet test`: 342/342 superados, 0 omitidos).
