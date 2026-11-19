using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

/// <summary>Guarda de R-16: cambios de rutina y agenda diaria validados contra el perfil y el horario vigente.</summary>
public class CambioRutinaTests
{
    private static PerfilPersonaje Leah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static readonly HorarioDia Viernes = new("Fri", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "LeahHouse", 6, 5),
        new EntradaHorario(1600, "Saloon", 2, 20),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2340, "LeahHouse", 2, 4, "leah_sleep"),
    });

    private static readonly HorarioDia Otono = new("fall", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "Town", 86, 66, "square_1_5_3"),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
    });

    private static void AfirmarRechazoEnDosFormas(IReadOnlyList<MotivoRechazo> motivos, string codigoEsperado, string? campoPrefijo = null)
    {
        Assert.NotEmpty(motivos);
        var motivo = Assert.Single(motivos, m => m.Codigo == codigoEsperado);
        Assert.False(string.IsNullOrWhiteSpace(motivo.Codigo));
        Assert.False(string.IsNullOrWhiteSpace(motivo.Campo));
        Assert.False(string.IsNullOrWhiteSpace(motivo.Prosa));
        if (campoPrefijo is not null)
        {
            Assert.StartsWith(campoPrefijo, motivo.Campo, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Agenda_se_construye_desde_horario_y_se_proyecta_al_contrato()
    {
        var agenda = AgendaDia.DesdeHorario(Viernes, Leah());

        Assert.Equal("Fri", agenda.ClaveHorario);
        Assert.Equal(5, agenda.Entradas.Count);
        Assert.All(agenda.Entradas, e => Assert.Equal(EstadoHito.Pendiente, e.Estado));
        Assert.Equal("tu mesa de esculpir", agenda.Buscar("Fri:900")!.Prosa);
        Assert.Equal("el Saloon", agenda.Buscar("Fri:1600")!.Prosa);

        var proyectada = agenda.Proyectar();
        Assert.Equal(5, proyectada.Count);
        Assert.Equal("Fri:1600", proyectada[2].Hito);
        Assert.Equal("el Saloon", proyectada[2].Prosa);
        Assert.Equal(EstadoHito.Pendiente, proyectada[2].Estado);
    }

    [Theory]
    [InlineData("Fri:1200", "saltar")]
    [InlineData("Fri:1900", "mover")]
    [InlineData("Fri:2340", "sustituir")]
    public void Hito_sin_cambios_permitidos_se_rechaza(string hito, string cambio)
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new()
            {
                Hito = hito,
                Cambio = cambio,
                Hora = cambio == "mover" ? 1300 : null,
                Plan = cambio == "sustituir" ? new() { new PasoPlan { Accion = "esperar", DuracionMin = 10 } } : null,
            },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, "cambio_no_permitido", "intencion.cambios_rutina[0].cambio");
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar(hito)!.Estado);
    }

    [Theory]
    [InlineData("Fri:900", "saltar")]
    [InlineData("Fri:900", "sustituir")]
    [InlineData("Fri:1600", "mover")]
    public void Tipo_de_cambio_no_permitido_por_la_anotacion_se_rechaza(string hito, string cambio)
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new()
            {
                Hito = hito,
                Cambio = cambio,
                Hora = cambio == "mover" ? 1700 : null,
                Plan = cambio == "sustituir" ? new() { new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" } } : null,
            },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, "cambio_no_permitido", "intencion.cambios_rutina[0].cambio");
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar(hito)!.Estado);
    }

    [Theory]
    [InlineData("festival")]
    [InlineData("lluvia")]
    [InlineData("boda")]
    public void Cambio_en_condicion_prohibida_nunca_se_rechaza(string condicion)
    {
        var perfil = Leah();
        perfil.Rutina.Anotaciones.Single(a => a.Id == "saloon").Limites!.Nunca = new() { condicion };
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68, condicionesActivas: new[] { condicion });
        var propuesta = new List<CambioRutina> { new() { Hito = "Fri:1600", Cambio = "saltar" } };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, "condicion_prohibida", "intencion.cambios_rutina[0]");
        Assert.Contains(condicion, motivos[0].Prosa, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar("Fri:1600")!.Estado);
    }

    [Fact]
    public void Exceso_de_max_por_semana_se_rechaza_y_se_libera_a_los_7_dias()
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var historial = new[] { new RegistroCambioRutina(DiaJuego: 62, AnotacionId: "saloon", Hito: "Fri:1600", Cambio: "saltar") };
        var contexto = new ContextoRutina(diaJuego: 68, historialCambios: historial);
        var propuesta = new List<CambioRutina> { new() { Hito = "Fri:1600", Cambio = "saltar" } };

        // Día 68 - día 62 = 6 < 7: sigue dentro de la semana de 7 días.
        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);
        AfirmarRechazoEnDosFormas(motivos, "limite_semanal", "intencion.cambios_rutina[0]");
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar("Fri:1600")!.Estado);

        // Día 69 - día 62 = 7: ya pasó una semana completa.
        contexto.AvanzarDia(69);
        var motivosSemanaNueva = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);
        Assert.Empty(motivosSemanaNueva);
        Assert.Equal(EstadoHito.Saltado, agenda.Buscar("Fri:1600")!.Estado);
    }

    [Fact]
    public void Dos_cambios_en_el_mismo_lote_que_superan_max_por_semana_se_rechazan_sin_aplicar_ninguno()
    {
        var perfil = Leah();
        var horario = new HorarioDia("Fri", new[]
        {
            new EntradaHorario(1400, "Saloon", 2, 20),
            new EntradaHorario(1600, "Saloon", 2, 20),
        });
        var agenda = AgendaDia.DesdeHorario(horario, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new() { Hito = "Fri:1400", Cambio = "saltar" },
            new() { Hito = "Fri:1600", Cambio = "saltar" },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, "limite_semanal", "intencion.cambios_rutina[1]");
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar("Fri:1400")!.Estado);
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar("Fri:1600")!.Estado);
        Assert.Empty(contexto.HistorialCambios);
    }

    [Theory]
    [InlineData("despertar", "saltar")]
    [InlineData("despertar", "mover")]
    [InlineData("acostarse", "saltar")]
    [InlineData("acostarse", "sustituir")]
    public void Cambios_sobre_despertar_o_acostarse_se_rechazan(string hitoMotor, string cambio)
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new()
            {
                Hito = hitoMotor,
                Cambio = cambio,
                Hora = cambio == "mover" ? 700 : null,
                Plan = cambio == "sustituir" ? new() { new PasoPlan { Accion = "esperar", DuracionMin = 10 } } : null,
            },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, "hito_del_motor", "intencion.cambios_rutina[0].hito");
    }

    [Fact]
    public void Cambio_sobre_hito_inexistente_en_horario_de_hoy_se_rechaza()
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Otono, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new() { Hito = "Fri:1600", Cambio = "saltar" },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, "hito_inexistente", "intencion.cambios_rutina[0].hito");
    }

    [Theory]
    [InlineData(null, "hora_requerida")]
    [InlineData(550, "hora_invalida")]
    [InlineData(960, "hora_invalida")]
    [InlineData(1275, "hora_invalida")]
    [InlineData(2600, "hora_invalida")]
    [InlineData(-10, "hora_invalida")]
    public void Mover_sin_hora_valida_se_rechaza(int? hora, string codigoEsperado)
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new() { Hito = "Fri:900", Cambio = "mover", Hora = hora },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        AfirmarRechazoEnDosFormas(motivos, codigoEsperado, "intencion.cambios_rutina[0].hora");
        Assert.Equal(900, agenda.Buscar("Fri:900")!.Hora);
        Assert.Equal(EstadoHito.Pendiente, agenda.Buscar("Fri:900")!.Estado);
    }

    [Fact]
    public void Mover_con_hora_valida_actualiza_la_hora_y_el_estado()
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);
        var propuesta = new List<CambioRutina>
        {
            new() { Hito = "Fri:900", Cambio = "mover", Hora = 1030 },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(propuesta, agenda, perfil, contexto);

        Assert.Empty(motivos);
        var esculpir = agenda.Buscar("Fri:900")!;
        Assert.Equal(EstadoHito.Movido, esculpir.Estado);
        Assert.Equal(1030, esculpir.Hora);
        Assert.Equal(900, esculpir.HoraOriginal);
    }

    [Fact]
    public void Sustituir_sin_plan_o_con_acciones_o_coordenadas_invalidas_se_rechaza()
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 68);

        var sinPlan = ValidadorCambiosRutina.Validar(
            new[] { new CambioRutina { Hito = "Fri:1600", Cambio = "sustituir", Plan = new() } },
            agenda, perfil, contexto);
        AfirmarRechazoEnDosFormas(sinPlan, "plan_requerido", "intencion.cambios_rutina[0].plan");

        var accionInvalida = ValidadorCambiosRutina.Validar(
            new[] { new CambioRutina { Hito = "Fri:1600", Cambio = "sustituir", Plan = new() { new PasoPlan { Accion = "teletransportarse", Lugar = "pasarela_lago" } } } },
            agenda, perfil, contexto);
        AfirmarRechazoEnDosFormas(accionInvalida, "accion_no_permitida", "intencion.cambios_rutina[0].plan[0].accion");

        var conCoordenadas = ValidadorCambiosRutina.Validar(
            new[] { new CambioRutina { Hito = "Fri:1600", Cambio = "sustituir", Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "12,30" } } } },
            agenda, perfil, contexto);
        AfirmarRechazoEnDosFormas(conCoordenadas, "lugar_invalido", "intencion.cambios_rutina[0].plan[0].lugar");

        var lugarDesconocido = ValidadorCambiosRutina.Validar(
            new[] { new CambioRutina { Hito = "Fri:1600", Cambio = "sustituir", Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "marte" } } } },
            agenda, perfil, contexto);
        AfirmarRechazoEnDosFormas(lugarDesconocido, "lugar_desconocido", "intencion.cambios_rutina[0].plan[0].lugar");
    }

    [Fact]
    public void Valida_agenda_propuesta_en_respuesta_de_despertar()
    {
        var perfil = Leah();
        var agenda = AgendaDia.DesdeHorario(Viernes, perfil);
        var contexto = new ContextoRutina(diaJuego: 69);
        var rutaEjemplo = Path.Combine(Contrato.Esquemas.Ejemplos("validos"), "respuesta--despertar.json");
        var respuestaValida = System.Text.Json.JsonSerializer.Deserialize<Respuesta>(File.ReadAllText(rutaEjemplo))!;

        var motivosValidos = ValidadorCambiosRutina.ValidarRespuestaDespertar(respuestaValida, agenda, perfil, contexto);
        Assert.Empty(motivosValidos);

        // Admite planes de intención permitidos en la agenda de despertar.
        var conPlanPermitido = ValidadorCambiosRutina.ValidarAgendaDespertar(
            new List<EstadoAgenda>
            {
                new() { Hito = "Fri:900", Estado = EstadoHito.Pendiente },
                new() { Hito = "int-lago", Estado = EstadoHito.Pendiente },
            },
            agenda,
            planesIntencionPermitidos: new[] { "int-lago" });
        Assert.Empty(conPlanPermitido);

        // Rechaza hitos inventados, del motor o marcados como cambiados sin cambio válido.
        var hitoInventado = ValidadorCambiosRutina.ValidarAgendaDespertar(
            new List<EstadoAgenda> { new() { Hito = "Fri:9999", Estado = EstadoHito.Pendiente } },
            agenda);
        AfirmarRechazoEnDosFormas(hitoInventado, "hito_inexistente", "agenda[0].hito");

        var hitoMotor = ValidadorCambiosRutina.ValidarAgendaDespertar(
            new List<EstadoAgenda> { new() { Hito = "despertar", Estado = EstadoHito.Pendiente } },
            agenda);
        AfirmarRechazoEnDosFormas(hitoMotor, "hito_del_motor", "agenda[0].hito");

        var cambioSinPermisoEnAgenda = ValidadorCambiosRutina.ValidarRespuestaDespertar(
            new Respuesta
            {
                Pensamiento = "Hoy me salto el mediodía.",
                Epoca = "e1",
                BasadaEn = 1,
                Agenda = new() { new EstadoAgenda { Hito = "Fri:1200", Estado = EstadoHito.Saltado } },
            },
            agenda,
            perfil,
            contexto);
        AfirmarRechazoEnDosFormas(cambioSinPermisoEnAgenda, "cambio_no_autorizado", "agenda[0].estado");
    }

    [Fact]
    public void FakeWorld_saltar_o_sustituir_un_hito_no_altera_los_posteriores_y_al_dia_siguiente_vuelve_la_rutina_canonica()
    {
        var perfil = Leah();
        var mundo = Escenario.Pasarela()
            .ALas(68, 600)
            .ConHorarios((dia, _) => dia == 68 ? Viernes : Viernes)
            .Construir();

        var agendaDia68 = AgendaDia.DesdeHorario(mundo.HorarioHoy, perfil);
        var contexto = new ContextoRutina(mundo.Reloj.Juego.Dia);

        var sustitucion = new List<CambioRutina>
        {
            new()
            {
                Hito = "Fri:1600",
                Cambio = "sustituir",
                Plan = new()
                {
                    new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" },
                    new PasoPlan { Accion = "dibujar", Obra = "nueva" },
                },
            },
        };

        var motivos = ValidadorCambiosRutina.ValidarYAplicar(sustitucion, agendaDia68, perfil, contexto);

        Assert.Empty(motivos);
        var sustituido = agendaDia68.Buscar("Fri:1600")!;
        Assert.Equal(EstadoHito.Sustituido, sustituido.Estado);
        Assert.Equal(2, sustituido.PlanSustituto!.Count);

        // Los hitos posteriores (19:00 y 23:40) permanecen intactos en la agenda de hoy.
        var posterior1900 = agendaDia68.Buscar("Fri:1900")!;
        var posterior2340 = agendaDia68.Buscar("Fri:2340")!;
        Assert.Equal(EstadoHito.Pendiente, posterior1900.Estado);
        Assert.Equal(1900, posterior1900.Hora);
        Assert.Null(posterior1900.PlanSustituto);
        Assert.Equal(EstadoHito.Pendiente, posterior2340.Estado);
        Assert.Equal(2340, posterior2340.Hora);
        Assert.Null(posterior2340.PlanSustituto);

        // Avanzamos en FakeWorld hasta el día siguiente (6:00 del día 69).
        mundo.Avanzar(TimeSpan.FromSeconds(120 * 7));
        Assert.Equal(69, mundo.Reloj.Juego.Dia);

        var agendaDia69 = AgendaDia.DesdeHorario(mundo.HorarioHoy, perfil);
        Assert.All(agendaDia69.Entradas, e =>
        {
            Assert.Equal(EstadoHito.Pendiente, e.Estado);
            Assert.Equal(e.HoraOriginal, e.Hora);
            Assert.Null(e.PlanSustituto);
        });
        Assert.Equal(EstadoHito.Pendiente, agendaDia69.Buscar("Fri:1600")!.Estado);

        // Y el historial semanal sigue recordando el cambio del día 68 para R-16.
        contexto.AvanzarDia(mundo.Reloj.Juego.Dia);
        var segundoIntento = ValidadorCambiosRutina.ValidarYAplicar(
            new[] { new CambioRutina { Hito = "Fri:1600", Cambio = "saltar" } },
            agendaDia69,
            perfil,
            contexto);
        AfirmarRechazoEnDosFormas(segundoIntento, "limite_semanal", "intencion.cambios_rutina[0]");
    }
}
