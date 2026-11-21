---
id: CP-111
titulo: Parámetros de consolidación de búfer y fuga de huella por personaje (P-10)
estado: hecha
prioridad: P2
hito: F1
depende_de: [CP-106]
tipo: code-light
complejidad: baja
duracion_min: 60
sensibilidad: public
fecha: 2026-11-21
---

## Objetivo

Permitir configurar en el perfil de cada personaje la velocidad de consolidación del búfer y la semivida de fuga de su huella de atención (`P-10`), manteniendo los valores por defecto del motor cuando el perfil omita el bloque.

## Contexto

En `CP-106`, `Bufer` (`MaxEntradasPorDefecto = 12`, `VentanaRecientePorDefectoMin = 60`, `SemividaAntiguasPorDefectoMin = 60.0`) y `HuellaAtencion` (`SemividaPorDefectoMin = 30.0`) ya admitían esos parámetros por constructor, pero `PerfilPersonaje` y `MotorHabitante` usaban solo constantes globales. El acuerdo `P-10` (`2026-11-21`, [`docs/decisiones.md`](../../docs/decisiones.md)) confirma exponerlos en el perfil con valores por defecto del motor.

## Alcance

- Bloque opcional `atencion` en `schemas/perfil.schema.json`, `PerfilPersonaje` (`ConfigAtencion`) y `PerfilValidator`.
- Cableado en `MotorHabitante` y `HabitanteModule` para aplicar la configuración del perfil (o sus valores por defecto si falta).
- Valores explícitos en `CorazonPelican/assets/personajes/leah.json` y pruebas en `PerfilValidatorTests` y `BuferHuellaTests`.

## Criterios de aceptación

- [x] Un perfil sin bloque `atencion` valida y construye `MotorHabitante` con los valores por defecto del motor (`12`, `60`, `60`, `30`). Evidencia: [`PerfilPersonaje.cs`](../../CorazonPelican.Core/Perfil/PerfilPersonaje.cs) + [`MotorHabitante.cs`](../../CorazonPelican.Core/Atencion/MotorHabitante.cs) + [`HabitanteModule.cs`](../../CorazonPelican/Modules/HabitanteModule.cs) + [`leah.json`](../../CorazonPelican/assets/personajes/leah.json) + `MotorHabitante_usa_parametros_de_atencion_del_perfil_o_los_valores_por_defecto_p10` en [`BuferHuellaTests.cs`](../../CorazonPelican.Tests/Atencion/BuferHuellaTests.cs).
- [x] Valores fuera de rango (`bufer_max_entradas < 1`, ventana negativa o semividas `<= 0`) se rechazan en `PerfilValidator` y JSON Schema. Evidencia: [`perfil.schema.json`](../../schemas/perfil.schema.json) + [`PerfilValidator.cs`](../../CorazonPelican.Core/Perfil/PerfilValidator.cs) + `Atencion_ausente_y_valida_pasan_y_valores_fuera_de_rango_fallan` en [`PerfilValidatorTests.cs`](../../CorazonPelican.Tests/Perfil/PerfilValidatorTests.cs).
- [x] Dos `MotorHabitante` con distinto `atencion` en su perfil consolidan el búfer y atenúan la huella a distinta velocidad ante la misma secuencia de observaciones en `BuferHuellaTests`. Evidencia: `MotorHabitante_usa_parametros_de_atencion_del_perfil_o_los_valores_por_defecto_p10` en [`BuferHuellaTests.cs`](../../CorazonPelican.Tests/Atencion/BuferHuellaTests.cs).

Verificación sin juego vivo al cerrar:
- `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → Compilación correcta (0 advertencias, 0 errores).
- `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → Correctas: 368, Con error: 0, Omitido: 0, Total: 368.
- `python3 -m pytest -q` → 113 passed in 1.67s.

## Fuera de alcance

- Calibrar parámetros distintos para un segundo personaje concreto (Fase 2, `CP-410`).
