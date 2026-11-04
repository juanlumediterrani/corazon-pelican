---
id: CP-013
titulo: Spike del taller · ¿se reconoce una obra a la resolución de un cuadro vanilla? (V-04)
estado: pendiente
prioridad: P1
hito: S
depende_de: [CP-001]
tipo: analysis
complejidad: media
duracion_min: 180
sensibilidad: public
---

## Objetivo

Decidir con imágenes reales cómo se fabrican las obras antes de construir nada alrededor de ellas.

## Contexto

La obra es lo que hace visible el sistema (GSS §1, §6) y es el riesgo técnico más independiente del juego: se puede probar en Python hoy. Los cuadros vanilla miden pocas casillas (V-04, del orden de 16–32 × 32 px de lienzo útil). Decisión P-3 (ADS §7): el taller pasa de la prosa a un asset de verdad con un **generador** (Space de Hugging Face adecuado o modelo de imagen capaz; SVG del LLM como respaldo) y un **script de asset** determinista que hace el resto.

Candidatos sin probar, con enlaces y licencias: [notas](../../docs/notas-juego.md) §11. Ninguno documenta control de paleta: lo pone el script. Campos de `Data/Furniture` en notas §5.

## Alcance

- Medir V-04: tamaños de los cuadros vanilla en `Data/Furniture` (campo 2) y su textura (campo 9, confirmado como índice de textura en `FurnitureDataDefinition`).
- Extraer una paleta candidata de `TileSheets/furniture` y retratos.
- Primer borrador de `taller/asset.py` (CLI `python -m taller.asset`): imagen bruta → recorte → reducción → cuantización a paleta → limpieza → PNG del formato + vista a ×4.
- Comparar con 5 temas fijos (lago gris de otoño, sauce, retrato del granjero, bodegón, playa al atardecer), todas por el mismo script de asset:
  1. Space de Hugging Face de pixel art (al menos dos candidatos), con la revisión fijada.
  2. Modelo de imagen capaz por API, con prompt de estilo pixel art.
  3. SVG del LLM → resvg (respaldo local, ADS §7).
  4. Rejilla directa de índices de paleta escrita por el LLM al tamaño final.
- Anotar por vía: latencia, coste, licencia del modelo y disponibilidad (cola, límites).
- Revisión a ciegas: ¿se reconoce el tema? ¿encaja junto a cuadros vanilla?

## Criterios de aceptación

- [ ] `docs/spikes/taller-resolucion.md` con tamaños vanilla citados, las imágenes de las tres vías y la decisión razonada.
- [ ] Las imágenes se ven a escala de juego (×4) al lado de un cuadro vanilla.
- [ ] Se fija el formato de `boceto`, `cuadro_pequeno` y `cuadro_mediano` (lienzo en px y casillas) para CP-401.
- [ ] Se elige el generador principal y el orden de respaldo, con la licencia de cada uno registrada.
- [ ] `python -m taller.asset` funciona a mano sobre cualquier PNG y es determinista (mismo bruto → misma salida).

## Fuera de alcance

- Integración con el mod o la mente.
