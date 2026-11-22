using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Atencion;

/// <summary>Pruebas de estabilidad en NightHold (2550) y saltos iniciales de reloj (CP-608).</summary>
public class NightHoldArranqueTests
{
    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Salto_inicial_a_2550_no_dispara_hitos_diurnos_intermedios_y_mantiene_acostarse_unico_CP608()
    {
        var perfil = CargarPerfilLeah();
        var estado = new EstadoPersonaje();
        var pulsos = new List<Core.Contrato.Pulso>();
        var motor = new MotorHabitante(
            perfil,
            CargarPlantillasLeah(),
            estado,
            ModoReloj.Pelican,
            publicarPulso: p =>
            {
                p.Id = pulsos.Count + 1;
                pulsos.Add(p);
                return p;
            })
        {
            Epoca = "ep-nighthold",
            MenteHabilitada = true,
        };

        var horario = new HorarioDia(
            "Mon",
            new List<EntradaHorario>
            {
                new(900, "LeahHouse", 3, 7, "leah_sculpt"),
                new(1100, "SeedShop", 11, 23, null),
                new(1700, "LeahHouse", 8, 5, null),
                new(2400, "LeahHouse", 2, 4, "leah_sleep"),
            });

        // Primer tick en SaveLoaded a las 06:00 (despertar).
        var obs600 = new Observacion(1, 600, "LeahHouse", 3, 7, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obs600, horario, ahoraMs: 1_000, minutosJuego: 0);

        // Salto brusco de Pelican a 2550 (01:50 NightHold): no debe emitir Mon:900 ni SeedShop, solo acostarse.
        var obs2550 = new Observacion(1, 2550, "LeahHouse", 2, 4, 0.2, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obs2550, horario, ahoraMs: 2_000, minutosJuego: 0);

        var hitosEmitidos = pulsos.Where(p => p.Hito is not null).Select(p => p.Hito!.Id).ToList();
        Assert.DoesNotContain("Mon:900", hitosEmitidos);
        Assert.DoesNotContain("SeedShop", hitosEmitidos);
        Assert.Contains(HitosRutina.Acostarse, hitosEmitidos);

        // Pasos sostenidos en NightHold con minutosJuego = 0 no repiten acostarse ni disparan deriva.
        motor.ProcesarPaso(obs2550, horario, ahoraMs: 60_000, minutosJuego: 0);
        var totalTrasEstabilizar = pulsos.Count;
        motor.ProcesarPaso(obs2550, horario, ahoraMs: 120_000, minutosJuego: 0);
        Assert.Equal(totalTrasEstabilizar, pulsos.Count);
        Assert.DoesNotContain(pulsos, p => p.Motivo == Motivos.Deriva);
        Assert.Single(pulsos, p => p.Hito?.Id == HitosRutina.Acostarse);
    }
}
