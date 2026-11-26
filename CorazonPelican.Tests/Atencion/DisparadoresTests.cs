using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Atencion;

public class DisparadoresTests
{
    private static PerfilPersonaje Leah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    /// <summary>Horarios reales de Leah (docs/notas-juego.md §3.1), resumidos.</summary>
    private static readonly HorarioDia Otono = new("fall", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "Town", 86, 66, "square_1_5_3"),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2200, "LeahHouse", 13, 10),
        new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
    });

    private static readonly HorarioDia Viernes = new("Fri", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "LeahHouse", 6, 5),
        new EntradaHorario(1400, "LeahHouse", 9, 5),
        new EntradaHorario(1600, "Saloon", 2, 20),
        new EntradaHorario(2340, "LeahHouse", 2, 4, "leah_sleep"),
    });

    private static IEnumerable<Disparo> Hitos(IEnumerable<Disparo> d, string? id = null)
        => d.Where(x => x.Motivo == Motivos.Hito && (id is null || x.Hito!.Id == id));

    private static Observacion Quieta(int dia = 1, int hora = 1200) => new(dia, hora, "Mountain", 0, 0, 1.0, "soleado", Array.Empty<EntidadObservada>());

    private static List<EntidadEnBanda> Menores(int cuantas, double saliencia)
        => Enumerable.Range(0, cuantas).Select(i => new EntidadEnBanda($"npc:{i}", saliencia >= 0.40 ? Banda.Mencion : Banda.Periferia, saliencia)).ToList();

    [Fact]
    public void Un_festival_no_produce_mas_de_una_pulsion_por_periodo_refractario_salvo_interlocucion()
    {
        var perfil = Leah();
        var mundo = Escenario.Pasarela().ConMultitud(50).ConJugador("Ana", corazones: 10, distancia: 3, deambula: true).Construir();
        var buscandoGente = new ContextoSaliencia(perfil.Rasgos, EstadoAtencion.Abierta, perfil.Umbrales.Periferia, new Dictionary<string, double> { ["persona"] = 4 });
        var bucle = new BucleAtencion(mundo, perfil, buscandoGente);

        bucle.Avanzar(TimeSpan.FromSeconds(61));
        bucle.Interlocucion("farmer:1");
        bucle.Avanzar(TimeSpan.FromMinutes(4));

        var estimulos = bucle.Disparos.Where(d => d.Motivo != Motivos.Interlocucion).OrderBy(d => d.MilisegundosReales).ToList();
        Assert.True(estimulos.Count >= 4, $"El festival debe provocar pulsiones; hubo {estimulos.Count}.");
        var ventana = (long)(perfil.Refractario.SemividaS * 1000);
        foreach (var (anterior, siguiente) in estimulos.Zip(estimulos.Skip(1)))
        {
            Assert.True(siguiente.MilisegundosReales - anterior.MilisegundosReales >= ventana,
                $"{anterior.Motivo}@{anterior.MilisegundosReales} y {siguiente.Motivo}@{siguiente.MilisegundosReales} caen en la misma ventana.");
        }

        Assert.Single(bucle.Disparos, d => d.Motivo == Motivos.Interlocucion);
    }

    [Fact]
    public void Muchas_saliencias_menores_sostenidas_acaban_en_deriva()
    {
        var disparadores = new Disparadores(Leah());
        var disparos = new List<Disparo>();

        for (var paso = 1; paso <= 400 && disparos.Count == 0; paso++)
        {
            disparos.AddRange(disparadores.Observar(Quieta(), Menores(10, 0.3), null, paso * 250, minutosJuego: 0.36));
        }

        Assert.Equal(Motivos.Deriva, Assert.Single(disparos).Motivo);
    }

    [Fact]
    public void Una_sola_saliencia_menor_nunca_llega_a_deriva()
    {
        var disparadores = new Disparadores(Leah());

        for (var paso = 1; paso <= 20_000; paso++)
        {
            Assert.Empty(disparadores.Observar(Quieta(), Menores(1, 0.15), null, paso * 250, minutosJuego: 0.36));
        }

        Assert.True(disparadores.ValorIntegrador < Leah().Umbrales.Deriva);
    }

    [Fact]
    public void Un_plan_ir_a_y_sentarse_produce_un_unico_hito_al_sentarse()
    {
        var perfil = Leah();
        perfil.Umbrales.Deriva = 1_000; // aislar el hito: sin deriva por el camino
        var bucle = new BucleAtencion(Escenario.Pasarela().Construir(), perfil);

        bucle.EjecutarPlan("int-1", new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" }, new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" });
        bucle.Avanzar(TimeSpan.FromSeconds(20));

        var hito = Assert.Single(Hitos(bucle.Disparos));
        Assert.Equal(HitosRutina.OrigenIntencion, hito.Hito!.Origen);
        Assert.Equal("int-1", hito.Hito.Id);
        var sentada = bucle.Mundo.Cuerpo.Sucesos.Single(s => s.Tipo == TipoSuceso.PlanTerminado).MilisegundosReales;
        Assert.InRange(hito.MilisegundosReales, sentada, sentada + FakeWorld.PasoMs);
    }

    [Fact]
    public void Si_a_mitad_de_camino_salta_una_deriva_el_hito_omitible_no_se_emite()
    {
        var bucle = new BucleAtencion(Escenario.Pasarela().ConMultitud(30).Construir(), Leah());

        bucle.EjecutarPlan("int-1", new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" }, new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" });
        bucle.Avanzar(TimeSpan.FromSeconds(20));

        var plan = bucle.Mundo.Cuerpo.Sucesos.Single(s => s.Tipo == TipoSuceso.PlanTerminado).MilisegundosReales;
        Assert.Contains(bucle.Disparos, d => d.Motivo == Motivos.Deriva && d.MilisegundosReales < plan);
        Assert.Empty(Hitos(bucle.Disparos));
        Assert.Contains(bucle.Disparadores.Omitidos, h => h.Id == "int-1");
    }

    [Fact]
    public void Un_hito_de_rutina_no_omitible_sale_aunque_haya_pensado_y_lleva_su_contexto()
    {
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 1300).ConHorarios((_, _) => Viernes).Construir(), Leah());

        bucle.Avanzar(TimeSpan.FromSeconds(7 * 7));   // 14:10: ya está en el plan que acaba al ir al Saloon
        bucle.Interlocucion("farmer:1");              // piensa durante ese plan
        bucle.Avanzar(TimeSpan.FromSeconds(7 * 15));  // pasa las 16:00

        // El Saloon (omitir_si_ya_penso: false) sale igual y lleva personas_cerca.
        var saloon = Assert.Single(Hitos(bucle.Disparos, "Fri:1600"));
        Assert.Equal(new[] { "personas_cerca" }, saloon.Hito!.Contexto);
        Assert.False(saloon.Hito.OmitirSiYaPenso);
        Assert.Equal(new[] { "Fri:2340" }, saloon.Agenda!.Select(a => a.Hito));
    }

    [Fact]
    public void Un_hito_omitible_de_la_rutina_se_omite_si_penso_durante_su_plan()
    {
        var perfil = Leah();
        perfil.Umbrales.Deriva = 1_000;
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 1300).ConHorarios((_, _) => Viernes).Construir(), perfil);

        bucle.Avanzar(TimeSpan.FromSeconds(7 * 2));   // 13:20: en el plan que acaba al disponerse para las 14:00
        bucle.Interlocucion("farmer:1");
        bucle.Avanzar(TimeSpan.FromSeconds(7 * 4));   // pasa las 13:50

        // 14:00 no tiene anotación: por defecto se omite si ya pensó durante su plan.
        Assert.Contains(bucle.Disparadores.Omitidos, h => h.Id == "Fri:1400");
        Assert.DoesNotContain(Hitos(bucle.Disparos), d => d.Hito!.Id == "Fri:1400");
    }

    [Fact]
    public void El_hito_de_rutina_sale_antes_de_la_hora_de_su_entrada()
    {
        var perfil = Leah();
        perfil.Umbrales.Deriva = 1_000;
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 1700).ConHorarios((_, _) => Otono).Construir(), perfil);

        bucle.Avanzar(TimeSpan.FromSeconds(7 * 13));

        var hito = Assert.Single(Hitos(bucle.Disparos, "fall:1900"));
        Assert.Equal(1850, hito.Hora);
    }

    [Fact]
    public void Cada_dia_tiene_exactamente_un_despertar_y_un_acostarse()
    {
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 600).ConHorarios((_, _) => Otono).Construir(), Leah());

        bucle.Avanzar(TimeSpan.FromSeconds(3 * 120 * 7));

        foreach (var dia in new[] { 68, 69, 70 })
        {
            Assert.Single(Hitos(bucle.Disparos, HitosRutina.Despertar), d => d.Dia == dia);
            Assert.Single(Hitos(bucle.Disparos, HitosRutina.Acostarse), d => d.Dia == dia);
        }
    }

    [Fact]
    public void Si_el_dia_termina_antes_de_acostarse_la_cadena_sale_entonces_y_una_sola_vez()
    {
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 2100).ConHorarios((_, _) => Otono).Construir(), Leah());

        bucle.Avanzar(TimeSpan.FromSeconds(7 * 6));   // 22:00: el jugador se va a dormir
        var fin = bucle.Disparadores.FinDeDia(bucle.Mundo.Observar(), bucle.Mundo.Reloj.MilisegundosReales);
        var otraVez = bucle.Disparadores.FinDeDia(bucle.Mundo.Observar(), bucle.Mundo.Reloj.MilisegundosReales);
        bucle.Avanzar(TimeSpan.FromSeconds(7 * 40));  // pasa las 24:00 (cama) y el cambio de día

        Assert.Equal("fin_de_dia", fin!.Detalle);
        Assert.Equal(HitosRutina.Acostarse, fin.Hito!.Id);
        Assert.Null(otraVez);
        Assert.DoesNotContain(Hitos(bucle.Disparos, HitosRutina.Acostarse), d => d.Dia == 68);
        Assert.Single(Hitos(bucle.Disparos, HitosRutina.Despertar), d => d.Dia == 69);
    }

    [Fact]
    public void Un_dia_sin_horario_tambien_se_acuesta_al_cambiar_de_dia()
    {
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 2400).Construir(), Leah());

        bucle.Avanzar(TimeSpan.FromSeconds(7 * 15));

        var acostarse = Assert.Single(Hitos(bucle.Disparos, HitosRutina.Acostarse));
        Assert.Equal(68, acostarse.Dia);
        Assert.Equal(HitosRutina.FormaCadena, acostarse.Hito!.Forma);
        Assert.Single(Hitos(bucle.Disparos, HitosRutina.Despertar), d => d.Dia == 69);
    }

    [Fact]
    public void Los_hitos_siguen_al_horario_de_cada_dia_sin_cambiar_el_perfil()
    {
        var perfil = Leah();
        perfil.Umbrales.Deriva = 1_000; // sin deriva, ningún hito omitible se omite: se ve el horario entero
        var bucle = new BucleAtencion(
            Escenario.Pasarela().ALas(68, 600).ConHorarios((_, clima) => clima == "lluvia" ? Viernes : Otono).Construir(), perfil);

        bucle.Avanzar(TimeSpan.FromSeconds(120 * 7) - TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
        bucle.Mundo.Clima = "lluvia"; // llueve durante la noche: el día 69 amanece lloviendo
        bucle.Avanzar(TimeSpan.FromSeconds(120 * 7));

        Assert.DoesNotContain(bucle.Disparos, d => d.Motivo == Motivos.Transicion && d.Detalle == "clima:lluvia");
        var deRutina = Hitos(bucle.Disparos).Where(d => d.Hito!.Origen == HitosRutina.OrigenRutina && d.Hito.Id.Contains(':')).ToList();
        Assert.Contains(deRutina, d => d.Dia == 68 && d.Hito!.Id == "fall:1200");
        Assert.Contains(deRutina, d => d.Dia == 69 && d.Hito!.Id == "Fri:1600");
        Assert.All(deRutina.Where(d => d.Dia == 68), d => Assert.StartsWith("fall:", d.Hito!.Id));
        Assert.All(deRutina.Where(d => d.Dia == 69), d => Assert.StartsWith("Fri:", d.Hito!.Id));
        // fall:2200 se omite: la puesta de sol (≈19:15) es una pulsión durante su plan.
        Assert.Equal(new[] { "fall:900", "fall:1200", "fall:1900" }, deRutina.Where(d => d.Dia == 68).Select(d => d.Hito!.Id));
        Assert.Contains(bucle.Disparadores.Omitidos, h => h.Id == "fall:2200");
        Assert.Equal(new[] { "Fri:900", "Fri:1200", "Fri:1400", "Fri:1600" }, deRutina.Where(d => d.Dia == 69).Select(d => d.Hito!.Id));
    }

    [Fact]
    public void La_puesta_de_sol_es_una_sola_transicion_y_el_cambio_de_clima_otra()
    {
        var bucle = new BucleAtencion(Escenario.Pasarela().ALas(68, 1700).Construir(), Leah());

        bucle.Avanzar(TimeSpan.FromSeconds(7 * 24));
        bucle.Mundo.Clima = "lluvia";
        bucle.Avanzar(TimeSpan.FromSeconds(60));

        var transiciones = bucle.Disparos.Where(d => d.Motivo == Motivos.Transicion).ToList();
        Assert.Single(transiciones, d => d.Detalle == "anochece");
        Assert.Single(transiciones, d => d.Detalle == "clima:lluvia");
    }

    [Fact]
    public void Un_pico_salta_al_cruzar_el_umbral_y_no_se_repite_mientras_sigue_arriba()
    {
        var disparadores = new Disparadores(Leah());
        var arriba = new List<EntidadEnBanda> { new("farmer:1", Banda.Foco, 0.95) };
        var abajo = new List<EntidadEnBanda> { new("farmer:1", Banda.Mencion, 0.5) };
        var disparos = new List<Disparo>();
        long t = 0;

        disparos.AddRange(disparadores.Observar(Quieta(), abajo, null, t += 250, 0));
        disparos.AddRange(disparadores.Observar(Quieta(), arriba, null, t += 250, 0));
        for (var i = 0; i < 1_000; i++)
        {
            disparos.AddRange(disparadores.Observar(Quieta(), arriba, null, t += 250, 0));
        }

        disparos.AddRange(disparadores.Observar(Quieta(), abajo, null, t += 250, 0));
        disparos.AddRange(disparadores.Observar(Quieta(), arriba, null, t += 250, 0));

        Assert.Equal(2, disparos.Count(d => d.Motivo == Motivos.Pico && d.Ref == "farmer:1"));
    }

    [Theory]
    [InlineData("saloon")]
    [InlineData("esculpir")]
    [InlineData("dibujar_fuera")]
    [InlineData("volver_a_casa")]
    public void Core_no_nombra_hitos_concretos(string hito)
    {
        var ficheros = Directory.GetFiles(Repo.Ruta("CorazonPelican.Core"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        Assert.DoesNotContain(ficheros, f => File.ReadAllText(f).Contains(hito, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Multiples_entidades_de_agua_en_terreno_se_deduplican_en_integrador_y_no_provocan_rafagas_de_deriva()
    {
        var perfil = Leah();
        var disparadoresCuarenta = new Disparadores(perfil);
        var disparadoresUno = new Disparadores(perfil);

        var entidadesObs = new List<EntidadObservada>();
        var bandas = new List<EntidadEnBanda>();
        for (var i = 0; i < 40; i++)
        {
            var id = IdentidadesObservacion.Agua("Forest", 10 + i, 20);
            entidadesObs.Add(new EntidadObservada(id, CategoriaEntidad.Terreno, "agua", "Forest", 10 + i, 20, new[] { "agua" }, Visible: true, Distancia: 4));
            bandas.Add(new EntidadEnBanda(id, Banda.Mencion, 0.60));
        }

        var obsCuarenta = new Observacion(68, 1200, "Forest", 8, 20, 1.0, "soleado", entidadesObs);
        var obsUno = new Observacion(68, 1200, "Forest", 8, 20, 1.0, "soleado", new[] { entidadesObs[0] });
        var bandasUno = new[] { bandas[0] };

        var disparos = new List<Disparo>();
        long t = 0;
        for (var paso = 0; paso < 48; paso++) // 12 s simulados (~12 min de juego)
        {
            t += 250;
            disparos.AddRange(disparadoresCuarenta.Observar(obsCuarenta, bandas, null, t, minutosJuego: 0.25));
            disparadoresUno.Observar(obsUno, bandasUno, null, t, minutosJuego: 0.25);
        }

        // 40 casillas de agua aportan al integrador exactamente lo mismo que 1 solo representante y quedan por debajo de deriva.
        Assert.Equal(disparadoresUno.ValorIntegrador, disparadoresCuarenta.ValorIntegrador, precision: 6);
        Assert.True(disparadoresCuarenta.ValorIntegrador < perfil.Umbrales.Deriva);
        Assert.DoesNotContain(disparos, d => d.Motivo == Motivos.Deriva);
    }
}
