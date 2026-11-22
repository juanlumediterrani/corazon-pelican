using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;
using Xunit.Abstractions;

namespace CorazonPelican.Tests.Atencion;

public class CadenciaAceptacionTests
{
    private readonly ITestOutputHelper output;

    public CadenciaAceptacionTests(ITestOutputHelper output) => this.output = output;

    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static readonly HorarioDia HorarioTipo = new("Mon", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "Forest", 33, 25, "leah_draw"),
        new EntradaHorario(1700, "Town", 29, 68),
        new EntradaHorario(2000, "LeahHouse", 8, 5),
        new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
    });

    [Fact]
    public void Medir_cadencia_dia_vanilla_y_12h_pelican_respeta_cupos_y_cadenas()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();

        // 1. Día completo Vanilla (06:00 -> 26:00 = 1200 min de juego = 840 s reales a 7 s / 10 min juego).
        var resVanilla = SimularTramo(perfil, plantillas, ModoReloj.Vanilla, minutosTotalesJuego: 1200, pasoMs: 1_000);
        this.output.WriteLine($"VANILLA (1 día = 840s reales): {resVanilla}");
        Assert.True(resVanilla.Despertar >= 1);
        Assert.True(resVanilla.Acostarse >= 1);
        Assert.True(resVanilla.Ordinarios <= perfil.Presupuesto.Vanilla.PulsosPorDiaJuego - perfil.Presupuesto.Vanilla.ReservadosCadenas);

        // 2. Sesión Pelican 12 h reales (720 min reales = 720 min juego en reloj 1:1).
        var resPelican = SimularTramo(perfil, plantillas, ModoReloj.Pelican, minutosTotalesJuego: 720, pasoMs: 10_000);
        this.output.WriteLine($"PELICAN (12 h reales): {resPelican}");
        Assert.True(resPelican.Despertar >= 1);
        Assert.True(resPelican.Ordinarios <= perfil.Presupuesto.Pelican.PulsosPorDiaJuego - perfil.Presupuesto.Pelican.ReservadosCadenas);
    }

    private sealed record ResumenCadencia(
        int TotalPulsos,
        int Despertar,
        int Acostarse,
        int Hitos,
        int Derivas,
        int Picos,
        int Transiciones,
        int Ordinarios,
        double IntervaloMedianoMinReales,
        double IntervaloP90MinReales);

    private static ResumenCadencia SimularTramo(
        PerfilPersonaje perfil,
        PlantillasProsa plantillas,
        ModoReloj modo,
        int minutosTotalesJuego,
        long pasoMs)
    {
        var motor = new MotorHabitante(perfil, plantillas, new EstadoPersonaje(), modo)
        {
            Epoca = "cadencia-cp208",
            MenteHabilitada = false,
        };

        var msPorDiezMin = HoraJuego.MilisegundosPorDiezMinutos(modo);
        var minJuegoPorPaso = (pasoMs * 10.0) / msPorDiezMin;
        var pasos = (int)Math.Ceiling(minutosTotalesJuego / minJuegoPorPaso);

        long ahoraMs = 0;
        double minAcum = 0;
        int despertar = 0, acostarse = 0, hitos = 0, derivas = 0, picos = 0, transiciones = 0;
        var marcasMs = new List<long>();

        for (var i = 0; i < pasos; i++)
        {
            var totMin = 360 + (int)minAcum;
            var hora = ((totMin / 60) * 100) + (totMin % 60);
            var obs = new Observacion(
                Dia: 1,
                Hora: Math.Min(2600, hora),
                Ubicacion: hora >= 1200 && hora < 1700 ? "Forest" : "LeahHouse",
                X: 33,
                Y: 25,
                Luz: hora >= 1900 ? 0.3 : 1.0,
                Clima: "soleado",
                Entidades: new[]
                {
                    new EntidadObservada("obj:Forest:35,25:agua", CategoriaEntidad.Terreno, "lago", "Forest", 35, 25, new[] { "agua" }),
                });

            var res = motor.ProcesarPaso(obs, HorarioTipo, ahoraMs, minJuegoPorPaso);
            foreach (var p in res.Pulsos)
            {
                marcasMs.Add(ahoraMs);
                if (p.Motivo == Motivos.Hito && p.Hito?.Id == HitosRutina.Despertar) despertar++;
                else if (p.Motivo == Motivos.Hito && p.Hito?.Id == HitosRutina.Acostarse) acostarse++;
                else if (p.Motivo == Motivos.Hito) hitos++;
                else if (p.Motivo == Motivos.Deriva) derivas++;
                else if (p.Motivo == Motivos.Pico) picos++;
                else if (p.Motivo == Motivos.Transicion) transiciones++;
            }

            ahoraMs += pasoMs;
            minAcum += minJuegoPorPaso;
        }

        if (acostarse == 0)
        {
            var fin = motor.ProcesarFinDeDia(new Observacion(1, 2600, "LeahHouse", 2, 4, 0.3, "soleado", Array.Empty<EntidadObservada>()), ahoraMs, horario: HorarioTipo);
            if (fin is not null && fin.Hito?.Id == HitosRutina.Acostarse)
            {
                acostarse++;
                marcasMs.Add(ahoraMs);
            }
        }

        var intervalosMin = marcasMs.Zip(marcasMs.Skip(1), (a, b) => (b - a) / 60_000.0).OrderBy(x => x).ToList();
        var med = intervalosMin.Count > 0 ? intervalosMin[intervalosMin.Count / 2] : 0.0;
        var p90 = intervalosMin.Count > 0 ? intervalosMin[(int)Math.Floor((intervalosMin.Count - 1) * 0.90)] : 0.0;

        return new ResumenCadencia(
            despertar + acostarse + hitos + derivas + picos + transiciones,
            despertar,
            acostarse,
            hitos,
            derivas,
            picos,
            transiciones,
            motor.Disparadores.Contadores?.OrdinariosLibresGastados ?? 0,
            Math.Round(med, 2),
            Math.Round(p90, 2));
    }
}
