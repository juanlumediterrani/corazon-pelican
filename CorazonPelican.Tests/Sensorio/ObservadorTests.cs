using System.Text.Json;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;

namespace CorazonPelican.Tests.Sensorio;

public class ObservadorTests
{
    private static readonly JsonSerializerOptions JsonOpciones = new() { WriteIndented = false };

    private static string InstantaneaMundo(FakeWorld mundo, Observacion observacion)
        => JsonSerializer.Serialize(new
        {
            Dia = mundo.Reloj.Juego.Dia,
            Hora = mundo.Reloj.Juego.Hora,
            MsReales = mundo.Reloj.MilisegundosReales,
            PersonajeUbicacion = mundo.Personaje.Ubicacion,
            PersonajeX = mundo.Personaje.X,
            PersonajeY = mundo.Personaje.Y,
            mundo.Clima,
            mundo.Cuerpo.Ocupado,
            mundo.Cuerpo.Sentada,
            mundo.Cuerpo.Continua,
            SucesosCuerpo = mundo.Cuerpo.Sucesos,
            Respuestas = mundo.RespuestasEntregadas,
            Observacion = observacion,
        }, JsonOpciones);

    [Fact]
    public void Observar_no_muta_ningun_campo_ni_entidad_del_mundo()
    {
        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 9, deambula: true)
            .ConNpc("Linus", "Mountain", 35, 25, deambula: true)
            .Construir();

        var primera = mundo.Observar();
        var estadoAntes = InstantaneaMundo(mundo, primera);

        var segunda = mundo.Observar();
        var tercera = mundo.Observar();
        var estadoDespues = InstantaneaMundo(mundo, tercera);

