using System.Diagnostics;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

public class ReflejosTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Con_0_a_2_corazones_y_jugador_en_foco_mientras_dibuja_guarda_el_cuaderno_en_el_mismo_paso(int corazones)
    {
        var perfil = CargarPerfil();
        var mundo = Escenario.Pasarela()
            .ConJugador("Granjero", corazones: corazones, distancia: 1)
            .ALas(dia: 68, hora: 1400)
            .Construir(perfil.Lexico);

        var bucle = new BucleAtencion(mundo, perfil);

        // El personaje está dibujando y nota al jugador en foco.
        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "mirar", Objetivo = "farmer:1" },
            new PasoPlan { Accion = "dibujar", Obra = "nueva" },
        });
        Assert.Equal("dibujar", mundo.Cuerpo.Continua);

        var gastadosAntes = bucle.Disparadores.Contadores?.OrdinariosLibresGastados ?? 0;

        // Un único paso de observación (250 ms) con el jugador en foco dispara guardar_cuaderno en ese mismo paso.
        var crono = Stopwatch.StartNew();
        bucle.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
        crono.Stop();

        Assert.True(crono.ElapsedMilliseconds < 100, $"El reflejo tardó {crono.ElapsedMilliseconds} ms (límite: 100 ms).");
        Assert.Null(mundo.Cuerpo.Continua);
        Assert.False(mundo.Cuerpo.EstaEnAnimacionActividad);
        var reflejo = Assert.Single(bucle.Reflejos.Historial);
        Assert.Equal("guardar_cuaderno_apertura", reflejo.ReglaId);
        Assert.Equal("guardar_cuaderno", reflejo.Paso.Accion);
        Assert.Contains(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.AccionTerminada && s.Accion == "guardar_cuaderno");

        // Ningún reflejo consume presupuesto de pulsos.
        var gastadosDespues = bucle.Disparadores.Contadores?.OrdinariosLibresGastados ?? 0;
        Assert.Equal(gastadosAntes, gastadosDespues);
    }

    [Fact]
    public void Con_3_o_mas_corazones_en_foco_mientras_dibuja_no_oculta_el_cuaderno_y_levanta_la_vista()
    {
        var perfil = CargarPerfil();
        var mundo = Escenario.Pasarela()
            .ConJugador("Amiga", corazones: 5, distancia: 1)
            .ALas(dia: 68, hora: 1400)
            .Construir(perfil.Lexico);

        var bucle = new BucleAtencion(mundo, perfil);
        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "mirar", Objetivo = "farmer:1" },
            new PasoPlan { Accion = "dibujar", Obra = "nueva" },
        });

        bucle.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));

        var reflejo = Assert.Single(bucle.Reflejos.Historial);
        Assert.Equal("levantar_vista_dibujando", reflejo.ReglaId);
        Assert.Equal("levantar_la_vista", reflejo.Paso.Accion);
        Assert.DoesNotContain(mundo.Cuerpo.Sucesos, s => s.Accion == "guardar_cuaderno");
    }

    [Fact]
    public void Enfriamiento_por_regla_evita_repetir_el_gesto_y_ningun_reflejo_consume_presupuesto()
    {
        var perfil = CargarPerfil();
        var mundo = Escenario.Pasarela()
            .ConJugador("Amiga", corazones: 6, distancia: 6)
            .ALas(dia: 68, hora: 1400)
            .Construir(perfil.Lexico);

        var bucle = new BucleAtencion(mundo, perfil);
        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "mirar", Objetivo = "farmer:1" } });

        // Primer paso: dispara el saludo inmediato y no gasta pulso.
        bucle.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
        Assert.Single(bucle.Reflejos.Historial);
        Assert.Equal("saludar_amistad", bucle.Reflejos.Historial[0].ReglaId);
        Assert.Equal("saludo", mundo.Cuerpo.UltimoGesto);
        Assert.Empty(bucle.Disparos);
        Assert.Equal(0, bucle.Disparadores.Contadores?.OrdinariosLibresGastados ?? 0);

        // Durante los siguientes 10 s (enfriamiento = 60 s), aunque vuelva a estar en foco no repite el gesto.
        var obs = mundo.Observar();
        var bandasEnFoco = new[] { new CorazonPelican.Core.Sensorio.EntidadEnBanda("farmer:1", CorazonPelican.Core.Sensorio.Banda.Foco, 0.78) };
        Assert.Null(bucle.Reflejos.EvaluarYEjecutar(obs, bandasEnFoco, mundo.Cuerpo.Ejecutor, ahoraMs: 10_000));
        Assert.Single(bucle.Reflejos.Historial);

        // Tras superar los 60 s de enfriamiento, si vuelve a entrar en foco vuelve a saludar sin gastar pulso.
        var segundo = bucle.Reflejos.EvaluarYEjecutar(obs, bandasEnFoco, mundo.Cuerpo.Ejecutor, ahoraMs: 65_000);
        Assert.NotNull(segundo);
        Assert.Equal(2, bucle.Reflejos.Historial.Count);
        Assert.Equal(0, bucle.Disparadores.Contadores?.OrdinariosLibresGastados ?? 0);
    }
}
