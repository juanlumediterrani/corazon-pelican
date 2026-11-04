---
id: CP-102
titulo: Saliencia de cinco capas con distancia, línea de visión y novedad
estado: pendiente
prioridad: P1
hito: F1
depende_de: [CP-003, CP-004]
tipo: code-light
complejidad: media
duracion_min: 120
sensibilidad: public
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

- [ ] Tests de propiedades: más distancia nunca sube la saliencia; sin visión, una entidad física no supera la banda de periferia.
- [ ] Exponer la misma entidad N veces reduce su novedad monótonamente y se recupera tras ausencia.
- [ ] Con los mismos rasgos y estado, el jugador con 9 corazones supera al de 1 corazón a igual distancia.

## Fuera de alcance

- Ajuste fino de pesos (se hace en CP-108 con escenas reales).
