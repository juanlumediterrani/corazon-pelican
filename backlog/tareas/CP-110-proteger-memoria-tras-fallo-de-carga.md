---
id: CP-110
titulo: Proteger la memoria guardada si falla la carga o la migración
estado: pendiente
prioridad: P1
hito: F1
depende_de: [CP-107]
tipo: code-light
complejidad: media
duracion_min: 60
sensibilidad: public
---

## Objetivo

Proteger la memoria guardada si falla la carga o la migración.

## Evidencia

En `PersistenciaModule.OnSaveLoaded`, una excepción inicia un estado vacío. Después, `OnSaving` puede sobrescribir la clave original. La ausencia de datos en una partida nueva sí es un inicio válido. [Revisión de Argón](CP-905-revision-argon.md).

## Criterios de aceptación

- [ ] Simular un fallo de lectura y una versión de esquema posterior a la admitida. Tras guardar, los datos originales se conservan intactos y el diagnóstico identifica la partida afectada.
- [ ] Abrir después una partida legible y otra nueva. Ambas pueden guardar su propio estado.
