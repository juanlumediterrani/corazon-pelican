---
id: CP-206
titulo: Recuperación de memoria y reflexión del día
estado: hecha
prioridad: P2
hito: F2
depende_de: [CP-204, CP-107, CP-211]
tipo: code-light
complejidad: media
duracion_min: 150
sensibilidad: public
fecha: 2026-11-19
---

## Objetivo

La mente elige qué recordar en cada pulso y sabe condensar el diario del día en recuerdos; la cadena de acostarse (CP-209) usa esa reflexión.

## Contexto

GSS §9, ADS §6 (*Generative Agents*, Park et al., 2023). Una sola verdad: el motor custodia y persiste; la mente solo tiene caché de trabajo.

## Alcance

- `recuperacion.py`: recencia × importancia × relevancia (sin embeddings remotos obligatorios; relevancia por solapamiento de entidades y léxico como base).
- `reflexion.py`: diario → recuerdos con `origen_diario[]`, enviados como `memoria.reflexiones` según CP-211.

## Criterios de aceptación

- [x] pytest: con 200 entradas de diario, la recuperación cabe en el presupuesto de tokens configurado. Evidencia: `mente/recuperacion.py` (`recuperar_recuerdos` por `recencia × importancia × relevancia` con recorte estricto de tokens) y `mente/tests/test_memoria.py` (`test_con_200_entradas_diario_respeta_presupuesto_tokens_y_prioriza_por_formula`, `test_con_200_recuerdos_con_origen_diario_respeta_presupuesto_estricto`, `test_compatibilidad_con_recuerdos_heredados_en_prosa_sin_origen_diario`).
- [x] Todo recuerdo creado cita al menos una entrada de diario existente. Cubrir el recorrido completo de una referencia desde el pulso hasta la respuesta validada y el estado guardado. Usar los episodios identificados y el validador de CP-211 antes de guardar. Mantener la lectura de recuerdos anteriores en prosa. CP-210 reutiliza estas referencias para el humor. Evidencia: `mente/reflexion.py` (`generar_reflexion`, `validar_reflexiones`, `filtrar_reflexiones`), `CorazonPelican.Core/Memoria/Consolidacion.cs` (`AplicarMemoria`, `ProyectarInstantaneaMemoria`, `ProyectarRecuerdosParaPulso`), `CorazonPelican.Core/Memoria/EstadoPersonaje.cs` (`AgregarRecuerdo`), `CorazonPelican.Core/Api/ApiMotor.cs` (`RegistrarProveedorEstado`), `mente/tests/test_memoria.py` (`TestReflexionDia`) y `CorazonPelican.Tests/Memoria/ReflexionDiarioTests.cs` (recorrido completo diario → pulso → `ApiMotor` / `ValidadorOrigenDiario` → `AplicarMemoria` → `GET /api/v1/memoria/{personaje}` → `PersistenciaEstado`, rechazo de orígenes ajenos/vacíos y compatibilidad en prosa).
- [x] La mente no escribe ningún fichero de memoria propio fuera de la caché descartable (test de sistema de ficheros). Evidencia: `mente/tests/test_memoria.py::TestSinEscrituraEnDisco::test_recuperacion_y_reflexion_no_escriben_ficheros_de_memoria_en_disco` (con `tmp_path` como `cwd`, `list(tmp_path.rglob("*")) == []`).

Verificación final: `python3 -m pytest -q` (94 passed), `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` (0 advertencias, 0 errores) y `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` (319/319 superados).

## Fuera de alcance

- Relaciones con impresiones (se alimentan en F5).
