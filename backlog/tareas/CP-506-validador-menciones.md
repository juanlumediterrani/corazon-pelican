---
id: CP-506
titulo: Validador de menciones · la mente no nombra lo que no existe para Leah
estado: hecha
prioridad: P2
hito: F5
depende_de: [CP-501, CP-206]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
reglas: [R-15]
fecha: 2026-11-21
---

## Objetivo

Medir y hacer cumplir el criterio «sin alucinación de mundo»: 100 % de las entidades que nombra la mente existen en su escena o su memoria.

## Contexto

GSS §8.5 y §12. R-02 cubre la escena que genera el motor; nada cubría lo que dice la mente. Decisión P-4: regla **R-15** en la GSS §13, con esta tarea como guarda.

## Alcance

- Extraer de la frase y la intención nombres propios, ids del léxico y nombres de objetos conocidos del juego.
- Contrastarlos con escena + memoria recuperada; tratar como acto inválido (reformular una vez) o como aviso según configuración.
- Métrica en telemetría.

## Criterios de aceptación

- [x] Tests: «la garza del muelle» sin garza en escena ni memoria → detectado; «Ana» presente → aceptado. Evidencia: [`ValidadorMenciones.cs`](../../CorazonPelican.Core/Dialogo/ValidadorMenciones.cs) + `Garza_del_muelle_sin_garza_ni_muelle_en_escena_ni_memoria_es_detectada_y_rechazada` y `Ana_presente_es_aceptada_Bruno_ausente_rechazado_y_genericos_ignorados` en [`MencionesAncladasTests.cs`](../../CorazonPelican.Tests/Dialogo/MencionesAncladasTests.cs).
- [x] La tasa de menciones no ancladas aparece por día en `rechazos.jsonl`. Evidencia: [`TelemetriaJsonl.cs`](../../CorazonPelican.Core/Observabilidad/TelemetriaJsonl.cs) + [`CorazonConfig.cs`](../../CorazonPelican.Core/Config/CorazonConfig.cs) + [`MotorHabitante.cs`](../../CorazonPelican.Core/Atencion/MotorHabitante.cs) + [`HabitanteModule.cs`](../../CorazonPelican/Modules/HabitanteModule.cs) + `Telemetria_registra_tasa_diaria_en_rechazos_jsonl_y_soporta_modo_aviso_y_rechazar` en [`MencionesAncladasTests.cs`](../../CorazonPelican.Tests/Dialogo/MencionesAncladasTests.cs).
- [x] `MencionesAncladasTests` verde: R-15 entra en vigor. Evidencia: verificación sin juego vivo tras el último cambio: `dotnet build CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false` → 0 advertencias, 0 errores; `dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false --no-build` → 362 superadas, 0 fallidas, 0 omitidas; `python3 -m pytest -q` → 113 passed.

## Fuera de alcance

- Entidades genéricas sin nombre («alguien», «el viento»).
