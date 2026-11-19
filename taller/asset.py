"""Script de asset determinista del taller (P-3, ADS §7).

Convierte una imagen bruta en un sprite PNG listo para ``Data/Furniture`` (o boceto de
portafolio) y su vista previa a ``×4`` con vecino más cercano:

    imagen bruta ─▶ recorte centrado ─▶ reducción (box/nearest)
                 ─▶ cuantización a PALETA_VANILLA ─▶ limpieza ─▶ marco ─▶ PNG + ×4

Uso CLI:
    python -m taller.asset entrada.png --formato cuadro_mediano --salida obra.png
"""

from __future__ import annotations

import argparse
import hashlib
import json
from dataclasses import dataclass
from pathlib import Path
from typing import Literal

import numpy as np
from PIL import Image

from taller.paleta import PALETA_VANILLA, cuantizar_rgba, indice_de_nombre

MetodoReduccion = Literal["box", "nearest"]


@dataclass(frozen=True)
class EspecificacionFormato:
    """Geometría de cada formato de obra soportado por el taller."""

    nombre: str
    casillas: tuple[int, int]
    tamano_sprite: tuple[int, int]
    margen: tuple[int, int, int, int]  # (izq, sup, der, inf) en px
    grosor_marco: int

    @property
    def caja_enmarcada(self) -> tuple[int, int, int, int]:
        """Rectángulo ``(x0, y0, x1, y1)`` ocupado por el cuadro incluido su marco."""
        w, h = self.tamano_sprite
        mi, ms, md, mf = self.margen
        return (mi, ms, w - md, h - mf)

    def tamano_lienzo(self, enmarcar: bool = True) -> tuple[int, int]:
        """Dimensiones ``(ancho, alto)`` del lienzo interior útil en píxeles."""
        if not enmarcar or self.grosor_marco == 0:
            return self.tamano_sprite
        x0, y0, x1, y1 = self.caja_enmarcada
        g = self.grosor_marco
        return (x1 - x0 - 2 * g, y1 - y0 - 2 * g)


FORMATOS: dict[str, EspecificacionFormato] = {
    "boceto": EspecificacionFormato(
        nombre="boceto",
        casillas=(0, 0),
        tamano_sprite=(24, 24),
        margen=(0, 0, 0, 0),
        grosor_marco=0,
    ),
    "cuadro_pequeno": EspecificacionFormato(
        nombre="cuadro_pequeno",
        casillas=(1, 2),
        tamano_sprite=(16, 32),
        margen=(1, 4, 1, 10),
        grosor_marco=1,
    ),
    "cuadro_mediano": EspecificacionFormato(
        nombre="cuadro_mediano",
        casillas=(2, 2),
        tamano_sprite=(32, 32),
        margen=(0, 3, 0, 1),
        grosor_marco=2,
    ),
}

_FILTROS_PIL = {
    "box": Image.BOX,
    "nearest": Image.NEAREST,
}


def obtener_formato(nombre: str) -> EspecificacionFormato:
    """Devuelve la especificación del formato o lanza ``ValueError``."""
    try:
        return FORMATOS[nombre]
    except KeyError as exc:
        validos = ", ".join(FORMATOS)
        raise ValueError(f"Formato desconocido {nombre!r}; válidos: {validos}") from exc


