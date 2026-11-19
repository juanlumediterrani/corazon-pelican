---
id: CP-107
titulo: Estado del personaje persistido con la partida, con versión y migraciones
estado: hecha
fecha: 2026-11-19
prioridad: P1
hito: F1
depende_de: [CP-106]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-10]
---

## Objetivo

`EstadoPersonaje` se escribe en `Saving` y se lee en `SaveLoaded`, solo en el host, de modo que memoria y mundo nunca divergen.

## Contexto

GSS §9.4, ADS §9. P-6 fija la persistencia del diario en F1. CP-403 ampliará el mismo estado con obras en F4. Véase el [registro de decisiones](../../docs/decisiones.md). Pelican tiene el patrón de `ServerWorldState` con migraciones.

## Alcance

- `PersistenciaModule` con `WriteSaveData("CorazonPelican.Estado", …)` y guarda `Context.IsMainPlayer`.
- `version_esquema` y migradores puros en Core con test por versión.
- Día del contador y gasto por personaje de cupo ordinario, reservas y bolsa extra de compromisos (P-12, CP-202).

## Criterios de aceptación

- [x] `MemoriaConPartidaTests` (FakeWorld): guardar, añadir episodios, recargar sin guardar → los episodios nuevos no están. Evidencia: `Guardar_anadir_episodios_y_recargar_sin_guardar_restaura_ultimo_guardado` en `CorazonPelican.Tests/Memoria/MemoriaConPartidaTests.cs` (guarda de R-10).
- [x] Un estado v1 serializado a mano migra a la versión actual en un test. Evidencia: `Estado_v1_serializado_a_mano_migra_a_version_actual_sin_perder_episodios_ni_contadores` en `CorazonPelican.Tests/Memoria/MemoriaConPartidaTests.cs` (v1 explícito con contadores y v1 mínimo sin `version_esquema` ni `entidades`, además de rechazo de versiones futuras no soportadas).
- [x] En FakeWorld, dos días con episodios distinguibles recuperan las entradas guardadas en su día y orden. La comprobación en DEV corresponde a CP-108. Evidencia: `Dos_dias_en_FakeWorld_conservan_episodios_orden_ids_estables_y_contadores_tras_guardar_y_recargar` en `CorazonPelican.Tests/Memoria/MemoriaConPartidaTests.cs` (conserva orden, ids estables y `ContadoresPulsos`).

## Notas de cierre

- `CorazonPelican.Core/Memoria/PersistenciaEstado.cs` y `MigradorEstado.cs`: almacén por partida `EstadoPartida` (`version_esquema = 2`, diccionario `personajeId -> EstadoPersonaje`), serialización determinista y migración pura `v1 -> v2` conservando diario, `SiguienteIdEpisodio`, recuerdos, relaciones, compromisos, intención activa y `ContadoresPulsos`. `EstadoPersonaje.cs` actualizado a `VersionActual = 2` con atributos `[DataContract]`/`[DataMember]` compatibles tanto con `System.Text.Json` como con `Newtonsoft.Json` de SMAPI (`Ida_y_vuelta_con_Newtonsoft_Json_como_WriteSaveData_de_SMAPI_conserva_estado_y_esquema`).
- `CorazonPelican/Modules/PersistenciaModule.cs` (`LadoModulo.Anfitrion`) registrado en `ModEntry.cs`, leyendo/escribiendo `CorazonPelican.Estado` con guarda `Context.IsMainPlayer` y exponiendo `EstadoPartida` y `ObtenerOIniciarEstado(personajeId)` en `ModContext`.
- Verificación: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (0 advertencias, 0 errores), `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (316 superados, 0 con error, 0 omitidos) y `python3 -m pytest -q` (71 passed).

## Fuera de alcance

- Portafolio y texturas (CP-403).
