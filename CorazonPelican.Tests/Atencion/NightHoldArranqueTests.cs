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

    [Fact]
    public void GuardaCicloPelican_protege_arranque_nighthold_y_rollover_antes_de_las_0600()
    {
        var guarda = new GuardaCicloPelican();
        guarda.MarcarInicioJornadaOCarga(0);

        // En modo no-Pelican nunca pausa, aunque se indiquen flags externos.
        Assert.False(guarda.DebePausarSensorio(esModoPelican: false, enNightHold: true, enRollover: true, horaJuego: 600, ahoraMs: 0));

        // Ventana inicial de 2_000 ms a las 06:00 tras SaveLoaded/DayStarted.
        guarda.MarcarInicioJornadaOCarga(1_000);
        Assert.True(guarda.DebePausarSensorio(true, false, false, 600, 1_500));
        Assert.True(guarda.DebePausarSensorio(true, false, false, 600, 2_999));

        // Si el reloj salta a otra hora antes de agotar 2_000 ms, libera de inmediato.
        Assert.False(guarda.DebePausarSensorio(true, false, false, 1430, 1_800));
        Assert.False(guarda.DebePausarSensorio(true, false, false, 600, 1_900));

        // Si realmente amanece a las 06:00, libera al completar los 2_000 ms.
        guarda.MarcarInicioJornadaOCarga(10_000);
        Assert.True(guarda.DebePausarSensorio(true, false, false, 600, 11_000));
        Assert.False(guarda.DebePausarSensorio(true, false, false, 600, 12_000));

        // Pausa en NightHold y Rollover; tras Rollover conserva ventana de 2_000 ms si sigue en 06:00.
        Assert.True(guarda.DebePausarSensorio(true, enNightHold: true, enRollover: false, horaJuego: 2550, ahoraMs: 15_000));
        Assert.True(guarda.DebePausarSensorio(true, enNightHold: false, enRollover: true, horaJuego: 600, ahoraMs: 20_000));
        Assert.True(guarda.DebePausarSensorio(true, enNightHold: false, enRollover: false, horaJuego: 600, ahoraMs: 20_500));
        Assert.False(guarda.DebePausarSensorio(true, enNightHold: false, enRollover: false, horaJuego: 600, ahoraMs: 22_000));
    }

    [Fact]
    public void Ciclo_completo_NightHold_a_amanecer_con_GuardaCicloPelican_evita_despertar_fantasma_y_conserva_hitos()
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
            Epoca = "ep-ciclo",
            MenteHabilitada = true,
        };

        var horario = new HorarioDia(
            "Mon",
            new List<EntradaHorario>
            {
                new(900, "LeahHouse", 3, 7, "leah_sculpt"),
                new(2400, "LeahHouse", 2, 4, "leah_sleep"),
            });

        var guarda = new GuardaCicloPelican();
        guarda.MarcarInicioJornadaOCarga(0);

        // SaveLoaded con hora inicial 600 y paso posterior a NightHold 2550: ambos pausados antes de emitir despertar.
        Assert.True(guarda.DebePausarSensorio(true, false, false, 600, 500));
        Assert.True(guarda.DebePausarSensorio(true, true, false, 2550, 1_500));
        // Rollover del cambio de día aún marcando 600.
        Assert.True(guarda.DebePausarSensorio(true, false, true, 600, 5_000));
        Assert.Empty(pulsos);

        // DayStarted del nuevo día (día 2) a las 06:00: espera 2_000 ms antes de liberar el sensorio.
        guarda.MarcarInicioJornadaOCarga(6_000);
        Assert.True(guarda.DebePausarSensorio(true, false, false, 600, 7_500));
        Assert.False(guarda.DebePausarSensorio(true, false, false, 600, 8_000));

        var obs600 = new Observacion(2, 600, "LeahHouse", 3, 7, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obs600, horario, ahoraMs: 8_000, minutosJuego: 0);

        var obs900 = new Observacion(2, 900, "LeahHouse", 3, 7, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obs900, horario, ahoraMs: 12_000, minutosJuego: 10);

        Assert.Single(pulsos, p => p.Hito?.Id == HitosRutina.Despertar);
        Assert.Single(pulsos, p => p.Hito?.Id == "Mon:900");
    }
}
