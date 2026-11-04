import json
from pathlib import Path

import pytest

from mente import contrato

EJEMPLOS = contrato.DIR_ESQUEMAS / "ejemplos"


def _ejemplos(carpeta: str) -> list[Path]:
    return sorted((EJEMPLOS / carpeta).glob("*.json"))


def _esquema_de(ruta: Path) -> str:
    # Convención: <esquema>--<caso>.json
    return ruta.name.split("--", 1)[0]


def _cargar(ruta: Path):
    return json.loads(ruta.read_text(encoding="utf-8"))


@pytest.mark.parametrize("nombre", contrato.ESQUEMAS)
def test_los_esquemas_son_draft_2020_12_validos(nombre):
    contrato.Draft202012Validator.check_schema(contrato.cargar_esquema(nombre))


@pytest.mark.parametrize("ruta", _ejemplos("validos"), ids=lambda p: p.name)
def test_ejemplos_validos_validan(ruta):
    assert contrato.errores(_esquema_de(ruta), _cargar(ruta)) == []


@pytest.mark.parametrize("ruta", _ejemplos("invalidos"), ids=lambda p: p.name)
def test_ejemplos_invalidos_no_validan(ruta):
    assert contrato.errores(_esquema_de(ruta), _cargar(ruta)) != []


def test_hay_ejemplo_valido_de_cada_esquema_con_raiz():
    con_ejemplo = {_esquema_de(p) for p in _ejemplos("validos")}
    assert con_ejemplo == set(contrato.ESQUEMAS) - {"comun"}


def test_pensamiento_es_la_primera_propiedad_de_la_respuesta():
    propiedades = list(contrato.cargar_esquema("respuesta")["properties"])
    assert propiedades[0] == "pensamiento"


class TestEsquemaIntencion:
    """R-13: la mente no elige casillas ni rutas."""

    @pytest.mark.parametrize(
        "extra",
        [{"x": 12, "y": 30}, {"tile": [12, 30]}, {"casilla": "12,30"}, {"ruta": ["Town", "Mountain"]}],
        ids=["x-y", "tile", "casilla", "ruta"],
    )
    def test_plan_con_coordenadas_es_invalido(self, extra):
        respuesta = _cargar(EJEMPLOS / "validos" / "respuesta--promesa.json")
        respuesta["intencion"]["plan"] = [{"accion": "ir_a", "lugar": "pasarela_lago", **extra}]
        assert not contrato.es_valido("respuesta", respuesta)

    def test_sustituir_un_hito_con_coordenadas_es_invalido(self):
        respuesta = _cargar(EJEMPLOS / "validos" / "respuesta--despertar.json")
        respuesta["intencion"]["cambios_rutina"][0]["plan"][0]["x"] = 3
        assert not contrato.es_valido("respuesta", respuesta)


def test_campos_obligatorios_coinciden_con_la_instantanea():
    instantanea = json.loads((EJEMPLOS / "obligatorios.json").read_text(encoding="utf-8"))
    instantanea.pop("_nota")
    reales = {nombre: contrato.cargar_esquema(nombre)["required"] for nombre in contrato.ESQUEMAS if nombre != "comun"}
    assert reales == instantanea


def test_los_perfiles_del_mod_cumplen_el_esquema():
    carpeta = contrato.DIR_ESQUEMAS.parent / "CorazonPelican" / "assets" / "personajes"
    perfiles = [p for p in carpeta.glob("*.json") if p.name.count(".") == 1]
    assert perfiles
    for ruta in perfiles:
        assert contrato.errores("perfil", _cargar(ruta)) == [], ruta.name
