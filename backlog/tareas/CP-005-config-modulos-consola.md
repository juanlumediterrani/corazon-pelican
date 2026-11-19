---
id: CP-005
titulo: Configuración validada, registro de módulos y comando cp_estado
estado: hecha
prioridad: P2
hito: F0
depende_de: [CP-001]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

El mod arranca con `config.json` validado, registra sus módulos aislados y responde a `cp_estado`.

## Contexto

ADS §12. Patrón `IModule`/`ModContext` y `ServerConfig.Validate()` de Pelican 4Ever (`Pelican4Ever/Framework/IModule.cs`).

## Alcance

- `CorazonConfig` con las claves de ADS §12.1 y `Validate()`, incluida `ModoDesarrollo`, que sustituye al `ModoValidacion.Normal` fijo con el que `ModEntry` valida hoy los perfiles (CP-003).
- Registro de módulos con `try/catch` por módulo; un fallo desactiva ese módulo, no el mod.
- Guarda de host: todo el motor solo corre si `Context.IsMainPlayer`.
- `cp_estado` y `cp_mente on/off`.

## Criterios de aceptación

- [x] Tests de `Validate()`: puerto fuera de rango, `TiempoMaxRespuestaMs <= 0`, personaje sin perfil. Evidencia: `CorazonConfigTests` (`Validate_rechaza_puerto_fuera_de_rango`, `Validate_rechaza_tiempo_max_respuesta_no_positivo`, `Validate_rechaza_sensorio_cadencia_no_positiva`, `Validate_rechaza_tamano_max_memoria_no_positivo`, `Validate_rechaza_modo_reloj_invalido`, `Validate_rechaza_personajes_vacio`, `Validate_rechaza_ids_de_personajes_invalidos`, `Validate_rechaza_personajes_duplicados`, `Validate_rechaza_pelican_api_url_invalida_cuando_no_esta_vacia`, `SeleccionarPersonajes_detecta_personaje_configurado_sin_perfil`, `RegistroModulos_aisla_fallos_y_no_tumba_el_resto`).
- [x] Con config inválida el mod registra los errores y queda inactivo sin excepción no capturada. Evidencia: `ModEntry.Entry` captura errores de lectura y ejecuta `config.Validate()`; si hay errores registra cada uno en `Monitor.Log(..., LogLevel.Error)` y retorna antes de registrar módulos, cargador de perfiles o eventos SMAPI (verificado por compilación e inspección; ejecución en SMAPI vivo pendiente de sesión 🎮).
- [x] `cp_estado` imprime personajes, `MenteHabilitada`, presupuesto y estado de la API. Evidencia: `ConsolaModule` registra `cp_estado` y `cp_mente` (`on`/`off`) sobre `ModContext`; `CorazonPelican.csproj` compila sin advertencias (verificado por compilación e inspección; ejecución en consola SMAPI en vivo pendiente de sesión 🎮).

Verificación final: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 0 advertencias / 0 errores, `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` 210/210, `python3 -m pytest -q` 44 passed.

## Notas de cierre

- `CorazonPelican.Core/Config/CorazonConfig.cs` mantiene `Personajes` vacío por defecto en Core para respetar `ArquitecturaTests.El_codigo_de_core_no_nombra_personajes` (GSS §1.2); `ModEntry.Entry` inicializa `["leah"]` y escribe `config.json` cuando este no existe aún, mientras que un `config.json` existente con `"Personajes": []` sigue siendo rechazado por `Validate()`.
- `RegistroModulos` (`CorazonPelican.Core/Config/CorazonConfig.cs`) y `IModule`/`ModContext` (`CorazonPelican/Framework/`) implementan el aislamiento `try/catch` por módulo; la guarda `Context.IsMainPlayer` protege `SaveLoaded` y `UpdateTicked`.
- **Comprobación en unidad/compilación frente a juego vivo:** la lógica de configuración, validación, selección de perfiles, resolución de `ModoReloj` y aislamiento de módulos está cubierta por pruebas unitarias xUnit y el proyecto SMAPI (`CorazonPelican`) compila contra SMAPI 4.1+ / Stardew 1.6.15 con 0 advertencias; la interacción real en la consola de SMAPI (`cp_estado`, `cp_mente on/off`) y el ciclo `Entry`/`GameLaunched`/`SaveLoaded` dentro del juego vivo quedan para sesión 🎮 (AGENTS.md regla 4).

## Fuera de alcance

- Resto de comandos `cp_*` (cada uno con su tarea).