def recortar_al_aspecto(imagen: Image.Image, ancho_obj: int, alto_obj: int) -> Image.Image:
    """Recorta al centro respetando exactamente la relación de aspecto objetivo."""
    if ancho_obj <= 0 or alto_obj <= 0:
        raise ValueError("El tamaño objetivo debe ser positivo")
    w, h = imagen.size
    if w <= 0 or h <= 0:
        raise ValueError("La imagen de entrada está vacía")

    # Comparación exacta sin coma flotante: w/h frente a ancho_obj/alto_obj.
    lhs = w * alto_obj
    rhs = h * ancho_obj
    if lhs == rhs:
        return imagen
    if lhs > rhs:
        nuevo_w = max(1, (h * ancho_obj) // alto_obj)
        x0 = (w - nuevo_w) // 2
        return imagen.crop((x0, 0, x0 + nuevo_w, h))
    nuevo_h = max(1, (w * alto_obj) // ancho_obj)
    y0 = (h - nuevo_h) // 2
    return imagen.crop((0, y0, w, y0 + nuevo_h))


def reducir_a_lienzo(
    imagen: Image.Image,
    ancho_obj: int,
    alto_obj: int,
    metodo: MetodoReduccion = "box",
) -> Image.Image:
    """Redimensiona ``imagen`` al lienzo objetivo con ``box`` o ``nearest``."""
    if metodo not in _FILTROS_PIL:
        raise ValueError(f"Método de reducción desconocido: {metodo!r}")
    w, h = imagen.size
    if (w, h) == (ancho_obj, alto_obj):
        return imagen
    # Si la fuente es menor que el lienzo en algún eje, NEAREST evita interpolar bloques.
    filtro = Image.NEAREST if (w < ancho_obj or h < alto_obj) else _FILTROS_PIL[metodo]
    return imagen.resize((ancho_obj, alto_obj), resample=filtro)


def limpiar_aislados(rgba: np.ndarray) -> np.ndarray:
    """Elimina puntos aislados de 1 píxel rodeados por ≥ 3 vecinos ortogonales idénticos.

    Conserva contornos, horizontes y detalles de 1 px (solo actúa si el píxel no tiene
    ningún vecino ortogonal igual y al menos tres vecinos opacos coinciden en un color).
    """
    h, w, c = rgba.shape
    if c != 4:
        raise ValueError(f"Se esperaba array RGBA (H, W, 4), recibido {rgba.shape}")
    salida = rgba.copy()
    for y in range(h):
        for x in range(w):
            actual = tuple(int(v) for v in rgba[y, x])
            if actual[3] == 0:
                continue
            vecinos: list[tuple[int, int, int, int]] = []
            for dy, dx in ((-1, 0), (1, 0), (0, -1), (0, 1)):
                ny, nx = y + dy, x + dx
                if 0 <= ny < h and 0 <= nx < w:
                    v = tuple(int(p) for p in rgba[ny, nx])
                    if v[3] == 255:
                        vecinos.append(v)
            if len(vecinos) < 3 or any(v == actual for v in vecinos):
                continue
            # Buscar consenso de >= 3 vecinos del mismo color.
            for candidato in vecinos:
                if vecinos.count(candidato) >= 3:
                    salida[y, x] = np.array(candidato, dtype=np.uint8)
                    break
    return salida


def _rgb_paleta(nombre: str) -> tuple[int, int, int, int]:
    r, g, b = PALETA_VANILLA[indice_de_nombre(nombre)].rgb
    return (r, g, b, 255)


def componer_en_sprite(
    lienzo_rgba: np.ndarray,
    spec: EspecificacionFormato,
    *,
    enmarcar: bool = True,
) -> np.ndarray:
    """Coloca el lienzo cuantizado dentro del sprite final y dibuja el marco si aplica."""
    sw, sh = spec.tamano_sprite
    if not enmarcar or spec.grosor_marco == 0:
        if lienzo_rgba.shape[:2] != (sh, sw):
            raise ValueError(
                f"Lienzo {lienzo_rgba.shape[1]}x{lienzo_rgba.shape[0]} no coincide con sprite {sw}x{sh}"
            )
        return lienzo_rgba.copy()

    sprite = np.zeros((sh, sw, 4), dtype=np.uint8)
    x0, y0, x1, y1 = spec.caja_enmarcada
    g = spec.grosor_marco
    lw, lh = spec.tamano_lienzo(enmarcar=True)
    if lienzo_rgba.shape[:2] != (lh, lw):
        raise ValueError(
            f"Lienzo {lienzo_rgba.shape[1]}x{lienzo_rgba.shape[0]} no coincide con {lw}x{lh}"
        )

    sprite[y0 + g : y1 - g, x0 + g : x1 - g] = lienzo_rgba

    tinta = _rgb_paleta("negro_tinta")
    sepia = _rgb_paleta("sepia_sombra")
    madera_osc = _rgb_paleta("madera_oscura")
    madera_cal = _rgb_paleta("madera_calida")
    roble = _rgb_paleta("roble_dorado")
    ocre = _rgb_paleta("ocre_marco")

    if g == 1:
        # Marco fino de 1 px (cuadro pequeño): luz arriba/izquierda, sombra abajo/derecha.
        for x in range(x0, x1):
            sprite[y0, x] = madera_cal
            sprite[y1 - 1, x] = sepia
        for y in range(y0 + 1, y1 - 1):
            sprite[y, x0] = madera_osc
            sprite[y, x1 - 1] = sepia
        sprite[y0, x0] = roble
        sprite[y1 - 1, x1 - 1] = tinta
    else:
        # Marco biselado de 2 px (cuadro mediano), análogo a cuadros vanilla 32x32.
        for x in range(x0, x1):
            sprite[y0, x] = madera_osc
            sprite[y1 - 1, x] = tinta
        for y in range(y0 + 1, y1 - 1):
            sprite[y, x0] = madera_osc
            sprite[y, x1 - 1] = tinta

        ix0, iy0, ix1, iy1 = x0 + 1, y0 + 1, x1 - 1, y1 - 1
        for x in range(ix0, ix1):
            sprite[iy0, x] = ocre
            sprite[iy1 - 1, x] = madera_cal
        for y in range(iy0 + 1, iy1 - 1):
            sprite[y, ix0] = roble
            sprite[y, ix1 - 1] = madera_osc
        sprite[iy0, ix0] = ocre
        sprite[iy1 - 1, ix1 - 1] = madera_osc

    return sprite


def generar_vista_4x(sprite: Image.Image, factor: int = 4) -> Image.Image:
    """Escala el sprite a resolución de juego (``×4``) con vecino más cercano."""
    if factor <= 0:
        raise ValueError("El factor de escala debe ser positivo")
    w, h = sprite.size
    return sprite.resize((w * factor, h * factor), resample=Image.NEAREST)


def procesar_imagen(
    imagen: Image.Image,
    formato: str,
    *,
    reduccion: MetodoReduccion = "box",
    enmarcar: bool = True,
    limpiar: bool | None = None,
    umbral_alfa: int = 128,
) -> tuple[Image.Image, Image.Image, dict]:
    """Ejecuta el pipeline determinista completo y devuelve ``(sprite, vista_4x, metadatos)``.

    Si ``limpiar`` es ``None``, se activa solo en reducciones ``box`` (para eliminar
    motas aisladas tras reescalar imágenes grandes) y se desactiva en ``nearest`` para
    conservar detalles intencionales de 1 px (ojos, estrellas, reflejos).
    """
    spec = obtener_formato(formato)
    con_marco = enmarcar and spec.grosor_marco > 0
    lw, lh = spec.tamano_lienzo(enmarcar=con_marco)
    aplicar_limpieza = (reduccion == "box") if limpiar is None else limpiar

    rgba_in = imagen.convert("RGBA")
    recortada = recortar_al_aspecto(rgba_in, lw, lh)
    reducida = reducir_a_lienzo(recortada, lw, lh, metodo=reduccion)
    cuantizada = cuantizar_rgba(np.asarray(reducida), umbral_alfa=umbral_alfa)
    if aplicar_limpieza:
        cuantizada = limpiar_aislados(cuantizada)

    sprite_arr = componer_en_sprite(cuantizada, spec, enmarcar=con_marco)
    sprite = Image.fromarray(sprite_arr, mode="RGBA")
    vista_4x = generar_vista_4x(sprite, factor=4)

    # Colores usados en el sprite (excluyendo transparencia) en orden estable de aparición.
    opacos = sprite_arr[sprite_arr[:, :, 3] == 255][:, :3]
    colores_hex = sorted({f"#{int(r):02X}{int(g):02X}{int(b):02X}" for r, g, b in opacos})

    meta = {
        "formato": spec.nombre,
        "casillas": list(spec.casillas),
        "tamano_sprite": list(spec.tamano_sprite),
        "tamano_lienzo": [lw, lh],
        "reduccion": reduccion,
        "enmarcado": con_marco,
        "limpieza": aplicar_limpieza,
        "colores_usados": len(colores_hex),
        "paleta_usada": colores_hex,
    }
    return sprite, vista_4x, meta


def guardar_png_determinista(imagen: Image.Image, ruta: Path) -> str:
    """Guarda ``imagen`` como PNG determinista sin metadatos variables y devuelve su SHA-256."""
    ruta.parent.mkdir(parents=True, exist_ok=True)
    imagen.save(ruta, format="PNG", optimize=False, compress_level=9)
    return hashlib.sha256(ruta.read_bytes()).hexdigest()


def ruta_vista_4x_por_defecto(ruta_salida: Path) -> Path:
    """Calcula la ruta por defecto ``<stem>.x4.png`` junto a ``ruta_salida``."""
    return ruta_salida.with_name(f"{ruta_salida.stem}.x4{ruta_salida.suffix or '.png'}")


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        prog="python -m taller.asset",
        description="Convierte una imagen bruta al sprite PNG determinista del taller.",
    )
    parser.add_argument("entrada", type=Path, help="Ruta de la imagen bruta (PNG/JPG/etc.)")
    parser.add_argument(
        "--formato",
        choices=tuple(FORMATOS),
        default="cuadro_mediano",
        help="Formato de obra objetivo (por defecto: cuadro_mediano)",
    )
    parser.add_argument("--salida", type=Path, required=True, help="Ruta del PNG final")
    parser.add_argument(
        "--vista-4x",
        type=Path,
        default=None,
        help="Ruta de la vista a x4 (por defecto: <salida>.x4.png)",
    )
    parser.add_argument(
        "--sin-vista-4x",
        action="store_true",
        help="No escribir el archivo de vista previa a x4",
    )
    parser.add_argument(
        "--reduccion",
        choices=("box", "nearest"),
        default="box",
        help="Método de reducción al lienzo (por defecto: box)",
    )
    parser.add_argument(
        "--sin-marco",
        action="store_true",
        help="No añadir marco de madera estilo vanilla",
    )
    parser.add_argument(
        "--limpiar",
        dest="limpiar",
        action="store_true",
        default=None,
        help="Forzar limpieza de píxeles aislados",
    )
    parser.add_argument(
        "--sin-limpieza",
        dest="limpiar",
        action="store_false",
        help="Omitir el filtro de eliminación de píxeles aislados",
    )
    parser.add_argument(
        "--json",
        action="store_true",
        help="Imprimir los metadatos del asset en JSON por stdout",
    )
    args = parser.parse_args(argv)

    with Image.open(args.entrada) as img:
        sprite, vista_4x, meta = procesar_imagen(
            img,
            args.formato,
            reduccion=args.reduccion,
            enmarcar=not args.sin_marco,
            limpiar=args.limpiar,
        )

    meta["salida"] = str(args.salida)
    meta["sha256"] = guardar_png_determinista(sprite, args.salida)
    if not args.sin_vista_4x:
        ruta_4x = args.vista_4x or ruta_vista_4x_por_defecto(args.salida)
        meta["vista_4x"] = str(ruta_4x)
        meta["sha256_4x"] = guardar_png_determinista(vista_4x, ruta_4x)

    if args.json:
        print(json.dumps(meta, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
