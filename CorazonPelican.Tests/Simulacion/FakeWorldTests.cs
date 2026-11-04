using System.Diagnostics;
using System.Text.Json;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Tests.Simulacion;

public class FakeWorldTests
{
    private static Pulso PulsoDe(FakeWorld mundo, long id)
        => new() { Id = id, Epoca = "epoca-test", Personaje = "prueba", Motivo = "deriva", Escena = "…" };

    private static string Huella(IEnumerable<Observacion> observaciones)
        => JsonSerializer.Serialize(observaciones);

    [Fact]
    public void Un_dia_vanilla_completo_se_simula_en_menos_de_dos_segundos()
    {
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9, deambula: true)
            .ConNpc("Linus", "Mountain", 30, 20).ALas(dia: 68, hora: HoraJuego.InicioDia).Construir();
        var reloj = Stopwatch.StartNew();

        // 6:00 → 2:00 son 120 tramos de 10 minutos; en vanilla, 7 s reales cada uno.
        var observaciones = mundo.AvanzarObservando(TimeSpan.FromSeconds(120 * 7));

        reloj.Stop();
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(2), $"Tardó {reloj.Elapsed}.");
        Assert.Equal(69, mundo.Reloj.Juego.Dia);
        Assert.Equal(HoraJuego.InicioDia, mundo.Reloj.Juego.Hora);
        Assert.Equal(120 * 7 * 1000 / FakeWorld.PasoMs, observaciones.Count);
        Assert.Contains(observaciones, o => o.Luz < 1.0);
    }

    [Fact]
    public void La_misma_semilla_da_la_misma_secuencia_de_observaciones()
    {
        static List<Observacion> Correr(int semilla)
            => Escenario.Pasarela().ConSemilla(semilla).ConJugador("Ana", 6, 9, deambula: true).ConNpc("Linus", "Mountain", 30, 20)
                .Construir().AvanzarObservando(TimeSpan.FromMinutes(5));

        Assert.Equal(Huella(Correr(7)), Huella(Correr(7)));
        Assert.NotEqual(Huella(Correr(7)), Huella(Correr(8)));
    }

    [Fact]
    public void Construir_dos_veces_da_mundos_independientes()
    {
        var escenario = Escenario.Pasarela().ConNpc("Linus", "Mountain", 30, 20);
        var a = escenario.Construir();
        var b = escenario.Construir();

        a.Avanzar(TimeSpan.FromMinutes(1));

        Assert.Equal(30, b.Observar().Entidades.Single(e => e.Ref == "npc:Linus").X);
        Assert.Equal(0, b.Reloj.MilisegundosReales);
    }

    [Fact]
    public void El_escenario_coloca_al_jugador_con_su_relacion_y_distancia()
    {
        var observacion = Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9).Construir().Observar();

        var ana = observacion.Entidades.Single(e => e.Categoria == CategoriaEntidad.Granjero);
        Assert.Equal("farmer:1", ana.Ref);
        Assert.Equal(6, ana.Corazones);
        Assert.Equal(9, Math.Abs(ana.Y - observacion.Y) + Math.Abs(ana.X - observacion.X));
        Assert.Equal("Mountain", observacion.Ubicacion);
    }

    [Fact]
    public void En_modo_pelican_diez_minutos_de_juego_son_diez_minutos_reales()
    {
        var mundo = Escenario.Pasarela().EnModo(ModoReloj.Pelican).ALas(68, 1700).Construir();

        mundo.Avanzar(TimeSpan.FromMinutes(9.9));
        Assert.Equal(1700, mundo.Reloj.Juego.Hora);
        mundo.Avanzar(TimeSpan.FromSeconds(6));
        Assert.Equal(1710, mundo.Reloj.Juego.Hora);
    }

    [Fact]
    public void Mente_lenta_responde_tras_su_latencia_en_tiempo_virtual()
    {
        var mundo = Escenario.Pasarela().ConMente(FakeMind.Lenta(TimeSpan.FromSeconds(30))).Construir();
        var reloj = Stopwatch.StartNew();

        mundo.EmitirPulso(PulsoDe(mundo, 1));
        mundo.Avanzar(TimeSpan.FromSeconds(29.75));
        Assert.Empty(mundo.RespuestasEntregadas);
        mundo.Avanzar(TimeSpan.FromSeconds(0.25));

        Assert.Single(mundo.RespuestasEntregadas);
        Assert.Equal(1, mundo.RespuestasEntregadas[0].BasadaEn);
        Assert.True(reloj.Elapsed < TimeSpan.FromSeconds(1), "La latencia es virtual: el test no espera de verdad.");
    }

    [Fact]
    public void Mente_caida_recibe_pero_nunca_responde()
    {
        var mente = FakeMind.Caida();
        var mundo = Escenario.Pasarela().ConMente(mente).Construir();

        mundo.EmitirPulso(PulsoDe(mundo, 1));
        mundo.Avanzar(TimeSpan.FromHours(1));

        Assert.Single(mente.Recibidos);
        Assert.Empty(mundo.RespuestasEntregadas);
    }

    [Fact]
    public void Mente_guionizada_responde_lo_que_dice_el_guion()
    {
        var mente = FakeMind.Rapida(p => new Respuesta { Pensamiento = $"pienso en el pulso {p.Id}", Epoca = p.Epoca, BasadaEn = p.Id });
        var mundo = Escenario.Pasarela().ConMente(mente).Construir();

        mundo.EmitirPulso(PulsoDe(mundo, 7));
        mundo.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));

        Assert.Equal("pienso en el pulso 7", mundo.RespuestasEntregadas.Single().Pensamiento);
    }

    [Fact]
    public void El_cuerpo_va_se_sienta_y_termina_el_plan()
    {
        var mundo = Escenario.Pasarela().Construir();

        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" },
        });
        mundo.Avanzar(TimeSpan.FromSeconds(5));
        Assert.DoesNotContain(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado);
        mundo.Avanzar(TimeSpan.FromSeconds(5));

        // 12 casillas a 533 ms + 1 s sentándose ≈ 7,4 s.
        var fin = mundo.Cuerpo.Sucesos.Single(s => s.Tipo == TipoSuceso.PlanTerminado);
        Assert.InRange(fin.MilisegundosReales, 7_000, 8_000);
        Assert.Equal(18, mundo.Observar().Y);
        Assert.True(mundo.Cuerpo.Sentada);
        Assert.False(mundo.Cuerpo.Ocupado);
    }

    [Fact]
    public void Ir_a_un_lugar_desconocido_bloquea_y_no_termina_el_plan()
    {
        var mundo = Escenario.Pasarela().Construir();

        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "ir_a", Lugar = "luna" }, new PasoPlan { Accion = "sentarse", Lugar = "luna" } });
        mundo.Avanzar(TimeSpan.FromSeconds(10));

        Assert.Contains(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.AccionBloqueada && s.Detalle!.Contains("luna"));
        Assert.DoesNotContain(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado);
        Assert.Equal("Mountain", mundo.Observar().Ubicacion);
    }

    [Fact]
    public void Dibujar_es_continua_y_cierra_el_plan_al_empezar()
    {
        var mundo = Escenario.Pasarela().Construir();

        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "sentarse", Lugar = "pasarela_lago" }, new PasoPlan { Accion = "dibujar", Obra = "nueva" } });
        mundo.Avanzar(TimeSpan.FromSeconds(2));

        Assert.Equal("dibujar", mundo.Cuerpo.Continua);
        Assert.Single(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado);

        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "guardar_cuaderno" } });
        Assert.Null(mundo.Cuerpo.Continua);
    }

    [Fact]
    public void Ir_a_otra_ubicacion_cambia_lo_que_se_observa()
    {
        var mundo = Escenario.Pasarela().ConJugador("Ana", 6, 9).Construir();

        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "ir_a", Lugar = "tu_casa" } });
        mundo.Avanzar(TimeSpan.FromMinutes(1));

        var observacion = mundo.Observar();
        Assert.Equal("LeahHouse", observacion.Ubicacion);
        Assert.Empty(observacion.Entidades);
    }
}
