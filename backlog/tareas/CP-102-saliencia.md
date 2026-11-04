---
id: CP-102
titulo: Saliencia de cinco capas con distancia, línea de visión y novedad
estado: hecha
prioridad: P1
hito: F1
depende_de: [CP-003, CP-004]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
fecha: 2026-11-04
---

## Objetivo

Una función pura que da a cada entidad una saliencia a partir de rasgos, estado, intención, novedad y relación.

## Contexto

GSS §3.2. `Saliencia.cs` y `Novedad.cs` en Core. La intención aún no existe en F1: su capa vale 1 hasta CP-301.

## Alcance

- Combinación multiplicativa de capas (ADS §2) atenuada por distancia y visión.
- Habituación por entidad con decaimiento (la niebla nueva pesa, el lago de siempre no).
- Capa de relación desde corazones nativos.

## Criterios de aceptación

- [x] Tests de propiedades: más distancia nunca sube la saliencia; sin visión, una entidad física no supera la banda de periferia. Evidencia: `SalienciaTests.Mas_distancia_nunca_sube_la_saliencia` (6 entidades × 3 estados × 3 novedades × 61 distancias) y `Sin_vision_una_entidad_fisica_no_pasa_de_la_periferia` (incluso con intención ×10).
- [x] Exponer la misma entidad N veces reduce su novedad monótonamente y se recupera tras ausencia. Evidencia: `NovedadTests` (200 exposiciones monótonas hasta el piso; recupera la mitad en la semivida; no depende de cómo se trocee el tiempo) y `En_el_mundo_simulado_el_lago_pierde_novedad_mientras_lo_mira` con FakeWorld.
- [x] Con los mismos rasgos y estado, el jugador con 9 corazones supera al de 1 corazón a igual distancia. Evidencia: `Con_los_mismos_rasgos_y_estado_nueve_corazones_superan_a_uno_a_igual_distancia` (0–40 casillas) y `Con_muchos_corazones_lo_nota_llegar_de_lejos`.

Verificación final: `dotnet build` 0 advertencias, `dotnet test` 99/99, `python3 -m pytest -q` 40 passed.

## Notas de cierre

- Fórmula: `x = rasgos × estado × intención × novedad × relación × e^(−d / (12 × relación))`, saliencia `x / (1 + x)` en [0, 1). Rasgo e intención: el mayor peso entre las etiquetas de la entidad (1 si no figura). Relación: `1 + 0,1 × corazones`, que también alarga el alcance. Estado: `apertura × (0,5 + 0,5 × energía)`. Sin visión: tope en el umbral de periferia.
- `Saliencia.Calcular` devuelve un `DesgloseSaliencia` con cada capa, para depurar y, más adelante, explicar en la telemetría por qué algo fue saliente.
- Novedad en **minutos de juego** (τ de habituación 15 min, semivida de recuperación 12 h, piso 0,3): habituarse dura lo mismo en vanilla y en Pelican. Es una sola escala; la familiaridad de días («el lago de siempre») queda para la memoria. Estos parámetros podrían ser del perfil (P-10).
- `EntidadObservada` gana `Visible` (por defecto `true`); la línea de visión real la calcula CP-101.
- La luz y el clima aún no son entidades: CP-101/CP-104 decidirán cómo entran (la huella ya usa `ref: luz`).
- **Calibración pendiente** (anotada en CP-108): con los umbrales actuales del perfil, casi nada llega a foco sin intención y el pico es prácticamente inalcanzable.

## Fuera de alcance

- Ajuste fino de pesos (se hace en CP-108 con escenas reales).
