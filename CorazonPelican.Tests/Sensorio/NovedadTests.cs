using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Tests.Sensorio;

public class NovedadTests
{
    [Fact]
    public void Lo_nunca_visto_es_novedad_completa()
        => Assert.Equal(1.0, new Novedad().Factor("niebla"));

    [Fact]
    public void Exponer_n_veces_reduce_la_novedad_de_forma_monotona_hasta_el_piso()
    {
        var novedad = new Novedad(piso: 0.3);
        var anterior = novedad.Factor("lago");
        for (var i = 0; i < 200; i++)
        {
            novedad.Exponer(new[] { "lago" }, minutosJuego: 1);
            var actual = novedad.Factor("lago");
            Assert.True(actual < anterior || actual == novedad.Piso || Math.Abs(actual - anterior) < 1e-12, $"Paso {i}: {anterior} → {actual}.");
            Assert.True(actual >= novedad.Piso);
            anterior = actual;
        }

        Assert.True(anterior < 0.35, "Tras 200 minutos mirando el lago, su novedad está cerca del piso.");
    }

    [Fact]
    public void La_novedad_se_recupera_con_la_ausencia_y_no_supera_uno()
    {
        var novedad = new Novedad(semividaRecuperacionMin: 720);
        novedad.Exponer(new[] { "lago" }, minutosJuego: 120);
        var habituado = novedad.Factor("lago");

        novedad.Exponer(new[] { "otra_cosa" }, minutosJuego: 720);
        var tras12Horas = novedad.Factor("lago");
        novedad.Exponer(Array.Empty<string>(), minutosJuego: 10_000);

        Assert.True(tras12Horas > habituado);
        Assert.InRange(1 - tras12Horas, (1 - habituado) / 2 - 1e-9, (1 - habituado) / 2 + 1e-9);
        Assert.InRange(novedad.Factor("lago"), 0.99, 1.0);
    }

    [Fact]
    public void Solo_se_habitua_a_lo_que_percibe()
    {
        var novedad = new Novedad();

        novedad.Exponer(new[] { "lago" }, minutosJuego: 60);

        Assert.True(novedad.Factor("lago") < 1);
        Assert.Equal(1.0, novedad.Factor("niebla"));
    }

    [Fact]
    public void La_habituacion_no_depende_de_como_se_trocee_el_tiempo()
    {
        var deGolpe = new Novedad();
        var aTrozos = new Novedad();

        deGolpe.Exponer(new[] { "lago" }, 30);
        for (var i = 0; i < 300; i++)
        {
            aTrozos.Exponer(new[] { "lago" }, 0.1);
        }

        Assert.Equal(deGolpe.Factor("lago"), aTrozos.Factor("lago"), precision: 9);
    }
}
