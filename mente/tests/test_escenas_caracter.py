import json
from pathlib import Path


CASOS = Path(__file__).parent / "escenas_caracter" / "leah.json"


def test_la_pareja_de_lluvia_solo_varia_el_recuerdo_que_recibe_la_mente():
    casos = {caso["id"]: caso for caso in json.loads(CASOS.read_text(encoding="utf-8"))}
    tranquila = dict(casos["lluvia-tranquilidad"]["entrada"])
    aislada = dict(casos["lluvia-aislamiento"]["entrada"])

    assert tranquila.pop("recuerdos") != aislada.pop("recuerdos")
    assert tranquila == aislada
