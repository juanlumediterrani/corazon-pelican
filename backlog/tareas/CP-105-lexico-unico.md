---
id: CP-105
titulo: Léxico único compartido por percepción, acción y diálogo
estado: hecha
fecha: 2026-11-19
prioridad: P2
hito: F1
depende_de: [CP-003]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
reglas: [R-14]
---

## Objetivo

Un único registro de nombres (lugares, objetos, acciones) que usan la escena, el validador de intenciones y el de actos.

## Contexto

GSS §3.5. Si la escena dice `pasarela_lago`, `ir_a(pasarela_lago)` es válido y Leah puede citar allí.

## Alcance

- `Lexico` en Core construido desde el perfil; resolución id → área/prosa.
- Los generadores de escena y los validadores reciben el mismo objeto, no copias.

## Criterios de aceptación

- [x] `LexicoUnicoTests` comprueba ambas direcciones. En el perfil cargado, cada par del apéndice resuelve la misma instancia de lugar para `ir_a` y `cita`. Una oferta limitada a la playa excluye el resto también de ambas resoluciones. Un lugar añadido al perfil lleva a FakeBody hasta el área declarada.
- [x] `PerfilValidatorTests.Lugar_duplicado_es_error` cubre la duplicidad. `LexicoUnicoTests.El_validador_rechaza_ids_con_mayusculas_o_espacios` cubre mayúsculas, espacios y un salto de línea final.

## Notas de cierre

El perfil conserva su representación de datos. `Lexico.OfrecerLugares` selecciona sus lugares en orden y esa instancia de `LugaresOfrecidos` se comparte entre la proyección al pulso y la resolución del destino. La integración con la prosa de escena corresponde a CP-104. CP-301 y CP-501 comprobarán también las precondiciones de la intención y del acto en el mundo vigente.

Verificación del 2026-11-19 sobre `main`:

- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → `0 Warning(s), 0 Error(s)`.
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → `Failed: 0, Passed: 154, Skipped: 0`.
- `python3 -m pytest -q` → `43 passed`.

## Fuera de alcance

- Léxico de objetos dinámicos del inventario (CP-504).
