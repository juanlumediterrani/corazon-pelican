---
id: CP-013
titulo: Spike del taller · ¿se reconoce una obra a la resolución de un cuadro vanilla? (V-04)
estado: en-curso
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

- Medir V-04: tamaños de los cuadros vanilla en `Data/Furniture` (campo 2) y localizar cada cuadro en la hoja con el índice de sprite (campo 8) y la textura (campo 9).
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

- [ ] `docs/spikes/taller-resolucion.md` con tamaños vanilla citados, las imágenes de las cuatro vías y la decisión razonada. *(Avance parcial: V-04 medido por completo, `docs/spikes/taller-resolucion.md` redactado y muestras generadas para Vía 3 con Inkscape 1.1.2 + Vía 4 rejilla directa en `docs/spikes/muestras-taller/`; faltan Vía 1 —Spaces candidatos en `RUNTIME_ERROR`, sin `HF_TOKEN` ni `gradio_client`—, Vía 2 —sin `OPENAI_API_KEY`/`GEMINI_API_KEY`— y `resvg` no instalado).*
- [ ] Las imágenes se ven a escala de juego (×4) al lado de un cuadro vanilla. *(Muestras propias a `×4` generadas en `docs/spikes/muestras-taller/hoja_comparativa_x4.png`; pendiente revisión a ciegas / comparación visual lado a lado con cuadro vanilla por el usuario).*
- [x] Se fija el formato de `boceto`, `cuadro_pequeno` y `cuadro_mediano` (lienzo en px y casillas) para CP-401. *(Fijados en `taller/asset.py` (`FORMATOS`) y documentados en `docs/spikes/taller-resolucion.md` §2: `boceto` `0×0` `24×24`, `cuadro_pequeno` `1×2` `16×32` sprite / `12×16` lienzo, `cuadro_mediano` `2×2` `32×32` sprite / `28×24` lienzo).*
- [ ] Se elige el generador principal y el orden de respaldo, con la licencia de cada uno registrada. *(Respaldo local validado provisionalmente: Vía 3 SVG + `--reduccion nearest` con fallback Vía 4; falta probar Vía 1 y Vía 2 para decidir el generador principal).*
- [x] `python -m taller.asset` funciona a mano sobre cualquier PNG y es determinista (mismo bruto → misma salida). *(Implementado con `taller/paleta.py` de 32 colores y verificado por `taller/tests/test_asset.py`).*

## Fuera de alcance

- Integración con el mod o la mente.
