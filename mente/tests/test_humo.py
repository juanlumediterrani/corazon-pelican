import mente


def test_version_contrato_coincide_con_el_motor():
    # Debe coincidir con CorazonCore.VersionContrato (ruta /api/v1/, ADS §5).
    assert mente.VERSION_CONTRATO == "v1"
