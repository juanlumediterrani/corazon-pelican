using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

/// <summary>
/// Guarda de R-06 (y R-13 / R-14): validación de precondiciones de intenciones, ciclo de vida completo,
/// caducidad con pulsión de fin y modulación de la saliencia por pesos de atención.
/// </summary>
public class ValidadorIntencionTests
{
    private static PerfilPersonaje LeahConLagoMedido()
    {
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        var pasarela = perfil.Lexico.Lugares.Single(l => l.Id == "pasarela_lago");
        pasarela.Area = new() { 40, 30, 1, 1 };
        var orilla = perfil.Lexico.Lugares.Single(l => l.Id == "orilla_norte");
        orilla.Area = new() { 40, 18, 1, 1 };
        return perfil;
    }

    private static Observacion EscenaLago(int dia = 68, int hora = 1700)
        => new(
            dia,
            hora,
            "Mountain",
            40,
            30,
            1.0,
            "soleado",
            new[]
            {
                new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" }),
                new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 24, new[] { "persona" }, Corazones: 6),
            },
            new[]
            {
                new SucesoObservado("suceso:regalo:farmer:1", "regalo", "Ana te ha ofrecido una trufa", ActorRef: "farmer:1"),
            });

    private static IntencionPropuesta PropuestaValida() => new()
    {
        Objetivo = "dibujar el lago al atardecer",
        Porque = "la luz en el agua está pidiendo un boceto rápido",
        PlazoJuego = new TiempoJuego { Dia = 68, Hora = 1900 },
        PesosAtencion = new Dictionary<string, double> { ["agua"] = 2.0, ["persona"] = 0.5 },
        Plan = new()
        {
            new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" },
            new PasoPlan { Accion = "sentarse", Lugar = "pasarela_lago" },
            new PasoPlan { Accion = "mirar", Objetivo = "agua:Mountain:lago" },
            new PasoPlan { Accion = "dibujar", Obra = "nueva" },
        },
    };

    private static MotivoRechazo AfirmarMotivo(ResultadoValidacionIntencion resultado, string codigoEsperado, string campoEsperado)
    {
        Assert.False(resultado.EsValido);
        var motivo = Assert.Single(resultado.Motivos, m => m.Codigo == codigoEsperado && m.Campo == campoEsperado);
        Assert.False(string.IsNullOrWhiteSpace(motivo.Codigo));
        Assert.False(string.IsNullOrWhiteSpace(motivo.Campo));
        Assert.False(string.IsNullOrWhiteSpace(motivo.Prosa));
        return motivo;
    }

    [Fact]
    public void Intencion_valida_con_precondiciones_ciertas_se_acepta()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();
        var propuesta = PropuestaValida();

        var resultado = ValidadorIntencion.Validar(propuesta, observacion, perfil);

        Assert.True(resultado.EsValido);
        Assert.Empty(resultado.Motivos);
        Assert.Equal(Resultado.Aceptada, resultado.AResultadoContrato().Estado);
    }

    [Fact]
    public void Lugar_inexistente_o_fuera_de_lugares_ofrecidos_se_rechaza_con_motivo()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();

        var inexistente = PropuestaValida();
        inexistente.Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "isla_imaginaria" } };
        AfirmarMotivo(ValidadorIntencion.Validar(inexistente, observacion, perfil), "lugar_desconocido", "intencion.plan[0].lugar");

        // R-14: aunque exista en el perfil, si no se ofreció en el apéndice de la escena, se rechaza.
        var soloMountain = perfil.Lexico.OfrecerLugares(l => l.Ubicacion == "Mountain");
        var noOfrecido = PropuestaValida();
        noOfrecido.Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "playa" } };
        AfirmarMotivo(ValidadorIntencion.Validar(noOfrecido, observacion, perfil, soloMountain), "lugar_desconocido", "intencion.plan[0].lugar");
    }

    [Fact]
    public void Lugar_con_area_pendiente_no_es_alcanzable()
    {
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        var observacion = EscenaLago();
        var propuesta = PropuestaValida();
        propuesta.Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" } };

        var resultado = ValidadorIntencion.Validar(propuesta, observacion, perfil);

        AfirmarMotivo(resultado, "lugar_no_alcanzable", "intencion.plan[0].lugar");
    }

    [Fact]
    public void Sentarse_en_lugar_no_sentable_se_rechaza()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();
        var propuesta = PropuestaValida();
        propuesta.Plan = new() { new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" } };

        var resultado = ValidadorIntencion.Validar(propuesta, observacion, perfil);

        AfirmarMotivo(resultado, "lugar_no_sentable", "intencion.plan[0].lugar");
    }

    [Theory]
    [InlineData(68, 1650)]
    [InlineData(68, 1700)]
    [InlineData(67, 2300)]
    public void Plazo_pasado_o_actual_se_rechaza_con_motivo(int diaPlazo, int horaPlazo)
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago(dia: 68, hora: 1700);
        var propuesta = PropuestaValida();
        propuesta.PlazoJuego = new TiempoJuego { Dia = diaPlazo, Hora = horaPlazo };

        var resultado = ValidadorIntencion.Validar(propuesta, observacion, perfil);

        AfirmarMotivo(resultado, "plazo_pasado", "intencion.plazo_juego");
    }

    [Fact]
    public void Plazo_nulo_o_futuro_es_valido()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago(dia: 68, hora: 1700);

        var sinPlazo = PropuestaValida();
        sinPlazo.PlazoJuego = null;
        Assert.True(ValidadorIntencion.Validar(sinPlazo, observacion, perfil).EsValido);

        var diaSiguiente = PropuestaValida();
        diaSiguiente.PlazoJuego = new TiempoJuego { Dia = 69, Hora = 700 };
        Assert.True(ValidadorIntencion.Validar(diaSiguiente, observacion, perfil).EsValido);
    }

    [Fact]
    public void Accion_fuera_del_perfil_o_inventada_se_rechaza_con_motivo()
    {
        var perfil = LeahConLagoMedido();
        perfil.Acciones.Remove("gesto");
        var observacion = EscenaLago();

        var fueraDePerfil = PropuestaValida();
        fueraDePerfil.Plan = new() { new PasoPlan { Accion = "gesto", Tipo = "saludo" } };
        AfirmarMotivo(ValidadorIntencion.Validar(fueraDePerfil, observacion, perfil), "accion_no_permitida", "intencion.plan[0].accion");

        var inventada = PropuestaValida();
        inventada.Plan = new() { new PasoPlan { Accion = "teletransportarse", Lugar = "pasarela_lago" } };
        AfirmarMotivo(ValidadorIntencion.Validar(inventada, observacion, perfil), "accion_no_permitida", "intencion.plan[0].accion");
    }

    [Fact]
    public void Mirar_objetivo_ausente_se_rechaza_y_objetivo_en_escena_se_acepta()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();

        var ausente = PropuestaValida();
        ausente.Plan = new() { new PasoPlan { Accion = "mirar", Objetivo = "npc:ausente" } };
        AfirmarMotivo(ValidadorIntencion.Validar(ausente, observacion, perfil), "objetivo_ausente", "intencion.plan[0].objetivo");

        var enSuceso = PropuestaValida();
        enSuceso.Plan = new() { new PasoPlan { Accion = "mirar", Objetivo = "suceso:regalo:farmer:1" } };
        Assert.True(ValidadorIntencion.Validar(enSuceso, observacion, perfil).EsValido);
    }

    [Fact]
    public void Plan_vacio_y_argumentos_faltantes_o_invalidos_se_rechazan()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();

        var planVacio = PropuestaValida();
        planVacio.Plan = new();
        AfirmarMotivo(ValidadorIntencion.Validar(planVacio, observacion, perfil), "plan_vacio", "intencion.plan");

        var pasosInvalidos = new (PasoPlan Paso, string CampoEsperado)[]
        {
            (new PasoPlan { Accion = "ir_a" }, "intencion.plan[0].lugar"),
            (new PasoPlan { Accion = "ir_a", Lugar = "12,30" }, "intencion.plan[0].lugar"),
            (new PasoPlan { Accion = "mirar" }, "intencion.plan[0].objetivo"),
            (new PasoPlan { Accion = "mirar", Objetivo = "12,30" }, "intencion.plan[0].objetivo"),
            (new PasoPlan { Accion = "dibujar" }, "intencion.plan[0].obra"),
            (new PasoPlan { Accion = "dibujar", Obra = "mi cuadro bonito" }, "intencion.plan[0].obra"),
            (new PasoPlan { Accion = "gesto" }, "intencion.plan[0].tipo"),
            (new PasoPlan { Accion = "gesto", Tipo = "voltereta" }, "intencion.plan[0].tipo"),
            (new PasoPlan { Accion = "murmurar", Texto = "   " }, "intencion.plan[0].texto"),
            (new PasoPlan { Accion = "esperar" }, "intencion.plan[0].duracion_min"),
            (new PasoPlan { Accion = "esperar", DuracionMin = 0 }, "intencion.plan[0].duracion_min"),
            (new PasoPlan { Accion = "esperar", DuracionMin = 5 }, "intencion.plan[0].duracion_min"),
        };

        foreach (var (paso, campo) in pasosInvalidos)
        {
            var propuesta = PropuestaValida();
            propuesta.Plan = new() { paso };
            AfirmarMotivo(ValidadorIntencion.Validar(propuesta, observacion, perfil), "argumento_invalido", campo);
        }

        var obraDesconocida = PropuestaValida();
        obraDesconocida.Plan = new() { new PasoPlan { Accion = "dibujar", Obra = "leah-0099" } };
        AfirmarMotivo(
            ValidadorIntencion.Validar(obraDesconocida, observacion, perfil, obrasConocidas: new[] { "leah-0001" }),
            "obra_desconocida",
            "intencion.plan[0].obra");
    }

    [Fact]
    public void Validar_y_crear_transiciona_a_validada_o_rechazada_y_valida_cambios_rutina()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();
        var horario = new HorarioDia("Fri", new[]
        {
            new EntradaHorario(1600, "Saloon", 2, 20),
            new EntradaHorario(2340, "LeahHouse", 2, 4, "leah_sleep"),
        });
        var agenda = AgendaDia.DesdeHorario(horario, perfil);
        var contexto = new ContextoRutina(observacion.Dia);

        var propuesta = PropuestaValida();
        propuesta.CambiosRutina = new() { new CambioRutina { Hito = "Fri:1600", Cambio = "saltar" } };

        var (valida, resValido) = ValidadorIntencion.ValidarYCrear("int-1", propuesta, observacion, perfil, agenda: agenda, contextoRutina: contexto);
        Assert.True(resValido.EsValido);
        Assert.Equal(EstadoIntencion.Validada, valida!.Estado);

        var propuestaMala = PropuestaValida();
        propuestaMala.CambiosRutina = new() { new CambioRutina { Hito = "despertar", Cambio = "saltar" } };

        var (rechazada, resRechazo) = ValidadorIntencion.ValidarYCrear("int-2", propuestaMala, observacion, perfil, agenda: agenda, contextoRutina: contexto);
        Assert.False(resRechazo.EsValido);
        Assert.Equal(EstadoIntencion.Rechazada, rechazada!.Estado);
        Assert.True(rechazada.EsTerminal);
        Assert.Contains(rechazada.MotivosRechazo, m => m.Codigo == "hito_del_motor");
    }

    [Fact]
    public void Ciclo_de_vida_cubre_todos_los_estados_y_bloquea_transiciones_invalidas_o_posteriores_a_terminal()
    {
        var propuesta = PropuestaValida();

        var cumplida = IntencionPersonaje.DesdePropuesta("int-c", propuesta);
        Assert.Equal(EstadoIntencion.Propuesta, cumplida.Estado);
        cumplida.MarcarValidada();
        cumplida.Activar();
        cumplida.Cumplir();
        Assert.Equal(EstadoIntencion.Cumplida, cumplida.Estado);
        Assert.True(cumplida.EsTerminal);
        Assert.Throws<InvalidOperationException>(() => cumplida.Sustituir());

        var bloqueada = IntencionPersonaje.DesdePropuesta("int-b", propuesta);
        bloqueada.MarcarValidada();
        bloqueada.Activar();
        bloqueada.Bloquear();
        Assert.Equal(EstadoIntencion.Bloqueada, bloqueada.Estado);
        Assert.True(bloqueada.EsTerminal);

        var sustituida = IntencionPersonaje.DesdePropuesta("int-s", propuesta);
        sustituida.MarcarValidada();
        sustituida.Activar();
        sustituida.Sustituir();
        Assert.Equal(EstadoIntencion.Sustituida, sustituida.Estado);
        Assert.True(sustituida.EsTerminal);

        var rechazadaDesdeValidada = IntencionPersonaje.DesdePropuesta("int-rv", propuesta);
        rechazadaDesdeValidada.MarcarValidada();
        rechazadaDesdeValidada.Rechazar();
        Assert.Equal(EstadoIntencion.Rechazada, rechazadaDesdeValidada.Estado);
        Assert.True(rechazadaDesdeValidada.EsTerminal);

        // No puede saltar pasos ni pasar de activa a validada/rechazada.
        var enPropuesta = IntencionPersonaje.DesdePropuesta("int-inv", propuesta);
        Assert.Throws<InvalidOperationException>(() => enPropuesta.Activar());
        Assert.Throws<InvalidOperationException>(() => enPropuesta.Cumplir());
        enPropuesta.MarcarValidada();
        Assert.Throws<InvalidOperationException>(() => enPropuesta.Cumplir());
        enPropuesta.Activar();
        Assert.Throws<InvalidOperationException>(() => enPropuesta.Rechazar());
    }

    [Fact]
    public void Intencion_activa_con_plazo_vencido_pasa_a_caducada_y_emite_pulsion_de_fin()
    {
        var perfil = LeahConLagoMedido();
        var disparadores = new Disparadores(perfil);
        var antes = EscenaLago(dia: 68, hora: 1850);
        var (intencion, resultado) = ValidadorIntencion.ValidarYCrear("int-plazo", PropuestaValida(), antes, perfil);
        Assert.True(resultado.EsValido);
        intencion!.Activar();

        // Primer observar antes del plazo: no caduca ni emite fin_intencion.
        var disparosAntes = disparadores.Observar(antes, Array.Empty<EntidadEnBanda>(), null, ahoraMs: 1_000, minutosJuego: 10);
        Assert.False(intencion.CaducarSiVencida(antes, disparadores, ahoraMs: 1_250));
        Assert.Equal(EstadoIntencion.Activa, intencion.Estado);
        Assert.DoesNotContain(disparosAntes, d => d.Motivo == Motivos.FinIntencion);

        // Al alcanzar el plazo (día 68, 19:00), caduca y programa la pulsión fin_intencion para el siguiente paso.
        var enPlazo = EscenaLago(dia: 68, hora: 1900);
        Assert.True(intencion.CaducarSiVencida(enPlazo, disparadores, ahoraMs: 2_000));
        Assert.Equal(EstadoIntencion.Caducada, intencion.Estado);
        Assert.True(intencion.EsTerminal);

        var disparosFin = disparadores.Observar(enPlazo, Array.Empty<EntidadEnBanda>(), null, ahoraMs: 2_250, minutosJuego: 10);
        var fin = Assert.Single(disparosFin, d => d.Motivo == Motivos.FinIntencion);
        Assert.Equal("int-plazo", fin.Ref);
        Assert.Equal("caducada", fin.Detalle);
    }

    [Fact]
    public void Pesos_atencion_de_la_intencion_activa_modulan_realmente_la_saliencia()
    {
        var perfil = LeahConLagoMedido();
        var observacion = EscenaLago();
        var lago = observacion.Entidades.Single(e => e.Ref == "agua:Mountain:lago");
        var granjera = observacion.Entidades.Single(e => e.Ref == "farmer:1");

        var (intencion, _) = ValidadorIntencion.ValidarYCrear("int-sal", PropuestaValida(), observacion, perfil);
        Assert.NotNull(intencion);

        // Mientras no está activa, la capa de intención en Saliencia vale 1.
        var contextoPropuesta = ContextoSaliencia.DesdePerfil(perfil, EstadoAtencion.Abierta, intencion);
        var baseLago = Saliencia.Calcular(lago, observacion.X, observacion.Y, contextoPropuesta, novedad: 1.0);
        var basePersona = Saliencia.Calcular(granjera, observacion.X, observacion.Y, contextoPropuesta, novedad: 1.0);
        Assert.Equal(1.0, baseLago.Intencion);
        Assert.Equal(1.0, basePersona.Intencion);

        // Al activarla, agua sube (×2.0) y persona baja (×0.5).
        intencion!.Activar();
        var contextoActiva = contextoPropuesta.ConIntencion(intencion);
        var activaLago = Saliencia.Calcular(lago, observacion.X, observacion.Y, contextoActiva, novedad: 1.0);
        var activaPersona = Saliencia.Calcular(granjera, observacion.X, observacion.Y, contextoActiva, novedad: 1.0);

        Assert.Equal(2.0, activaLago.Intencion);
        Assert.Equal(0.5, activaPersona.Intencion);
        Assert.True(activaLago.Total > baseLago.Total);
        Assert.True(activaPersona.Total < basePersona.Total);

        // Al terminar, la modulación vuelve a 1.
        intencion.Cumplir();
        var contextoFinal = intencion.AplicarAContexto(contextoActiva);
        Assert.Equal(1.0, Saliencia.Calcular(lago, observacion.X, observacion.Y, contextoFinal, novedad: 1.0).Intencion);
    }
}
