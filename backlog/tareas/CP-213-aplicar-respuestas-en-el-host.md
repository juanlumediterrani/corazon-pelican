---
id: CP-213
titulo: Confirmar las respuestas de memoria después de aplicarlas en el host
estado: pendiente
prioridad: P1
hito: F2
depende_de: [CP-203, CP-206, CP-109]
tipo: code-light
complejidad: media
duracion_min: 90
sensibilidad: public
---

## Objetivo

Confirmar las respuestas de memoria después de aplicarlas en el host.

## Evidencia

En el registro de módulos del host falta el aplicador de respuestas. En la reproducción, `ApiMotor` confirmó una reflexión y quedaron cero recuerdos guardados. La prueba de CP-206 registra el aplicador desde el propio test. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [ ] Ejercitar el registro que usa el mod con FakeWorld. Enviar una reflexión con origen válido y leerla mediante la API de memoria. Repetir el envío conserva una sola copia.
- [ ] Cuando falte el aplicador para una respuesta con efectos, devolver un motivo y mantener el estado. Verificar que un rechazo de origen tampoco deja cambios parciales.
