using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Sensorio;

public class SucesosPelicanTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Sin_Pelican_configurado_no_se_habilita_integracion()
    {
        Assert.False(ConversorSucesosPelican.EsConfigurado(null));
        Assert.False(ConversorSucesosPelican.EsConfigurado(""));
        Assert.False(ConversorSucesosPelican.EsConfigurado("   "));
        Assert.True(ConversorSucesosPelican.EsConfigurado("http://127.0.0.1:8085"));
    }

    [Fact]
    public void Jugador_que_se_conecta_en_Pelican_aparece_como_suceso_en_la_siguiente_escena_relevante()
    {
        const string json = """
        {
          "SinceId": 0,
          "LastEventId": 42,
          "Events": [
            {
              "Id": 42,
              "Type": "player.connected",
              "TimestampUtc": "2026-11-22T02:45:00Z",
              "GameDay": 10,
              "GameTime": 1000,
              "Summary": "Álex se ha conectado al servidor.",
              "Data": {
                "playerName": "Álex",
                "playerId": "123456"
              }
            }
          ]
        }
        """;

        var eventos = ConversorSucesosPelican.ParsearRespuestaJson(json, out var lastId);
        Assert.Equal(42, lastId);
        var evento = Assert.Single(eventos);

        var cola = new ColaSucesosPelican();
        cola.Encolar(evento);
        cola.Encolar(evento); // duplicado por id ignorado
        Assert.Equal(1, cola.CantidadPendiente);

        var obsBase = new Observacion(
            Dia: 10,
            Hora: 1000,
            Ubicacion: "Forest",
            X: 33,
            Y: 25,
            Luz: 1.0,
            Clima: "soleado",
            Entidades: Array.Empty<EntidadObservada>());

        var obsConSuceso = cola.AplicarAObservacion(obsBase);
        Assert.Equal(0, cola.CantidadPendiente);

        var sucesoObs = Assert.Single(obsConSuceso.Sucesos!);
        Assert.Equal("player.connected", sucesoObs.Tipo);
        Assert.Contains("Álex", sucesoObs.Descripcion);
        Assert.Equal("farmer:123456", sucesoObs.ActorRef);

        var entidadSuceso = Assert.Single(obsConSuceso.Entidades);
        Assert.Equal(CategoriaEntidad.Suceso, entidadSuceso.Categoria);

        var motor = new MotorHabitante(CargarPerfil(), CargarPlantillas(), new EstadoPersonaje(), ModoReloj.Pelican)
        {
            Epoca = "epoca-cp602",
        };
        var escena = motor.PrevisualizarEscena(obsConSuceso);
        Assert.Contains("Álex", escena.Escena);
    }

    [Fact]
    public void Filtra_ruido_administrativo_y_entrega_sucesos_a_multiples_personajes_CP607()
    {
        var obsBase = new Observacion(10, 1000, "Forest", 33, 25, 1.0, "soleado", Array.Empty<EntidadObservada>());

        // Eventos internos de administración/seguridad/telemetría no deben entrar como entidades en escena.
        foreach (var tipoRuido in new[] { "checkpoint.created", "economy.daily", "rollover.started", "host.lockdown", "host.test.command", "player.pairing.invited" })
        {
            Assert.Null(ConversorSucesosPelican.Convertir(
                new EventoHostPelican { Id = 1, Type = tipoRuido, Summary = $"Resumen interno {tipoRuido}" },
                "Forest",
                33,
                25));
        }

        var cola = new ColaSucesosPelican();
        cola.Encolar(new EventoHostPelican { Id = 10, Type = "checkpoint.created", Summary = "Checkpoint completado." });
        cola.Encolar(new EventoHostPelican
        {
            Id = 11,
            Type = "player.connected",
            Summary = "Marta llegó al valle.",
            Data = new Dictionary<string, System.Text.Json.JsonElement>
            {
                ["playerName"] = System.Text.Json.JsonDocument.Parse("\"Marta\"").RootElement,
                ["playerId"] = System.Text.Json.JsonDocument.Parse("\"77\"").RootElement,
            },
        });
        cola.Encolar(new EventoHostPelican { Id = 12, Type = "festival.run.started", Summary = "Comienza la Danza de las Flores." });

        var obsLeah = cola.AplicarAObservacion(obsBase, "leah");
        var obsElliott = cola.AplicarAObservacion(obsBase, "elliott");
        Assert.Equal(2, obsLeah.Sucesos!.Count);
        Assert.Equal(2, obsElliott.Sucesos!.Count);

        // Una segunda lectura del mismo personaje no duplica los eventos ya consumidos.
        var obsLeahSegunda = cola.AplicarAObservacion(obsBase, "leah");
        Assert.Null(obsLeahSegunda.Sucesos);
    }
}
