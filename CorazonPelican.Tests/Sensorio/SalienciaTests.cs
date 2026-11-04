using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;

namespace CorazonPelican.Tests.Sensorio;

public class SalienciaTests
{
    private static readonly Dictionary<string, double> RasgosArtista = new() { ["luz"] = 2.0, ["agua"] = 1.8, ["madera"] = 1.5, ["persona"] = 0.8, ["maquinaria"] = 0.3 };

    private static readonly ContextoSaliencia Contexto = new(RasgosArtista, EstadoAtencion.Abierta, UmbralPeriferia: 0.15);

    private static EntidadObservada Entidad(string etiqueta, int y, int? corazones = null, bool visible = true, CategoriaEntidad categoria = CategoriaEntidad.Objeto)
        => new($"prueba:{etiqueta}:{y}", categoria, etiqueta, "Mountain", 0, y, new[] { etiqueta }, corazones, visible);

    private static double S(EntidadObservada e, ContextoSaliencia? contexto = null, double novedad = 1.0)
        => Saliencia.Calcular(e, 0, 0, contexto ?? Contexto, novedad).Total;

    public static TheoryData<string, int?> Entidades() => new()
    {
        { "agua", null }, { "madera", null }, { "maquinaria", null }, { "persona", 0 }, { "persona", 9 }, { "sin_rasgo", null },
    };

    [Theory]
    [MemberData(nameof(Entidades))]
    public void Mas_distancia_nunca_sube_la_saliencia(string etiqueta, int? corazones)
    {
        foreach (var estado in new[] { EstadoAtencion.Abierta, EstadoAtencion.Estrecha, new EstadoAtencion(1, 0) })
        {
            foreach (var novedad in new[] { 1.0, 0.5, 0.3 })
            {
                var contexto = Contexto with { Estado = estado };
                var anterior = double.MaxValue;
                for (var distancia = 0; distancia <= 60; distancia++)
                {
                    var actual = S(Entidad(etiqueta, distancia, corazones), contexto, novedad);
                    Assert.True(actual <= anterior, $"{etiqueta} sube de {anterior} a {actual} al pasar a {distancia} casillas.");
                    anterior = actual;
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Entidades))]
    public void Sin_vision_una_entidad_fisica_no_pasa_de_la_periferia(string etiqueta, int? corazones)
    {
        var intensa = Contexto with { PesosIntencion = new Dictionary<string, double> { [etiqueta] = 10 } };
        for (var distancia = 0; distancia <= 20; distancia++)
        {
            Assert.True(S(Entidad(etiqueta, distancia, corazones, visible: false), intensa) <= intensa.UmbralPeriferia);
        }

        Assert.True(S(Entidad(etiqueta, 0, corazones, visible: true), intensa) > intensa.UmbralPeriferia, "Con visión sí puede pasar de la periferia.");
    }

    [Fact]
    public void Con_los_mismos_rasgos_y_estado_nueve_corazones_superan_a_uno_a_igual_distancia()
    {
        foreach (var distancia in new[] { 0, 3, 9, 20, 40 })
        {
            var nueve = S(Entidad("persona", distancia, 9, categoria: CategoriaEntidad.Granjero));
            var uno = S(Entidad("persona", distancia, 1, categoria: CategoriaEntidad.Granjero));
            Assert.True(nueve > uno, $"A {distancia} casillas: 9 corazones {nueve} ≤ 1 corazón {uno}.");
        }
    }

    [Fact]
    public void Con_muchos_corazones_lo_nota_llegar_de_lejos()
    {
        // A 20 casillas, alguien con 10 corazones pesa más que alguien sin relación a 12.
        Assert.True(S(Entidad("persona", 20, 10)) > S(Entidad("persona", 12, 0)));
    }

    [Fact]
    public void Los_rasgos_del_perfil_ordenan_lo_que_pesa()
    {
        Assert.True(S(Entidad("agua", 5)) > S(Entidad("persona", 5, 0)));
        Assert.True(S(Entidad("persona", 5, 0)) > S(Entidad("maquinaria", 5)));
    }

    [Fact]
    public void Atencion_estrecha_y_cansancio_bajan_la_saliencia()
    {
        var agua = Entidad("agua", 4);

        Assert.True(S(agua, Contexto with { Estado = EstadoAtencion.Estrecha }) < S(agua));
        Assert.True(S(agua, Contexto with { Estado = new EstadoAtencion(1, 0) }) < S(agua));
    }

    [Fact]
    public void La_intencion_da_peso_a_lo_que_busca_y_sin_intencion_vale_uno()
    {
        var madera = Entidad("madera", 4);
        var buscandoMadera = Contexto with { PesosIntencion = new Dictionary<string, double> { ["madera"] = 2.0 } };

        Assert.True(S(madera, buscandoMadera) > S(madera));
        Assert.Equal(1.0, Saliencia.Calcular(madera, 0, 0, Contexto, 1.0).Intencion);
    }

    [Fact]
    public void La_saliencia_esta_en_cero_uno_y_el_desglose_la_explica()
    {
        var desglose = Saliencia.Calcular(Entidad("agua", 0), 0, 0, Contexto with { PesosIntencion = new Dictionary<string, double> { ["agua"] = 100 } }, 1.0);

        Assert.InRange(desglose.Total, 0, 0.9999999);
        Assert.Equal(1.8, desglose.Rasgos);
        Assert.Equal(100, desglose.Intencion);
        Assert.Equal(1.0, desglose.Distancia);
    }

    [Fact]
    public void En_el_mundo_simulado_el_lago_pierde_novedad_mientras_lo_mira()
    {
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9).Construir();
        var novedad = new Novedad();
        var minutosPorPaso = FakeWorld.PasoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(mundo.Reloj.Modo);
        double LagoAhora() => Saliencia.Calcular(mundo.Observar(), Contexto, novedad).Single(d => d.Ref == "agua:Mountain:lago").Total;

        var alPrincipio = LagoAhora();
        foreach (var observacion in mundo.AvanzarObservando(TimeSpan.FromSeconds(7 * 6)))
        {
            novedad.Exponer(observacion.Entidades.Select(e => e.Ref), minutosPorPaso);
        }

        Assert.True(LagoAhora() < alPrincipio, "Tras una hora de juego mirándolo, el lago pesa menos.");
    }
}
