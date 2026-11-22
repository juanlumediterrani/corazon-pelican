using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

/// <summary>Pruebas de festivales reprogramados y ventana prime-time de Pelican 4Ever (CP-609).</summary>
public class FestivalesPelicanTests
{
    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Detecta_festival_reprogramado_en_FestivalDates_y_aplica_nunca_festival_R16_CP609()
    {
        // Pelican 4Ever mueve spring13 -> spring16 vía RescheduledFestivals en Data/Festivals/FestivalDates.
        var festivalDates = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["spring16"] = "Egg Festival",
        };

        Assert.Null(CalendarioFestivales.ResolverFestivalEnFecha(festivalDates, "spring", 13));
        Assert.Equal("Egg Festival", CalendarioFestivales.ResolverFestivalEnFecha(festivalDates, "spring", 16));

        var ventanaPrimeTime = CalendarioFestivales.ParsearVentanaFestival("Town/1800 2350");
        Assert.Equal((1800, 2350), ventanaPrimeTime);
        Assert.False(CalendarioFestivales.EstaEnVentanaFestival(1200, ventanaPrimeTime));
        Assert.True(CalendarioFestivales.EstaEnVentanaFestival(1900, ventanaPrimeTime));

        var perfil = CargarPerfilLeah();
        var estado = new EstadoPersonaje();
        var motor = new MotorHabitante(perfil, CargarPlantillasLeah(), estado, ModoReloj.Pelican)
        {
            Epoca = "ep-fest",
        };

        var horario = new HorarioDia(
            "Fri",
            new List<EntradaHorario>
            {
                new(1600, "Saloon", 2, 20, null),
                new(2340, "LeahHouse", 2, 4, "leah_sleep"),
            });

        // En spring16 (festival movido) se inyecta "festival" en condicionesDia y R-16 bloquea el hito "saloon".
        var condiciones = CalendarioFestivales.ConstruirCondicionesDia("soleado", esDiaFestival: true);
        var obs = new Observacion(16, 1200, "LeahHouse", 8, 5, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obs, horario, ahoraMs: 1_000, minutosJuego: 0, condicionesDia: condiciones);

        var entradaSaloon = motor.AgendaActual!.Entradas.First(e => e.Hora == 1600);
        var motivos = ValidadorCambiosRutina.ValidarYAplicar(
            new[] { new Core.Contrato.CambioRutina { Hito = entradaSaloon.Hito, Cambio = "saltar" } },
            motor.AgendaActual,
            perfil,
            motor.ContextoRutina);

        Assert.NotEmpty(motivos);
        Assert.Contains(motivos, m => m.Prosa.Contains("festival", StringComparison.OrdinalIgnoreCase));
    }
}
