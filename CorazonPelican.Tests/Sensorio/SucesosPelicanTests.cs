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
}
