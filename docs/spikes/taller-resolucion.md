# Spike del taller · Legibilidad a resolución vanilla (V-04)

Fecha: 2026-11-19
Tarea: [CP-013](../../backlog/tareas/CP-013-spike-taller-32px.md)
Fuentes comprobadas: decompilado de Stardew Valley **1.6.15.24356** (`FurnitureDataDefinition.cs`, `Furniture.cs`), `Data/Furniture`, `TileSheets/furniture.xnb` y `Portraits/Leah.xnb` leídos en `/tmp` mediante MonoGame (sin versionar contenido ni texturas del juego).

---

## 1. Medición de V-04: cuadros vanilla en `Data/Furniture`

### 1.1 Contrato de lectura en Stardew Valley 1.6.15

En [`FurnitureDataDefinition.cs`](/home/mtx/.gemini/antigravity/brain/bb7844c8-360d-4d66-95b7-53ac5f845a71/scratch/decomp/StardewValley.ItemTypeDefinitions/FurnitureDataDefinition.cs#L36-L102) y [`Furniture.cs`](/home/mtx/.gemini/antigravity/brain/bb7844c8-360d-4d66-95b7-53ac5f845a71/scratch/decomp/StardewValley.Objects/Furniture.cs#L237-L253):

* **Campos de `Data/Furniture`** (separados por `/`):
  * `0`: nombre interno.
  * `1`: tipo (`"painting"` → `Furniture.painting = 6` en `getTypeNumberFromName`).
  * `2`: tamaño del sprite en casillas (`"<ancho> <alto>"`, o `"-1"` → `getDefaultSourceRectForType(..., 6)` usa **`2×2`** casillas).
  * `3`: caja de colisión en casillas (`"<ancho> <alto>"`, o `"-1"` → `getDefaultBoundingBoxForType(6)` usa **`2×2`** casillas).
  * `4`–`7`: rotaciones, precio, restricción de colocación, texto mostrado.
  * `8`: **índice de sprite** (`int`); si falta o es `< 0`, usa `int.TryParse(itemId)`.
  * `9`: **textura** (si está vacía, `"TileSheets\\furniture"`).
  * `10`: booleano «fuera de venta aleatoria».
* **Región de origen en píxeles** (`Furniture.getDefaultSourceRect`, l. 1639–1643):
  * `x = (SpriteIndex * 16) % texture.Width`
  * `y = (SpriteIndex * 16 / texture.Width) * 16`
  * `width = spriteWidth * 16` px, `height = spriteHeight * 16` px.
* **Dibujo en pantalla** (`Furniture.draw`, l. 1920, 1929): se dibuja a **escala fija `4f` (`×4`)** respecto al `sourceRect`. Las paredes admiten una altura máxima de 2 casillas (`num2 > 2` se recorta a `2` en `canBePlacedHere`, l. 1500–1503).

### 1.2 Censo de entradas `painting` en `Data/Furniture`

Se catalogaron las **177** entradas con campo `1 == "painting"` (`98` en `TileSheets\furniture` y `79` repartidas entre `TileSheets\junimo_furniture`, `furniture_2`, `furniture_3`, `wizard_furniture`, `retro_furniture` y `joja_furniture`):

| Campo 2 | Casillas | Sprite (px) | Entradas | Ejemplos representativos |
|---|---:|---:|---:|---|
| `1 2` | `1×2` | `16×32` | 41 | `'Little Tree'` (`1602`), `'Sun #45'` (`1601`), `'Blueberries'` (`1603`), pósters de cine (`1952`–`1959`) |
| `2 2` | `2×2` | `32×32` | 40 | `'VGA Paradise'` (`1607`), `'Boat'` (`2421`), retratos de aldeanos (`LeahPortrait`, `junimo_furniture` idx `302`) |
| `-1` | `2×2` (por tipo `6`) | `32×32` | 26 | `'The Muzzamaroo'` (`1539`), `'Pathways'` (`1543`), `'Vanilla Villa'` (`1550`), `Calico Falls` (`1565`) |
| `1 3` | `1×3` | `16×48` | 30 | Banderines y paneles altos (`Clouds Banner` `1975`, `LeafyWallPanel` `270` en `junimo_furniture`) |
| `2 3` | `2×3` | `32×48` | 12 | Puertas decorativas y `'Squid Kid'` (`147` en `furniture_2`) |
| `3 2` | `3×2` | `48×32` | 11 | `'Queen of the Gem Sea'` (`1547`), `'Jade Hills'` (`1554`), `'Pig Painting'` (`93` en `furniture_2`) |
| `1 1` | `1×1` | `16×16` | 9 | Escudos, banderines, adornos de pared (`Hanging Shield` `1811`) |
| `2 1` | `2×1` | `32×16` | 4 | Guirnaldas y estantes (`L. Light String` `1745`) |
| `4 2` | `4×2` | `64×32` | 2 | `'Jade Hills Extended'` (`2584`), `CommunityCenter` (`191` en `junimo_furniture`) |
| `3 3` / `4 1` | `3×3` / `4×1` | `48×48` / `64×16` | 1 / 1 | `'Exotic Palace'` (`13`), `DesertFlags` (`135` en `furniture_2`) |

### 1.3 Caja activa frente a caja de sprite en cuadros vanilla

Midiendo la caja opaca real (`alpha > 0`) en `TileSheets/furniture`:

* **Cuadros `1×2` (`16×32` px):** `'Little Tree'`, `'Sun #45'`, `'Blueberries'` y `'Blue City'` ocupan **`12×16` a `13×16` px** con marco incluido (p. ej. `x=2..14, y=4..19`), dejando el resto transparente para alinear en pared.
* **Cuadros `2×2` (`32×32` px):** `'The Muzzamaroo'` (`x=0..31, y=3..30`) y `'Boat'` (`x=0..31, y=3..30`) ocupan **`32×28` px** (`3` px transparentes arriba, `1` px abajo) y tienen un marco de `2` px, dejando **`28×24` px** de lienzo interior útil.
* **Cuadros `3×2` (`48×32` px):** `'Queen of the Gem Sea'` y `'Jade Hills'` ocupan **`47×28` px** (`~43×24` px útiles).

---

## 2. Formatos fijados para CP-401

Alineado con `comun.schema.json#/$defs/formatoObra` (`boceto`, `cuadro_pequeno`, `cuadro_mediano`):

| Formato | Casillas | Sprite PNG | Márgenes (izq, sup, der, inf) | Marco | Lienzo útil | Uso |
|---|---:|---:|---|---:|---:|---|
| `boceto` | `0×0` | `24×24` px | `(0, 0, 0, 0)` | `0` px | `24×24` px | Solo portafolio (no colocable) |
| `cuadro_pequeno` | `1×2` | `16×32` px | `(1, 4, 1, 10)` px | `1` px | `12×16` px | Cuadro vertical en pared (`1 2` en `Data/Furniture`) |
| `cuadro_mediano` | `2×2` | `32×32` px | `(0, 3, 0, 1)` px | `2` px | `28×24` px | Cuadro estándar en pared (`2 2` en `Data/Furniture`) |

El formato `3×2` (`48×32` px) queda medido en V-04 pero fuera del vocabulario v1.

Peso medido de las PNG generadas para `cuadro_mediano` (`32×32` px): **421–678 bytes** (media **530 bytes**), muy por debajo del tope `TamanoMaxMemoriaKB` (`512` KB, ADS §9).

---

## 3. Paleta (`taller/paleta.py`) y pipeline (`taller/asset.py`)

### 3.1 Paleta fija de 32 colores (`PALETA_VANILLA`)

Extraída de píxeles opacos cuantizados (*median cut*) y colores dominantes de `TileSheets/furniture.xnb` y `Portraits/Leah.xnb`:

| Grupo | Colores (hex) | Fuente principal |
|---|---|---|
| Oscuros (3) | `#1A0E13`, `#222235`, `#332926` | `TileSheets/furniture` |
| Grises y luces (6) | `#444B51`, `#675959`, `#8A848A`, `#A0C7CA`, `#DCDCEA`, `#EFF2F8` | `TileSheets/furniture` |
| Azules y agua (4) | `#244E8A`, `#566DA9`, `#3F88A2`, `#49B3B4` | `TileSheets/furniture` (cuadros) |
| Verdes (5) | `#194E47`, `#277042`, `#477C3E`, `#8FA34F`, `#8AD67A` | `TileSheets/furniture` y `Portraits/Leah` |
| Maderas y lino (5) | `#633424`, `#8E3D21`, `#A85D25`, `#CB771E`, `#E3C28F` | `TileSheets/furniture` y `Portraits/Leah` |
| Cálidos, otoño y piel (9) | `#630036`, `#8E241E`, `#C33B49`, `#E07D3C`, `#FE7423`, `#FFAA80`, `#FFE6CA`, `#F2C168`, `#F5E98E` | `TileSheets/furniture` y `Portraits/Leah` |

### 3.2 Script determinista `python -m taller.asset`

Implementado en [`taller/asset.py`](../../taller/asset.py) y verificado con [`taller/tests/test_asset.py`](../../taller/tests/test_asset.py):

1. `recortar_al_aspecto`: recorte centrado exacto a la relación del lienzo útil.
2. `reducir_a_lienzo`: `box` (`Image.BOX`) o `nearest` (`Image.NEAREST`), forzando `NEAREST` si la fuente es más pequeña que el lienzo.
3. `cuantizar_rgba`: distancia perceptual entera *redmean* sobre `PALETA_VANILLA` (alfa binario `0`/`255`, desempate determinista por índice).
4. `limpiar_aislados`: elimina motas aisladas de 1 px rodeadas por `≥ 3` vecinos ortogonales iguales. **Hallazgo del spike:** en la primera pasada sobre `retrato_granjero`, esta limpieza borró los ojos de `1×1` px en `nearest` y rejilla directa; por ello queda activa por defecto solo en `--reduccion box` y desactivada por defecto en `--reduccion nearest` (anulable con `--limpiar` / `--sin-limpieza`).
5. `componer_en_sprite`: coloca el lienzo y dibuja el marco de madera (todo en `PALETA_VANILLA`), dejando el margen de pared transparente.
6. Emite PNG determinista (`compress_level=9`, sin metadatos variables) + vista previa `×4` con `NEAREST`.

---

## 4. Comparativa de las cuatro vías sobre los 5 temas fijos

Temas evaluados: `lago_otono`, `sauce`, `retrato_granjero`, `bodegon`, `playa_atardecer`.

| Vía | Estado en este entorno | Latencia medida | Coste / Licencia | Legibilidad observada |
|---|---|---|---|---|
| **1. Space de Hugging Face** | **No verificada (entorno/red):** `gsbm/sdxl-pixelart` (`sha: 6e8bbc82...`, MIT) devuelve **HTTP 503** (`RUNTIME_ERROR` al iniciar `Client("hysts/SDXL")`). Los 3 Spaces de pixel art más populares sondeados (`artificialguybr/pixel-art-generator`, `Meltedmindz/nerijs-pixel-art-xl`, `pedrogengo/pixel_art`) también están en `RUNTIME_ERROR`. Los repos LoRA de [notas §11](../notas-juego.md#11-generadores-de-imagen-candidatos-p-3--cp-013) (`UmeAiRT`, `deAPI-ai`, `Mystic07`) son pesos Diffusers, no Spaces ejecutables; el Inference Router devuelve **HTTP 401** sin `HF_TOKEN` y `gradio_client` no está instalado. | N/A | Depende del modelo base (`deAPI-ai` y `FLUX.1-dev` tienen licencia **no comercial**). | Pendiente de token/Space operativo. |
| **2. Modelo de imagen por API** | **No verificada (sin credenciales):** `OPENAI_API_KEY`, `GEMINI_API_KEY` y `GOOGLE_API_KEY` no están definidas en el entorno. | N/A | De pago por imagen; términos según proveedor. | Pendiente de credenciales. |
| **3. SVG → rasterizador → `taller.asset`** | **Probada con Inkscape 1.1.2** (`resvg`, `rsvg-convert` y `cairosvg` **no están instalados** en este sistema). Se rasterizaron 5 SVGs (`280×240`) a PNG en `/tmp` y se pasaron por `taller.asset` con `box` y `nearest`. | Inkscape: **226–354 ms**<br>`taller.asset`: **6–22 ms** | Local, sin API ni licencia restrictiva (GPLv2+ para Inkscape; si se incorpora `resvg`, Apache-2.0/MIT). | **Alta en `cuadro_mediano` con `--reduccion nearest`:** los 5 temas se reconocen con claridad a `×4`. Con `--reduccion box`, el antialiasing del SVG introduce píxeles intermedios grises en los bordes (p. ej. ramas del sauce y poste en la playa). |
| **4. Rejilla directa de paleta → `taller.asset`** | **Probada** en `cuadro_mediano` (`28×24`) en los 5 temas y en `cuadro_pequeno` (`12×16`) en `lago_otono` y `retrato_granjero`. | Rasterizado: **0 ms**<br>`taller.asset`: **4,6–5,9 ms** | Solo tokens del LLM (~700 tokens para `28×24` frente a ~250–400 para un SVG geométrico). | **Muy alta en `cuadro_mediano`:** control exacto de siluetas, reflejos y ojos `1×1`. En `cuadro_pequeno` (`12×16`), el retrato se lee bien pero el paisaje del lago queda muy apretado. |

### Muestras generadas (todas obras propias del taller a escala `×4`)

* Comparativa de los 5 temas en `cuadro_mediano` (`Vía 3 SVG + BOX`, `Vía 3 SVG + NEAREST`, `Vía 4 Rejilla directa`): [`hoja_comparativa_x4.png`](muestras-taller/hoja_comparativa_x4.png)
* Prueba en `cuadro_pequeno` (`12×16` útil, `16×32` sprite): [`muestras_pequeno_x4.png`](muestras-taller/muestras_pequeno_x4.png)

---

## 5. Conclusiones provisionales y qué queda pendiente

1. **¿Se reconoce una obra a resolución vanilla?** **Sí en `cuadro_mediano` (`28×24` útil dentro de `32×32`)** cuando se trabaja con masas de color de alto contraste, siluetas claras y sin halos de antialiasing (`Vía 3 + nearest` o `Vía 4`). `cuadro_pequeno` (`12×16` útil dentro de `16×32`) conviene reservarlo para motivos verticales simples (retratos, árboles, flores), no paisajes amplios.
2. **Respaldo local recomendado hasta cerrar las vías externas:** **Vía 3 (SVG geométrico → rasterizado sin antialiasing o con `--reduccion nearest`) como primera vía local**, con **Vía 4 (rejilla directa)** como alternativa sin rasterizador externo. Si en producción se prefiere evitar Inkscape por peso/latencia, queda pendiente instalar y medir `resvg`.
3. **Pendiente para cerrar CP-013:**
   * Probar **Vía 1** (con un Space activo o endpoint con `HF_TOKEN`) y **Vía 2** (API de imagen con clave), registrando latencia, coste y licencia comercial/no comercial real.
   * Instalar/evaluar **`resvg`** si se adopta como rasterizador ligero de Vía 3.
   * **Revisión a ciegas por el usuario y comparación lado a lado a escala `×4` junto a un cuadro vanilla** en partida o captura local (en este spike no se han versionado ni incrustado texturas vanilla en el repositorio).
