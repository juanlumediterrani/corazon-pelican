"""Tests deterministas del script de asset y la paleta del taller (CP-013)."""

import json
import subprocess
import sys
from pathlib import Path

import numpy as np
import pytest
from PIL import Image

from taller import asset, paleta


def _imagen_sintetica(ancho: int = 128, alto: int = 96) -> Image.Image:
    """Construye una imagen sintética reproducible con gradientes y franjas."""
    arr = np.zeros((alto, ancho, 4), dtype=np.uint8)
    for y in range(alto):
        for x in range(ancho):
            arr[y, x] = (
                (x * 2 + y) % 256,
                (y * 3 + 40) % 256,
                (180 - x + y) % 256,
                255,
            )
    return Image.fromarray(arr, mode="RGBA")


def _verificar_paleta_estricta(imagen: Image.Image) -> None:
    arr = np.asarray(imagen.convert("RGBA"))
    alfas = set(np.unique(arr[:, :, 3]).tolist())
    assert alfas.issubset({0, 255})

    opacos = arr[arr[:, :, 3] == 255][:, :3]
    assert len(opacos) > 0
    for r, g, b in opacos:
        assert paleta.es_color_en_paleta((int(r), int(g), int(b))), (
            f"Color fuera de paleta: {(int(r), int(g), int(b))}"
        )


def test_paleta_tiene_entre_24_y_32_colores_unicos():
    assert 24 <= len(paleta.PALETA_VANILLA) <= 32
    assert len(set(paleta.hex_paleta())) == len(paleta.PALETA_VANILLA)
    for entrada in paleta.PALETA_VANILLA:
        assert entrada.fuente
        assert entrada.hex.startswith("#") and len(entrada.hex) == 7


@pytest.mark.parametrize(
    ("formato", "tamano_sprite", "tamano_lienzo"),
    [
        ("boceto", (24, 24), (24, 24)),
        ("cuadro_pequeno", (16, 32), (12, 16)),
        ("cuadro_mediano", (32, 32), (28, 24)),
    ],
)
def test_dimensiones_vista_4x_y_paleta_estricta(formato, tamano_sprite, tamano_lienzo):
    bruta = _imagen_sintetica(160, 120)
    sprite, vista_4x, meta = asset.procesar_imagen(bruta, formato)

    assert sprite.size == tamano_sprite
    assert vista_4x.size == (tamano_sprite[0] * 4, tamano_sprite[1] * 4)
    assert tuple(meta["tamano_sprite"]) == tamano_sprite
    assert tuple(meta["tamano_lienzo"]) == tamano_lienzo

    _verificar_paleta_estricta(sprite)
    _verificar_paleta_estricta(vista_4x)


def test_cuadro_mediano_tiene_margen_transparente_y_marco_en_paleta():
    bruta = _imagen_sintetica(64, 64)
    sprite, _, _ = asset.procesar_imagen(bruta, "cuadro_mediano")
    arr = np.asarray(sprite)

    # Mismo margen que cuadros vanilla 32x32 (The Muzzamaroo / Boat): 3 px arriba, 1 px abajo.
    assert np.all(arr[:3, :, 3] == 0)
    assert np.all(arr[31:, :, 3] == 0)
    # Borde del marco opaco en la fila 3.
    assert np.all(arr[3, :, 3] == 255)


def test_determinismo_byte_a_byte_en_disco(tmp_path: Path):
    bruta = _imagen_sintetica(128, 128)
    entrada = tmp_path / "bruta.png"
    bruta.save(entrada, format="PNG")

    sal_a = tmp_path / "a.png"
    sal_b = tmp_path / "b.png"
    v4_a = tmp_path / "a.x4.png"
    v4_b = tmp_path / "b.x4.png"

    asset.main([str(entrada), "--formato", "cuadro_mediano", "--salida", str(sal_a)])
    asset.main([str(entrada), "--formato", "cuadro_mediano", "--salida", str(sal_b)])

    assert sal_a.read_bytes() == sal_b.read_bytes()
    assert v4_a.read_bytes() == v4_b.read_bytes()


def test_limpieza_elimina_punto_aislado_sin_borrar_linea():
    azul = (*paleta.PALETA_VANILLA[paleta.indice_de_nombre("azul_lago")].rgb, 255)
    oro = (*paleta.PALETA_VANILLA[paleta.indice_de_nombre("oro_atardecer")].rgb, 255)

    lienzo = np.zeros((7, 7, 4), dtype=np.uint8)
    lienzo[:, :] = azul
    # Punto aislado de 1 px rodeado de azul: debe limpiarse.
    lienzo[2, 2] = oro
    # Línea horizontal de 3 px: debe conservarse.
    lienzo[5, 2:5] = oro

    limpio = asset.limpiar_aislados(lienzo)
    assert tuple(limpio[2, 2]) == azul
    for x in (2, 3, 4):
        assert tuple(limpio[5, x]) == oro


def test_nearest_conserva_punto_1px_y_box_activa_limpieza_por_defecto():
    piel = (*paleta.PALETA_VANILLA[paleta.indice_de_nombre("crema_piel")].rgb, 255)
    tinta = (*paleta.PALETA_VANILLA[paleta.indice_de_nombre("negro_tinta")].rgb, 255)

    lienzo = np.zeros((24, 24, 4), dtype=np.uint8)
    lienzo[:, :] = piel
    lienzo[10, 8] = tinta
    lienzo[10, 15] = tinta
    img = Image.fromarray(lienzo, mode="RGBA")

    sprite_near, _, meta_near = asset.procesar_imagen(img, "boceto", reduccion="nearest")
    sprite_box, _, meta_box = asset.procesar_imagen(img, "boceto", reduccion="box")

    assert meta_near["limpieza"] is False
    assert meta_box["limpieza"] is True
    assert tuple(np.asarray(sprite_near)[10, 8]) == tinta
    assert tuple(np.asarray(sprite_box)[10, 8]) == piel


def test_recorte_centrado_al_aspecto():
    ancha = Image.new("RGBA", (200, 100), (255, 0, 0, 255))
    recortada = asset.recortar_al_aspecto(ancha, 12, 16)
    assert recortada.size == (75, 100)


def test_cli_python_m_taller_asset_json(tmp_path: Path):
    entrada = tmp_path / "in.png"
    salida = tmp_path / "out.png"
    _imagen_sintetica(96, 96).save(entrada, format="PNG")

    proc = subprocess.run(
        [
            sys.executable,
            "-m",
            "taller.asset",
            str(entrada),
            "--formato",
            "cuadro_pequeno",
            "--reduccion",
            "nearest",
            "--salida",
            str(salida),
            "--json",
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    meta = json.loads(proc.stdout)
    assert meta["formato"] == "cuadro_pequeno"
    assert meta["tamano_sprite"] == [16, 32]
    assert meta["tamano_lienzo"] == [12, 16]
    assert Path(meta["salida"]).exists()
    assert Path(meta["vista_4x"]).exists()
