"""Paleta fija candidata del taller, extraída de texturas vanilla de Stardew Valley.

Cada color procede de los píxeles opacos cuantizados o dominantes de
``Content/TileSheets/furniture.xnb`` (cuadros y muebles de madera) y
``Content/Portraits/Leah.xnb`` (retrato de Leah), leídos en memoria temporal
sin incorporar ningún asset del juego al repositorio.
"""

from __future__ import annotations

from dataclasses import dataclass

import numpy as np


@dataclass(frozen=True)
class ColorPaleta:
    """Entrada inmutable de la paleta del taller."""

    nombre: str
    hex: str
    rgb: tuple[int, int, int]
    grupo: str
    fuente: str


def _c(nombre: str, hex_str: str, grupo: str, fuente: str) -> ColorPaleta:
    valor = hex_str.lstrip("#")
    rgb = (int(valor[0:2], 16), int(valor[2:4], 16), int(valor[4:6], 16))
    return ColorPaleta(nombre=nombre, hex=f"#{valor.upper()}", rgb=rgb, grupo=grupo, fuente=fuente)


# 32 colores cuantizados/dominantes de TileSheets/furniture y Portraits/Leah.
PALETA_VANILLA: tuple[ColorPaleta, ...] = (
    # Contornos y oscuros (3)
    _c("negro_tinta", "#1A0E13", "oscuro", "TileSheets/furniture (cuadros)"),
    _c("pizarra_nocturna", "#222235", "oscuro", "TileSheets/furniture"),
    _c("sepia_sombra", "#332926", "oscuro", "TileSheets/furniture"),
    # Grises fríos, niebla y luces neutras (6)
    _c("pizarra_media", "#444B51", "gris", "TileSheets/furniture"),
    _c("gris_piedra", "#675959", "gris", "TileSheets/furniture"),
    _c("gris_niebla", "#8A848A", "gris", "TileSheets/furniture"),
    _c("bruma_azulada", "#A0C7CA", "gris", "TileSheets/furniture (cuadros)"),
    _c("nube_fria", "#DCDCEA", "gris", "TileSheets/furniture"),
    _c("blanco_lienzo", "#EFF2F8", "gris", "TileSheets/furniture (cuadros)"),
    # Azules y aguas (4)
    _c("azul_profundo", "#244E8A", "azul", "TileSheets/furniture (cuadros)"),
    _c("azul_pizarra", "#566DA9", "azul", "TileSheets/furniture (cuadros)"),
    _c("azul_lago", "#3F88A2", "azul", "TileSheets/furniture (cuadros)"),
    _c("turquesa_reflejo", "#49B3B4", "azul", "TileSheets/furniture (cuadros)"),
    # Verdes de bosque y sauce (5)
    _c("pino_profundo", "#194E47", "verde", "Portraits/Leah"),
    _c("verde_bosque", "#277042", "verde", "TileSheets/furniture"),
    _c("verde_hoja", "#477C3E", "verde", "TileSheets/furniture"),
    _c("oliva_sauce", "#8FA34F", "verde", "TileSheets/furniture"),
    _c("brote_claro", "#8AD67A", "verde", "Portraits/Leah"),
    # Maderas, marcos y tierras (5)
    _c("madera_oscura", "#633424", "madera", "TileSheets/furniture"),
    _c("madera_calida", "#8E3D21", "madera", "TileSheets/furniture"),
    _c("roble_dorado", "#A85D25", "madera", "TileSheets/furniture"),
    _c("ocre_marco", "#CB771E", "madera", "TileSheets/furniture"),
    _c("arena_lino", "#E3C28F", "madera", "Portraits/Leah"),
    # Rojos, otoño, piel y atardecer (9)
    _c("ciruela_sombra", "#630036", "calido", "Portraits/Leah"),
    _c("granate_barro", "#8E241E", "calido", "TileSheets/furniture (cuadros)"),
    _c("carmin_calido", "#C33B49", "calido", "Portraits/Leah"),
    _c("terracota", "#E07D3C", "calido", "TileSheets/furniture (cuadros)"),
    _c("naranja_sol", "#FE7423", "calido", "Portraits/Leah"),
    _c("melocoton_piel", "#FFAA80", "calido", "Portraits/Leah"),
    _c("crema_piel", "#FFE6CA", "calido", "Portraits/Leah"),
    _c("oro_atardecer", "#F2C168", "calido", "TileSheets/furniture (cuadros)"),
    _c("luz_amarilla", "#F5E98E", "calido", "TileSheets/furniture (cuadros)"),
)

_RGB_MATRIZ: np.ndarray = np.array([c.rgb for c in PALETA_VANILLA], dtype=np.int32)
_INDICE_POR_NOMBRE: dict[str, int] = {c.nombre: i for i, c in enumerate(PALETA_VANILLA)}
_INDICE_POR_RGB: dict[tuple[int, int, int], int] = {c.rgb: i for i, c in enumerate(PALETA_VANILLA)}


def rgb_paleta() -> np.ndarray:
    """Devuelve una copia de la tabla ``(N, 3)`` uint8 con los colores RGB de la paleta."""
    return _RGB_MATRIZ.astype(np.uint8, copy=True)


def hex_paleta() -> tuple[str, ...]:
    """Devuelve los códigos hexadecimales de la paleta en orden fijo."""
    return tuple(c.hex for c in PALETA_VANILLA)


def indice_de_nombre(nombre: str) -> int:
    """Devuelve el índice de paleta para ``nombre`` o lanza ``KeyError``."""
    return _INDICE_POR_NOMBRE[nombre]


def es_color_en_paleta(rgb: tuple[int, int, int]) -> bool:
    """Indica si una terna ``(R, G, B)`` pertenece exactamente a ``PALETA_VANILLA``."""
    return rgb in _INDICE_POR_RGB


def cuantizar_rgba(rgba: np.ndarray, *, umbral_alfa: int = 128) -> np.ndarray:
    """Cuantiza un array RGBA ``(H, W, 4)`` a ``PALETA_VANILLA`` de forma determinista.

    Los píxeles con ``alfa < umbral_alfa`` pasan a transparente puro ``(0, 0, 0, 0)``.
    Los opacos se asignan al color más cercano usando distancia perceptual *redmean*
    (desempate determinista por el menor índice de paleta).
    """
    if rgba.ndim != 3 or rgba.shape[2] != 4:
        raise ValueError(f"Se esperaba un array (H, W, 4), recibido {rgba.shape}")

    h, w, _ = rgba.shape
    salida = np.zeros((h, w, 4), dtype=np.uint8)
    opacos = rgba[:, :, 3] >= umbral_alfa
    if not np.any(opacos):
        return salida

    pixeles = rgba[opacos][:, :3].astype(np.int32)  # (M, 3)
    pal = _RGB_MATRIZ  # (N, 3)

    # Distancia perceptual redmean en aritmética entera determinista.
    r_med = (pixeles[:, 0:1] + pal[None, :, 0]) >> 1
    dr = pixeles[:, 0:1] - pal[None, :, 0]
    dg = pixeles[:, 1:2] - pal[None, :, 1]
    db = pixeles[:, 2:3] - pal[None, :, 2]

    dist = (
        ((512 + r_med) * dr * dr)
        + (1024 * dg * dg)
        + ((767 - r_med) * db * db)
    )
    indices = np.argmin(dist, axis=1)
    elegidos = pal[indices].astype(np.uint8)

    salida[opacos, :3] = elegidos
    salida[opacos, 3] = 255
    return salida
