"""Pruebas unitarias de respaldo, verificación y restauración de escenarios DEV (CP-006)."""

from __future__ import annotations

from pathlib import Path

import pytest

from tools.escenarios import (
    ErrorEscenario,
    guardar_escenario,
    listar_escenarios,
    main,
    restaurar_escenario,
    verificar_escenario,
)


def _crear_save_sintetico(dir_saves: Path, nombre_carpeta: str, *, estacion: str = "fall", dia: int = 12) -> Path:
    carpeta = dir_saves / nombre_carpeta
    carpeta.mkdir(parents=True, exist_ok=True)
    xml = (
        '<?xml version="1.0" encoding="utf-8"?>\n'
        "<SaveGame>"
        "<player><name>Dev</name><farmName>PelicanDev</farmName></player>"
        f"<currentSeason>{estacion}</currentSeason>"
        f"<dayOfMonth>{dia}</dayOfMonth>"
        "<year>1</year>"
        "<uniqueIDForThisGame>999001</uniqueIDForThisGame>"
        "</SaveGame>\n"
    )
    (carpeta / nombre_carpeta).write_text(xml, encoding="utf-8")
    (carpeta / "SaveGameInfo").write_text("<Farmer><name>Dev</name></Farmer>\n", encoding="utf-8")
    return carpeta


def test_guardar_listar_verificar_y_restaurar_escenario(tmp_path: Path) -> None:
    saves = tmp_path / "saves"
    escenarios = tmp_path / "escenarios"
    _crear_save_sintetico(saves, "CorazonDev_999001", estacion="spring", dia=2)

    man = guardar_escenario(
        "base_primavera",
        "CorazonDev_999001",
        dir_saves=saves,
        dir_escenarios=escenarios,
        descripcion="Día 2 soleado en primavera",
    )
    assert man["metadatos"]["estacion"] == "spring"
    assert man["metadatos"]["dia"] == 2
    assert len(man["archivos_sha256"]) == 2

    lista = listar_escenarios(dir_escenarios=escenarios)
    assert [x["nombre"] for x in lista] == ["base_primavera"]

    assert verificar_escenario("base_primavera", dir_escenarios=escenarios)["nombre"] == "base_primavera"

    # Si ya existe el destino exige --confirmar
    with pytest.raises(ErrorEscenario, match="--confirmar"):
        restaurar_escenario("base_primavera", dir_saves=saves, dir_escenarios=escenarios)

    # Con --confirmar restaura en el mismo slot y también permite clonar a otro slot CorazonDev*
    (saves / "CorazonDev_999001" / "basura.txt").write_text("sucio", encoding="utf-8")
    restaurado = restaurar_escenario(
        "base_primavera",
        dir_saves=saves,
        dir_escenarios=escenarios,
        confirmar=True,
    )
    assert not (restaurado / "basura.txt").exists()
    assert (restaurado / "CorazonDev_999001").is_file()

    clon = restaurar_escenario(
        "base_primavera",
        dir_saves=saves,
        dir_escenarios=escenarios,
        destino="CorazonDev_Otono",
        confirmar=False,
    )
    assert (clon / "CorazonDev_Otono").is_file()
    assert not (clon / "CorazonDev_999001").exists()


def test_proteccion_contra_sobrescritura_de_campana_y_deteccion_de_corrupcion(tmp_path: Path) -> None:
    saves = tmp_path / "saves"
    escenarios = tmp_path / "escenarios"
    _crear_save_sintetico(saves, "CorazonDev_999001", estacion="fall", dia=14)
    guardar_escenario("otono_pasarela", "CorazonDev_999001", dir_saves=saves, dir_escenarios=escenarios)

    # Rechaza restaurar sobre cualquier carpeta que no comience por CorazonDev
    with pytest.raises(ErrorEscenario, match="CorazonDev"):
        restaurar_escenario(
            "otono_pasarela",
            dir_saves=saves,
            dir_escenarios=escenarios,
            destino="MiCampanaReal_123",
            confirmar=True,
        )

    # Detección de corrupción SHA-256 antes de restaurar
    ruta_xml = escenarios / "otono_pasarela" / "save" / "CorazonDev_999001"
    ruta_xml.write_text(ruta_xml.read_text(encoding="utf-8") + " ", encoding="utf-8")
    with pytest.raises(ErrorEscenario, match="SHA-256"):
        verificar_escenario("otono_pasarela", dir_escenarios=escenarios)
    with pytest.raises(ErrorEscenario, match="SHA-256"):
        restaurar_escenario(
            "otono_pasarela",
            dir_saves=saves,
            dir_escenarios=escenarios,
            destino="CorazonDev_Otro",
        )


def test_cli_devuelve_codigo_cero_y_errores_limpios(tmp_path: Path, capsys: pytest.CaptureFixture[str]) -> None:
    saves = tmp_path / "saves"
    escenarios = tmp_path / "escenarios"
    _crear_save_sintetico(saves, "CorazonDev_1")

    rc = main(
        [
            "--dir-saves",
            str(saves),
            "--dir-escenarios",
            str(escenarios),
            "guardar",
            "esc1",
            "--partida",
            "CorazonDev_1",
        ]
    )
    assert rc == 0
    rc_list = main(["--dir-escenarios", str(escenarios), "listar"])
    assert rc_list == 0
    assert "esc1" in capsys.readouterr().out
