using System.Text.RegularExpressions;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Tests.Simulacion;

namespace CorazonPelican.Tests.Sensorio;

/// <summary>Guarda de R-03 y de las bandas de detalle (GSS §3.3).</summary>
public class GradienteDetalleTests
{
    private static Umbrales Umbrales(double histeresis = 0)
        => new() { Periferia = 0.15, Mencion = 0.40, Foco = 0.70, Pico = 0.85, Deriva = 3.0, Histeresis = histeresis };

    private static readonly ContextoSaliencia Contexto = new(
        new Dictionary<string, double> { ["agua"] = 1.8, ["madera"] = 1.5, ["persona"] = 0.8 }, EstadoAtencion.Abierta, UmbralPeriferia: 0.15);

    [Theory]
    [InlineData(0.00, Banda.Ausente)]
    [InlineData(0.149, Banda.Ausente)]
    [InlineData(0.15, Banda.Periferia)]
    [InlineData(0.399, Banda.Periferia)]
    [InlineData(0.40, Banda.Mencion)]
    [InlineData(0.70, Banda.Foco)]
    [InlineData(0.99, Banda.Foco)]
    public void Cada_saliencia_cae_en_su_banda(double saliencia, Banda esperada)
        => Assert.Equal(esperada, ClasificadorBandas.Cruda(saliencia, Umbrales()));

    /// <summary>R-03: lo que queda bajo la periferia no llega a la escena, en un recorrido real del FakeWorld.</summary>
    [Fact]
    public void Entidades_bajo_periferia_no_llegan_a_la_escena()
    {
        // Ana se aleja deambulando; el NPC lejano y tras una pared nunca pasa de la periferia.
        var mundo = Escenario.Pasarela().ConSemilla(3).ConJugador("Ana", corazones: 2, distancia: 30, deambula: true)
            .ConNpc("Linus", "Mountain", 5, 2).Construir();
        var clasificador = new ClasificadorBandas(Umbrales());
        var novedad = new Novedad();
        var vistasAusentes = 0;

        foreach (var observacion in mundo.AvanzarObservando(TimeSpan.FromMinutes(3)))
        {
            var saliencias = Saliencia.Calcular(observacion, Contexto, novedad);
            var bandas = clasificador.Clasificar(saliencias);
            var escena = ClasificadorBandas.EnEscena(bandas);

            Assert.All(escena, e => Assert.True(e.Saliencia >= 0.15, $"{e.Ref} con {e.Saliencia} llegó a la escena."));
            var debajo = saliencias.Where(s => s.Total < 0.15).Select(s => s.Ref).ToHashSet();
            Assert.DoesNotContain(escena, e => debajo.Contains(e.Ref));
            vistasAusentes += debajo.Count;
        }

        Assert.True(vistasAusentes > 0, "El recorrido debe incluir entidades bajo la periferia para que la prueba diga algo.");
    }

    [Fact]
    public void Ausente_nunca_llega_a_la_escena_aunque_haya_histeresis()
    {
        var clasificador = new ClasificadorBandas(Umbrales(histeresis: 0.02));
        var bandas = new[] { 0.05, 0.14, 0.16, 0.5 }.Select((s, i) => new EntidadEnBanda($"e:{i}", clasificador.Clasificar($"e:{i}", s), s));

        var escena = ClasificadorBandas.EnEscena(bandas);

        Assert.Equal(new[] { "e:2", "e:3" }, escena.Select(e => e.Ref));
    }

    [Theory]
    [InlineData(0.15)]
    [InlineData(0.40)]
    [InlineData(0.70)]
    public void Con_histeresis_oscilar_un_centesimo_no_hace_parpadear_la_banda(double umbral)
    {
        var clasificador = new ClasificadorBandas(Umbrales(histeresis: 0.02));
        var bandas = Enumerable.Range(0, 40).Select(i => clasificador.Clasificar("niebla", umbral + (i % 2 == 0 ? 0.01 : -0.01))).ToList();

        Assert.Single(bandas.Distinct());
    }

    [Fact]
    public void Sin_histeresis_la_misma_oscilacion_si_parpadea()
    {
        var clasificador = new ClasificadorBandas(Umbrales());
        var bandas = Enumerable.Range(0, 4).Select(i => clasificador.Clasificar("niebla", 0.40 + (i % 2 == 0 ? 0.01 : -0.01))).ToList();

        Assert.Equal(new[] { Banda.Mencion, Banda.Periferia, Banda.Mencion, Banda.Periferia }, bandas);
    }

    [Fact]
    public void Con_histeresis_un_cambio_claro_si_cambia_la_banda()
    {
        var clasificador = new ClasificadorBandas(Umbrales(histeresis: 0.02));

        Assert.Equal(Banda.Mencion, clasificador.Clasificar("ana", 0.45));
        Assert.Equal(Banda.Foco, clasificador.Clasificar("ana", 0.75));
        Assert.Equal(Banda.Periferia, clasificador.Clasificar("ana", 0.20));
        Assert.Equal(Banda.Ausente, clasificador.Clasificar("ana", 0.05));
    }

    [Fact]
    public void Lo_que_desaparece_se_olvida_y_al_volver_empieza_de_cero()
    {
        var clasificador = new ClasificadorBandas(Umbrales(histeresis: 0.02));
        static DesgloseSaliencia D(string r, double s) => new(r, 1, 1, 1, 1, 1, 1, true, s);

        clasificador.Clasificar(new[] { D("ana", 0.41) });
        clasificador.Clasificar(Array.Empty<DesgloseSaliencia>());
        var alVolver = clasificador.Clasificar(new[] { D("ana", 0.39) }).Single();

        Assert.Equal(Banda.Periferia, alVolver.Banda);
    }

    [Fact]
    public void Las_bandas_salen_de_los_umbrales_del_perfil()
    {
        var exigente = new Umbrales { Periferia = 0.3, Mencion = 0.6, Foco = 0.9, Pico = 0.95 };

        Assert.Equal(Banda.Mencion, ClasificadorBandas.Cruda(0.5, Umbrales()));
        Assert.Equal(Banda.Periferia, ClasificadorBandas.Cruda(0.5, exigente));
    }

    [Fact]
    public void Bandas_no_codifica_ningun_umbral()
    {
        var codigo = File.ReadAllLines(Repo.Ruta("CorazonPelican.Core", "Sensorio", "Bandas.cs"))
            .Where(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal));

        Assert.DoesNotContain(codigo, l => Regex.IsMatch(l, @"\b\d+\.\d+"));
    }

    [Fact]
    public void El_validador_rechaza_una_histeresis_que_taparia_una_banda()
    {
        var perfil = Newtonsoft.Json.JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        Assert.Equal(0.02, perfil.Umbrales.Histeresis);

        perfil.Umbrales.Histeresis = 0.1;

        Assert.Contains(PerfilValidator.Validar(perfil, ModoValidacion.Desarrollo).Errores, e => e.Campo == "umbrales.histeresis");
    }
}