        Assert.Equal(estadoAntes, estadoDespues);
        Assert.Equal(JsonSerializer.Serialize(primera, JsonOpciones), JsonSerializer.Serialize(segunda, JsonOpciones));
    }

    [Fact]
    public void Identidades_estables_entre_observaciones_consecutivas_y_tras_guardar_y_cargar()
    {
        var escenario = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 9)
            .ConNpc("Linus", "Mountain", 35, 25, deambula: false)
            .ALas(dia: 68, hora: 1730);

        var antesDeGuardar = escenario.Construir();
        var obs1 = antesDeGuardar.Observar();
        var obs2 = antesDeGuardar.Observar();

        Assert.Equal(
            obs1.Entidades.Select(e => e.Ref),
            obs2.Entidades.Select(e => e.Ref));

        // Simula guardar y recargar reconstruyendo el mundo con el mismo estado persistido.
        var trasRecargar = escenario.Construir();
        var obsRecargada = trasRecargar.Observar();

        Assert.Equal(
            obs1.Entidades.Select(e => e.Ref),
            obsRecargada.Entidades.Select(e => e.Ref));

        var petId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        Assert.Equal("farmer:123", IdentidadesObservacion.Granjero(123));
        Assert.Equal("npc:Linus", IdentidadesObservacion.Npc("Linus"));
        Assert.Equal("animal:42", IdentidadesObservacion.Animal(42));
        Assert.Equal($"mascota:{petId:D}", IdentidadesObservacion.Mascota(petId));
        Assert.Equal("obj:Mountain:40,30:(O)128", IdentidadesObservacion.Objeto("Mountain", 40, 30, "(O)128"));
        Assert.Equal("mueble:LeahHouse:3,7:(F)1228", IdentidadesObservacion.Mueble("LeahHouse", 3, 7, "(F)1228"));
        Assert.Equal("agua:Mountain:40,34", IdentidadesObservacion.Agua("Mountain", 40, 34));
        Assert.Equal("suceso:regalo:68:123:1", IdentidadesObservacion.Suceso("regalo", "68", "123", "1"));
    }

    [Fact]
    public void Linea_de_vision_marca_visible_false_con_obstaculo_y_true_sin_el()
    {
        var rejilla = new bool[12, 12];
        // Muro vertical en x = 5, de y = 2 a y = 8
        for (var y = 2; y <= 8; y++)
        {
            rejilla[5, y] = true;
        }

        // Con obstáculo en medio (2,5) -> (8,5)
        var visibleConMuro = LineaVision.HayLineaVision(2, 5, 8, 5, rejilla);
        // En diagonal cruzando el muro (2,3) -> (8,7)
        var visibleDiagonalBloqueada = LineaVision.HayLineaVision(2, 3, 8, 7, rejilla);
        // Rodeando el muro (2,1) -> (8,1)
        var visibleSinMuro = LineaVision.HayLineaVision(2, 1, 8, 1, rejilla);
        // El propio muro en (5,5) sí es visible desde (2,5) porque no hay obstáculos intermedios
        var muroEsVisible = LineaVision.HayLineaVision(2, 5, 5, 5, rejilla);

        Assert.False(visibleConMuro);
        Assert.False(visibleDiagonalBloqueada);
        Assert.True(visibleSinMuro);
        Assert.True(muroEsVisible);
        Assert.True(LineaVision.HayLineaVision(2, 5, 2, 5, rejilla));

        // Integración con EntidadObservada y Saliencia: sin visión queda acotada a periferia
        var contexto = new ContextoSaliencia(
            new Dictionary<string, double> { ["persona"] = 5.0 },
            EstadoAtencion.Abierta,
            UmbralPeriferia: 0.15);

        var bloqueada = new EntidadObservada(
            IdentidadesObservacion.Granjero(1),
            CategoriaEntidad.Granjero,
            "Ana",
            "Mountain",
            8,
            5,
            new[] { "persona" },
            Corazones: 8,
            Visible: visibleConMuro,
            Distancia: EntidadObservada.CalcularDistancia(2, 5, 8, 5));

        var despejada = bloqueada with { Y = 1, Visible = visibleSinMuro, Distancia = EntidadObservada.CalcularDistancia(2, 1, 8, 1) };

        Assert.False(bloqueada.Visible);
        Assert.True(despejada.Visible);
        Assert.Equal(6.0, bloqueada.Distancia);
        Assert.True(Saliencia.Calcular(bloqueada, 2, 5, contexto, 1.0).Total <= contexto.UmbralPeriferia);
        Assert.True(Saliencia.Calcular(despejada, 2, 1, contexto, 1.0).Total > contexto.UmbralPeriferia);
    }

    [Fact]
    public void Luz_ambiental_y_sucesos_locales_son_deterministas()
    {
        Assert.Equal(1.0, LuzAmbiental.Calcular(1200, "soleado", esExterior: true));
        Assert.Equal(0.6, LuzAmbiental.Calcular(1900, "soleado", esExterior: true), precision: 3);
        Assert.Equal(LuzAmbiental.LuzNoche, LuzAmbiental.Calcular(2100, "soleado", esExterior: true));
        Assert.True(LuzAmbiental.Calcular(1200, "tormenta", esExterior: true) < LuzAmbiental.Calcular(1200, "soleado", esExterior: true));
        Assert.Equal(LuzAmbiental.LuzInterior, LuzAmbiental.Calcular(2200, "tormenta", esExterior: false));

        var suceso = new SucesoObservado(
            IdentidadesObservacion.Suceso("regalo", "68", "1", "1"),
            "regalo",
            "regalo recibido de Ana",
            ActorRef: IdentidadesObservacion.Granjero(1));
        var obs = new Observacion(68, 1730, "Mountain", 40, 30, 0.8, "soleado", Array.Empty<EntidadObservada>(), new[] { suceso });

        Assert.Single(obs.Sucesos!);
        Assert.Equal("suceso:regalo:68:1:1", obs.Sucesos![0].Ref);
    }

    [Fact]
    public void Medidor_coste_bucle_registra_medias_maximos_y_reinicio()
    {
        var medidor = new MedidorCosteBucle();

        medidor.Registrar("observar", 0.4);
        medidor.Registrar("observar", 0.8);
        medidor.Registrar("observar", 0.3);
        medidor.Registrar("saliencia", 0.1);
        using (medidor.Medir("medido"))
        {
            // Alcance vacío para cubrir la ruta sin asignaciones de Stopwatch.
        }

        var observar = medidor.Obtener("observar");
        Assert.NotNull(observar);
        Assert.Equal(3, observar!.Muestras);
        Assert.Equal(0.8, observar.MaximoMs, precision: 6);
        Assert.Equal(0.3, observar.MinimoMs, precision: 6);
        Assert.Equal(0.5, observar.MedioMs, precision: 6);

        var medido = medidor.Obtener("medido");
        Assert.NotNull(medido);
        Assert.Equal(1, medido!.Muestras);
        Assert.True(medido.MaximoMs >= 0.0 && medido.MedioMs >= 0.0);

        var resumen = medidor.Resumen();
        Assert.Equal(new[] { "observar", "saliencia", "medido" }, resumen.Select(r => r.Paso));
        Assert.Null(medidor.Obtener("inexistente"));

        medidor.Reiniciar();
        Assert.Empty(medidor.Resumen());
    }

    [Fact]
    public void Identidades_de_zona_en_terreno_y_agua_son_deterministas()
    {
        Assert.Equal("agua:Forest:orilla", IdentidadesObservacion.AguaZona("Forest"));
        Assert.Equal("agua:Mountain:lago", IdentidadesObservacion.AguaZona("Mountain", "lago"));
        Assert.Equal("terreno:Forest:arbol", IdentidadesObservacion.TerrenoZona("Forest", "arbol"));
    }
}
