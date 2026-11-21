"""Gestor de escenarios y respaldos para la partida DEV de Corazón Pelican (CP-006)."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
import xml.etree.ElementTree as ET
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PREFIJO_DEV = "CorazonDev"
_NOMBRE_VALIDO = re.compile(r"^[A-Za-z0-9_-]+$")


class ErrorEscenario(RuntimeError):
    """Error operativo o de validación al gestionar escenarios DEV."""


def dir_saves_por_defecto() -> Path:
    crudo = os.environ.get("CORAZON_PELICAN_SAVES")
    if crudo:
        return Path(crudo).expanduser()
    return Path.home() / ".config" / "StardewValley" / "Saves"


def dir_escenarios_por_defecto() -> Path:
    crudo = os.environ.get("CORAZON_PELICAN_ESCENARIOS")
    if crudo:
        return Path(crudo).expanduser()
    return Path.home() / ".local" / "share" / "corazon-pelican" / "escenarios"


def _validar_nombre_segmento(nombre: str, etiqueta: str) -> str:
    limpio = (nombre or "").strip()
    if not limpio or not _NOMBRE_VALIDO.match(limpio):
        raise ErrorEscenario(
            f"{etiqueta} inválido {nombre!r}: usa solo letras, dígitos, guiones y guiones bajos."
        )
    return limpio


def _sha256_archivo(ruta: Path) -> str:
    h = hashlib.sha256()
    with ruta.open("rb") as f:
        while True:
            bloque = f.read(65536)
            if not bloque:
                break
            h.update(bloque)
    return h.hexdigest()


def _hashes_carpeta(carpeta: Path) -> dict[str, str]:
    salida: dict[str, str] = {}
    for archivo in sorted(p for p in carpeta.rglob("*") if p.is_file()):
        rel = archivo.relative_to(carpeta).as_posix()
        salida[rel] = _sha256_archivo(archivo)
    return salida


def _leer_metadatos_save(ruta_xml: Path) -> dict[str, Any]:
    meta: dict[str, Any] = {
        "nombre_granjero": None,
        "nombre_granja": None,
        "estacion": None,
        "dia": None,
        "anio": None,
        "unique_id_juego": None,
    }
    try:
        arbol = ET.parse(ruta_xml)
        raiz = arbol.getroot()
    except (ET.ParseError, OSError):
        return meta

    meta["estacion"] = raiz.findtext("currentSeason")
    dia_txt = raiz.findtext("dayOfMonth")
    anio_txt = raiz.findtext("year")
    uid_txt = raiz.findtext("uniqueIDForThisGame")
    if dia_txt and dia_txt.isdigit():
        meta["dia"] = int(dia_txt)
    if anio_txt and anio_txt.isdigit():
        meta["anio"] = int(anio_txt)
    if uid_txt:
        meta["unique_id_juego"] = uid_txt

    jugador = raiz.find("player")
    if jugador is not None:
        meta["nombre_granjero"] = jugador.findtext("name")
        meta["nombre_granja"] = jugador.findtext("farmName")
    return meta


def guardar_escenario(
    nombre: str,
    partida_origen: str,
    *,
    dir_saves: Path | None = None,
    dir_escenarios: Path | None = None,
    descripcion: str = "",
    sobrescribir: bool = False,
) -> dict[str, Any]:
    """Guarda una copia íntegra con SHA-256 de una partida de Stardew Valley como escenario DEV."""
    nombre_ok = _validar_nombre_segmento(nombre, "Nombre de escenario")
    origen_ok = _validar_nombre_segmento(partida_origen, "Nombre de partida origen")

    saves = (dir_saves or dir_saves_por_defecto()).expanduser()
    escenarios = (dir_escenarios or dir_escenarios_por_defecto()).expanduser()

    ruta_origen = saves / origen_ok
    if not ruta_origen.is_dir():
        raise ErrorEscenario(f"No existe la partida origen en disco: {ruta_origen}")

    xml_principal = ruta_origen / origen_ok
    save_info = ruta_origen / "SaveGameInfo"
    if not xml_principal.is_file() or not save_info.is_file():
        raise ErrorEscenario(
            f"La carpeta '{origen_ok}' no contiene los ficheros de guardado esperados ({origen_ok} y SaveGameInfo)."
        )

    destino_escenario = escenarios / nombre_ok
    if destino_escenario.exists() and not sobrescribir:
        raise ErrorEscenario(
            f"El escenario '{nombre_ok}' ya existe en {destino_escenario}. Usa --sobrescribir para reemplazarlo."
        )

    escenarios.mkdir(parents=True, exist_ok=True)
    tmp_dir = Path(tempfile.mkdtemp(prefix=f".tmp-esc-{nombre_ok}-", dir=escenarios))
    try:
        carpeta_save = tmp_dir / "save"
        shutil.copytree(ruta_origen, carpeta_save)
        hashes = _hashes_carpeta(carpeta_save)
        metadatos = _leer_metadatos_save(carpeta_save / origen_ok)
        manifiesto: dict[str, Any] = {
            "version": 1,
            "nombre": nombre_ok,
            "partida_origen": origen_ok,
            "descripcion": descripcion.strip(),
            "guardado_en_utc": datetime.now(timezone.utc).isoformat(),
            "metadatos": metadatos,
            "archivos_sha256": hashes,
        }
        (tmp_dir / "escenario.json").write_text(
            json.dumps(manifiesto, ensure_ascii=False, indent=2) + "\n",
            encoding="utf-8",
        )
        if destino_escenario.exists():
            shutil.rmtree(destino_escenario)
        tmp_dir.replace(destino_escenario)
    except Exception:
        shutil.rmtree(tmp_dir, ignore_errors=True)
        raise

    return manifiesto


def _cargar_manifiesto(escenario_dir: Path) -> dict[str, Any]:
    ruta_json = escenario_dir / "escenario.json"
    if not ruta_json.is_file():
        raise ErrorEscenario(f"Falta 'escenario.json' en {escenario_dir}.")
    try:
        datos = json.loads(ruta_json.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ErrorEscenario(f"No se pudo leer '{ruta_json}': {exc}") from exc
    if not isinstance(datos, dict):
        raise ErrorEscenario(f"'{ruta_json}' debe contener un objeto JSON.")
    return datos


def verificar_escenario(nombre: str, *, dir_escenarios: Path | None = None) -> dict[str, Any]:
    """Comprueba existencia e integridad SHA-256 de todos los ficheros de un escenario."""
    nombre_ok = _validar_nombre_segmento(nombre, "Nombre de escenario")
    escenarios = (dir_escenarios or dir_escenarios_por_defecto()).expanduser()
    escenario_dir = escenarios / nombre_ok
    if not escenario_dir.is_dir():
        raise ErrorEscenario(f"No existe el escenario '{nombre_ok}' en {escenarios}.")

    manifiesto = _cargar_manifiesto(escenario_dir)
    carpeta_save = escenario_dir / "save"
    if not carpeta_save.is_dir():
        raise ErrorEscenario(f"Falta la carpeta 'save/' en el escenario '{nombre_ok}'.")

    esperados = manifiesto.get("archivos_sha256")
    if not isinstance(esperados, dict) or not esperados:
        raise ErrorEscenario(f"El escenario '{nombre_ok}' no declara archivos_sha256.")

    actuales = _hashes_carpeta(carpeta_save)
    if set(actuales.keys()) != set(esperados.keys()):
        faltan = sorted(set(esperados.keys()) - set(actuales.keys()))
        sobran = sorted(set(actuales.keys()) - set(esperados.keys()))
        raise ErrorEscenario(
            f"Integridad rota en '{nombre_ok}': faltan={faltan}, adicionales={sobran}."
        )

    distintos = [k for k, v in esperados.items() if actuales.get(k) != v]
    if distintos:
        raise ErrorEscenario(
            f"Integridad SHA-256 fallida en '{nombre_ok}' para: {', '.join(distintos)}."
        )

    return manifiesto


def listar_escenarios(*, dir_escenarios: Path | None = None) -> list[dict[str, Any]]:
    """Lista los escenarios guardados ordenados alfabéticamente por nombre."""
    escenarios = (dir_escenarios or dir_escenarios_por_defecto()).expanduser()
    if not escenarios.is_dir():
        return []

    salida: list[dict[str, Any]] = []
    for sub in sorted(p for p in escenarios.iterdir() if p.is_dir()):
        ruta_json = sub / "escenario.json"
        if ruta_json.is_file():
            salida.append(_cargar_manifiesto(sub))
    return salida


def restaurar_escenario(
    nombre: str,
    *,
    dir_saves: Path | None = None,
    dir_escenarios: Path | None = None,
    destino: str | None = None,
    confirmar: bool = False,
) -> Path:
    """
    Restaura un escenario DEV en el directorio de guardados.
    Solo permite escribir en slots cuyo nombre comience por 'CorazonDev' para no tocar campañas.
    """
    manifiesto = verificar_escenario(nombre, dir_escenarios=dir_escenarios)
    nombre_ok = str(manifiesto["nombre"])
    partida_origen = str(manifiesto["partida_origen"])

    if destino is None:
        destino_ok = partida_origen if partida_origen.startswith(PREFIJO_DEV) else f"{PREFIJO_DEV}_{nombre_ok}"
    else:
        destino_ok = _validar_nombre_segmento(destino, "Carpeta destino")

    if not destino_ok.startswith(PREFIJO_DEV):
        raise ErrorEscenario(
            f"Destino rechazado ('{destino_ok}'): por seguridad anti-campaña, solo se puede restaurar en carpetas que empiecen por '{PREFIJO_DEV}'."
        )

    saves = (dir_saves or dir_saves_por_defecto()).expanduser()
    escenarios = (dir_escenarios or dir_escenarios_por_defecto()).expanduser()
    ruta_destino = saves / destino_ok

    if ruta_destino.exists() and not confirmar:
        raise ErrorEscenario(
            f"La partida DEV destino '{destino_ok}' ya existe en {saves}. Pasa --confirmar para sobrescribirla."
        )

    saves.mkdir(parents=True, exist_ok=True)
    tmp_dir = Path(tempfile.mkdtemp(prefix=f".tmp-restore-{destino_ok}-", dir=saves))
    try:
        carpeta_copia = tmp_dir / "slot"
        shutil.copytree(escenarios / nombre_ok / "save", carpeta_copia)
        if destino_ok != partida_origen:
            xml_orig = carpeta_copia / partida_origen
            if xml_orig.is_file():
                xml_orig.replace(carpeta_copia / destino_ok)
            xml_orig_old = carpeta_copia / f"{partida_origen}_old"
            if xml_orig_old.is_file():
                xml_orig_old.replace(carpeta_copia / f"{destino_ok}_old")

        if ruta_destino.exists():
            shutil.rmtree(ruta_destino)
        carpeta_copia.replace(ruta_destino)
    finally:
        shutil.rmtree(tmp_dir, ignore_errors=True)

    return ruta_destino


def _construir_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Gestor de escenarios para la partida DEV de Corazón Pelican."
    )
    parser.add_argument(
        "--dir-saves",
        type=Path,
        default=None,
        help="Directorio de guardados de Stardew Valley (por defecto ~/.config/StardewValley/Saves).",
    )
    parser.add_argument(
        "--dir-escenarios",
        type=Path,
        default=None,
        help="Directorio de escenarios (por defecto ~/.local/share/corazon-pelican/escenarios).",
    )

    sub = parser.add_subparsers(dest="subcomando", required=True)

    p_guardar = sub.add_parser("guardar", help="Guarda una partida DEV como escenario con nombre.")
    p_guardar.add_argument("nombre", help="Identificador del escenario (letras, dígitos, -, _).")
    p_guardar.add_argument("--partida", required=True, help="Nombre de la carpeta de guardado origen.")
    p_guardar.add_argument("--descripcion", default="", help="Nota breve sobre qué prepara el escenario.")
    p_guardar.add_argument("--sobrescribir", action="store_true", help="Reemplaza un escenario existente.")

    sub.add_parser("listar", help="Lista los escenarios guardados.")

    p_verif = sub.add_parser("verificar", help="Verifica los hashes SHA-256 de un escenario.")
    p_verif.add_argument("nombre", help="Nombre del escenario.")

    p_rest = sub.add_parser("restaurar", help="Restaura un escenario en una carpeta CorazonDev*.")
    p_rest.add_argument("nombre", help="Nombre del escenario.")
    p_rest.add_argument(
        "--destino",
        default=None,
        help=f"Carpeta destino en Saves (debe empezar por {PREFIJO_DEV}).",
    )
    p_rest.add_argument(
        "--confirmar",
        action="store_true",
        help="Obligatorio si la carpeta destino ya existe.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    parser = _construir_parser()
    args = parser.parse_args(argv)
    try:
        if args.subcomando == "guardar":
            man = guardar_escenario(
                args.nombre,
                args.partida,
                dir_saves=args.dir_saves,
                dir_escenarios=args.dir_escenarios,
                descripcion=args.descripcion,
                sobrescribir=args.sobrescribir,
            )
            print(json.dumps(man, ensure_ascii=False, indent=2))
            return 0

        if args.subcomando == "listar":
            lista = listar_escenarios(dir_escenarios=args.dir_escenarios)
            if not lista:
                print("No hay escenarios guardados.")
                return 0
            for item in lista:
                meta = item.get("metadatos") or {}
                fecha = (
                    f"{meta.get('estacion') or '?'} día {meta.get('dia') or '?'}, año {meta.get('anio') or '?'}"
                )
                print(
                    f"- {item['nombre']} | origen={item['partida_origen']} | {fecha} | {item.get('descripcion', '')}"
                )
            return 0

        if args.subcomando == "verificar":
            man = verificar_escenario(args.nombre, dir_escenarios=args.dir_escenarios)
            print(
                f"OK: escenario '{man['nombre']}' ({len(man['archivos_sha256'])} archivos, origen={man['partida_origen']})."
            )
            return 0

        if args.subcomando == "restaurar":
            ruta = restaurar_escenario(
                args.nombre,
                dir_saves=args.dir_saves,
                dir_escenarios=args.dir_escenarios,
                destino=args.destino,
                confirmar=args.confirmar,
            )
            print(f"OK: escenario '{args.nombre}' restaurado en {ruta}")
            return 0
    except ErrorEscenario as exc:
        print(f"Error: {exc}", file=sys.stderr)
        return 1
    return 1


if __name__ == "__main__":
    raise SystemExit(main())
