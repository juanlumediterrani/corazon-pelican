---
id: CP-110
titulo: Proteger la memoria guardada si falla la carga o la migración
estado: hecha
prioridad: P1
hito: F1
depende_de: [CP-107]
tipo: code-light
complejidad: media
duracion_min: 60
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Proteger la memoria guardada si falla la carga o la migración.

## Evidencia

En `PersistenciaModule.OnSaveLoaded`, una excepción inicia un estado vacío. Después, `OnSaving` puede sobrescribir la clave original. La ausencia de datos en una partida nueva sí es un inicio válido. [Revisión de Argón](CP-905-revision-argon.md).

## Alcance

- `CorazonPelican.Core/Memoria/GestorPersistenciaPartida.cs`: gestor puro de carga, migración, validación y bloqueo de guardado ante error, con diagnóstico que identifica la partida afectada y desbloqueo automático al cargar una partida nueva o legible.
- `CorazonPelican/Modules/PersistenciaModule.cs`: delegación en `GestorPersistenciaPartida` usando `Constants.SaveFolderName` para identificar la partida y evitar llamadas a `WriteSaveData` mientras el guardado esté bloqueado.
- `CorazonPelican.Tests/Memoria/ProteccionGuardadoTests.cs`: pruebas deterministas de fallo de lectura, versión de esquema v99 y recuperación posterior con partidas legibles y nuevas.

## Criterios de aceptación

- [x] Simular un fallo de lectura y una versión de esquema posterior a la admitida. Tras guardar, los datos originales se conservan intactos y el diagnóstico identifica la partida afectada. — Evidencia: `ProteccionGuardadoTests.Fallo_de_lectura_y_version_futura_bloquean_el_guardado_conservan_datos_y_diagnostican_la_partida` (`dotnet test`: 340/340 superados, 0 omitidos).
- [x] Abrir después una partida legible y otra nueva. Ambas pueden guardar su propio estado. — Evidencia: `ProteccionGuardadoTests.Abrir_despues_una_partida_legible_y_otra_nueva_desbloquea_el_guardado_sin_tocar_la_afectada` (`dotnet test`: 340/340 superados, 0 omitidos).
