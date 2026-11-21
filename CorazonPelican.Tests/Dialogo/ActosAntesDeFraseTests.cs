using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

/// <summary>
/// Guarda de R-07: una frase solo se muestra si todos sus actos son válidos (ADR-0003).
/// Verifica también la validación de cada acto del vocabulario y el orden
/// intención → actos → memoria → frase.
/// </summary>
public class ActosAntesDeFraseTests
{
    private static PerfilPersonaje PerfilConLugaresMedidos()
    {
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        perfil.Lexico.Lugares.Single(l => l.Id == "pasarela_lago").Area = new() { 40, 30, 1, 1 };
        perfil.Lexico.Lugares.Single(l => l.Id == "orilla_norte").Area = new() { 40, 18, 1, 1 };
        return perfil;
    }

    private static Observacion EscenaConInterlocutor(
        int dia = 68,
        int hora = 1700,
        int corazones = 9,
        IReadOnlyList<SucesoObservado>? sucesos = null)
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
                new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 29, new[] { "persona" }, Corazones: corazones),
            },
            sucesos);

    private static ContextoValidacionActos CrearContexto(
        EstadoPersonaje? estado = null,
        Observacion? observacion = null,
        PerfilPersonaje? perfil = null,
        LugaresOfrecidos? lugares = null,
        int? corazones = 9,
        IReadOnlyCollection<string>? objetosRegalo = null,
        Pulso? pulso = null)
    {
        var p = perfil ?? PerfilConLugaresMedidos();
        var obs = observacion ?? EscenaConInterlocutor(corazones: corazones ?? 9);
        return new ContextoValidacionActos(
            obs,
            p,
            lugares ?? p.Lexico.OfrecerLugares(_ => true),
            estado ?? new EstadoPersonaje(),
            InterlocutorRef: "farmer:1",
            CorazonesInterlocutor: corazones,
            ObjetosRegaloDisponibles: objetosRegalo,
            Pulso: pulso);
    }

    private static MotivoRechazo AfirmarMotivo(
        IReadOnlyList<MotivoRechazo> motivos,
        string codigoEsperado,
        string campoEsperado)
    {
        var motivo = Assert.Single(motivos, m => m.Codigo == codigoEsperado && m.Campo == campoEsperado);
        Assert.False(string.IsNullOrWhiteSpace(motivo.Codigo));
        Assert.False(string.IsNullOrWhiteSpace(motivo.Campo));
        Assert.False(string.IsNullOrWhiteSpace(motivo.Prosa));
        return motivo;
    }

    [Fact]
    public void Un_acto_invalido_junto_a_varios_validos_rechaza_todo_el_lote_impide_frase_y_no_muta_estado()
    {
        var estado = new EstadoPersonaje();
        estado.Portafolio.Add(new ObraPortafolio
        {
            ObraId = "leah-0001",
            Estado = EstadosObra.Terminada,
            Nota = "boceto del lago",
        });

        var contexto = CrearContexto(estado, corazones: 9);
        var respuesta = new Respuesta
        {
            Pensamiento = "Le doy el cuadro y quedamos mañana... aunque quizá en un sitio que no existe.",
            Frase = "Toma este cuadro, y nos vemos mañana junto al volcán.",
            Actos = new()
            {
                new Acto { Tipo = TiposActo.Regala, ObraId = "leah-0001", Para = "farmer:1" },
                new Acto
                {
                    Tipo = TiposActo.Promete,
                    Obra = "nueva",
                    Tema = "sauce con niebla",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1800 },
                },
                new Acto
                {
                    Tipo = TiposActo.Cita,
                    Lugar = "volcan_inventado",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1900 },
                },
            },
            Memoria = new MemoriaPropuesta
            {
                Recordar = new() { "Le regalé el cuadro a Ana." },
            },
        };

        var resultado = AplicadorTurnoDialogo.Aplicar(respuesta, contexto);

        Assert.False(resultado.Aceptado);
        Assert.Equal(Resultado.Rechazada, resultado.AResultadoContrato().Estado);
        Assert.Null(resultado.FraseVisible);
        Assert.Empty(resultado.EtapasAplicadas);
        Assert.Empty(resultado.ActosAplicados);
        Assert.Empty(resultado.CompromisosCreados);
        Assert.Empty(resultado.ObrasRegaladas);
        Assert.Empty(resultado.RecuerdosCreados);

        // El estado del personaje debe quedar completamente intacto.
        Assert.Equal(EstadosObra.Terminada, estado.Portafolio.Single().Estado);
        Assert.Null(estado.Portafolio.Single().Destinatario);
        Assert.Empty(estado.Compromisos);
        Assert.Empty(estado.Recuerdos);
        Assert.Null(estado.IntencionActiva);

        AfirmarMotivo(resultado.Motivos, "lugar_desconocido", "actos[2].lugar");
    }

    [Fact]
    public void Cita_con_lugar_fuera_del_lexico_o_area_pendiente_o_fecha_pasada_es_invalida()
    {
        var perfil = PerfilConLugaresMedidos();
        var soloMountain = perfil.Lexico.OfrecerLugares(l => l.Ubicacion == "Mountain");

        // Lugar fuera del apéndice ofrecido (R-14), aunque exista en el perfil.
        var citaFueraApendice = new Respuesta
        {
            Frase = "Nos vemos en la playa.",
            Actos = new()
            {
                new Acto
                {
                    Tipo = TiposActo.Cita,
                    Lugar = "playa",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1200 },
                },
            },
        };
        var resFuera = ValidadorActos.Validar(citaFueraApendice, CrearContexto(perfil: perfil, lugares: soloMountain));
        Assert.False(resFuera.EsValido);
        AfirmarMotivo(resFuera.Motivos, "lugar_desconocido", "actos[0].lugar");

        // Lugar con área pendiente [0,0,0,0].
        var perfilSinMedir = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        perfilSinMedir.Lexico.Lugares.Single(l => l.Id == "pasarela_lago").Area = new() { 0, 0, 0, 0 };
        var citaPendiente = new Respuesta
        {
            Actos = new()
            {
                new Acto
                {
                    Tipo = TiposActo.Cita,
                    Lugar = "pasarela_lago",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1200 },
                },
            },
        };
        var resPendiente = ValidadorActos.Validar(citaPendiente, CrearContexto(perfil: perfilSinMedir));
        Assert.False(resPendiente.EsValido);
        AfirmarMotivo(resPendiente.Motivos, "lugar_no_alcanzable", "actos[0].lugar");

        // Fecha pasada o igual al instante actual.
        foreach (var (dia, hora) in new[] { (68, 1700), (68, 1650), (67, 2200) })
        {
            var citaPasada = new Respuesta
            {
                Actos = new()
                {
                    new Acto
                    {
                        Tipo = TiposActo.Cita,
                        Lugar = "pasarela_lago",
                        Para = "farmer:1",
                        Cuando = new TiempoJuego { Dia = dia, Hora = hora },
                    },
                },
            };
            var resPasada = ValidadorActos.Validar(citaPasada, CrearContexto(perfil: perfil));
            Assert.False(resPasada.EsValido);
            AfirmarMotivo(resPasada.Motivos, "plazo_pasado", "actos[0].cuando");
        }
    }

    [Fact]
    public void Promete_sin_apertura_o_plazo_pasado_o_sin_tema_es_invalido()
    {
        // Con 2 corazones, Encargos == false en leah.json.
        var prometeSinApertura = new Respuesta
        {
            Frase = "Claro, te pinto el lago mañana.",
            Actos = new()
            {
                new Acto
                {
                    Tipo = TiposActo.Promete,
                    Obra = "nueva",
                    Tema = "el lago en otoño",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1800 },
                },
            },
        };
        var resApertura = AplicadorTurnoDialogo.Aplicar(prometeSinApertura, CrearContexto(corazones: 2));
        Assert.False(resApertura.Aceptado);
        Assert.Null(resApertura.FraseVisible);
        AfirmarMotivo(resApertura.Motivos, "apertura_insuficiente", "actos[0]");

        // Con apertura (3+ corazones), pero plazo pasado y tema vacío: devuelve ambos motivos.
        var prometeInvalido = new Respuesta
        {
            Actos = new()
            {
                new Acto
                {
                    Tipo = TiposActo.Promete,
                    Obra = "nueva",
                    Tema = "   ",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 68, Hora = 1700 },
                },
            },
        };
        var resDatos = ValidadorActos.Validar(prometeInvalido, CrearContexto(corazones: 3));
        Assert.False(resDatos.EsValido);
        AfirmarMotivo(resDatos.Motivos, "argumento_invalido", "actos[0].tema");
        AfirmarMotivo(resDatos.Motivos, "plazo_pasado", "actos[0].cuando");
    }

    [Fact]
    public void Ensena_valida_apertura_y_estado_de_obra()
    {
        var estado = new EstadoPersonaje();
        estado.Portafolio.Add(new ObraPortafolio { ObraId = "leah-0001", Estado = EstadosObra.EnCurso });
        estado.Portafolio.Add(new ObraPortafolio { ObraId = "leah-0002", Estado = EstadosObra.Regalada, Destinatario = "farmer:99" });

        // Con 1 corazón, Ensenar == false.
        var respuesta = new Respuesta
        {
            Frase = "Mira lo que estoy dibujando.",
            Actos = new() { new Acto { Tipo = TiposActo.Ensena, ObraId = "leah-0001", Para = "farmer:1" } },
        };
        var sinApertura = AplicadorTurnoDialogo.Aplicar(respuesta, CrearContexto(estado, corazones: 1));
        Assert.False(sinApertura.Aceptado);
        Assert.Null(sinApertura.FraseVisible);
        AfirmarMotivo(sinApertura.Motivos, "apertura_insuficiente", "actos[0]");

        // Con 3+ corazones y obra en_curso -> válido.
        var valido = AplicadorTurnoDialogo.Aplicar(respuesta, CrearContexto(estado, corazones: 3));
        Assert.True(valido.Aceptado);
        Assert.Equal("Mira lo que estoy dibujando.", valido.FraseVisible);

        // Obra ya regalada -> inválida para enseñar.
        var yaRegalada = new Respuesta
        {
            Actos = new() { new Acto { Tipo = TiposActo.Ensena, ObraId = "leah-0002" } },
        };
        var resRegalada = ValidadorActos.Validar(yaRegalada, CrearContexto(estado, corazones: 6));
        Assert.False(resRegalada.EsValido);
        AfirmarMotivo(resRegalada.Motivos, "obra_ya_regalada", "actos[0].obra_id");
    }

    [Fact]
    public void Rechaza_sin_motivo_y_acepta_regalo_sin_regalo_y_acto_desconocido_se_rechazan()
    {
        var respuesta = new Respuesta
        {
            Frase = "No y gracias.",
            Actos = new()
            {
                new Acto { Tipo = TiposActo.Rechaza, Motivo = "  " },
                new Acto { Tipo = TiposActo.AceptaRegalo },
                new Acto { Tipo = "inventar_acto" },
            },
        };

        var resultado = AplicadorTurnoDialogo.Aplicar(respuesta, CrearContexto());

        Assert.False(resultado.Aceptado);
        Assert.Null(resultado.FraseVisible);
        AfirmarMotivo(resultado.Motivos, "argumento_invalido", "actos[0].motivo");
        AfirmarMotivo(resultado.Motivos, "sin_regalo_ofrecido", "actos[1]");
        AfirmarMotivo(resultado.Motivos, "acto_desconocido", "actos[2].acto");

        // Con suceso de regalo del interlocutor y motivo explícito, ambos son válidos.
        var obsConRegalo = EscenaConInterlocutor(sucesos: new[]
        {
            new SucesoObservado("suceso:regalo:68:1:1", "regalo", "regalo recibido de Ana", ActorRef: "farmer:1"),
        });
        var respuestaValida = new Respuesta
        {
            Frase = "Gracias por la trufa, pero ahora no puedo hacer encargos.",
            Actos = new()
            {
                new Acto { Tipo = TiposActo.AceptaRegalo },
                new Acto { Tipo = TiposActo.Rechaza, Motivo = "tengo las manos llenas con el boceto del lago" },
            },
        };
        var resValida = AplicadorTurnoDialogo.Aplicar(respuestaValida, CrearContexto(observacion: obsConRegalo));
        Assert.True(resValida.Aceptado);
        Assert.Equal(respuestaValida.Frase, resValida.FraseVisible);

        // Con ObjetosRegaloDisponibles también se acepta acepta_regalo.
        var resConObjeto = ValidadorActos.Validar(
            new[] { new Acto { Tipo = TiposActo.AceptaRegalo } },
            CrearContexto(objetosRegalo: new[] { "trufa" }));
        Assert.True(resConObjeto.EsValido);
    }

    [Fact]
    public void Orden_intencion_actos_memoria_frase_se_respeta_y_detiene_ante_fallo()
    {
        var estado = new EstadoPersonaje();
        var contexto = CrearContexto(estado, corazones: 6);

        // 1. Si falla la intención (plan vacío), no se aplican actos, memoria ni frase.
        var fallaIntencion = new Respuesta
        {
            Intencion = new IntencionPropuesta
            {
                Objetivo = "quedar mañana",
                Porque = "me apetece pasear",
                Plan = new(),
            },
            Frase = "Nos vemos mañana en la pasarela.",
            Actos = new()
            {
                new Acto
                {
                    Tipo = TiposActo.Cita,
                    Lugar = "pasarela_lago",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1800 },
                },
            },
            Memoria = new MemoriaPropuesta { Recordar = new() { "Quedé con Ana." } },
        };

        var resFallaIntencion = AplicadorTurnoDialogo.Aplicar(fallaIntencion, contexto);
        Assert.False(resFallaIntencion.Aceptado);
        Assert.Null(resFallaIntencion.FraseVisible);
        Assert.Empty(resFallaIntencion.EtapasAplicadas);
        Assert.Null(estado.IntencionActiva);
        Assert.Empty(estado.Compromisos);
        Assert.Empty(estado.Recuerdos);

        // 2. Si intención es válida pero falla un acto, tampoco se aplica intención, memoria ni frase.
        var fallaActo = new Respuesta
        {
            Intencion = new IntencionPropuesta
            {
                Objetivo = "volver al lago",
                Porque = "sigue la buena luz",
                Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" } },
            },
            Frase = "Nos vemos luego.",
            Actos = new()
            {
                new Acto { Tipo = TiposActo.Rechaza, Motivo = "" },
            },
            Memoria = new MemoriaPropuesta { Recordar = new() { "No debería guardarse." } },
        };

        var resFallaActo = AplicadorTurnoDialogo.Aplicar(fallaActo, contexto);
        Assert.False(resFallaActo.Aceptado);
        Assert.Null(resFallaActo.FraseVisible);
        Assert.Empty(resFallaActo.EtapasAplicadas);
        Assert.Null(estado.IntencionActiva);
        Assert.Empty(estado.Compromisos);
        Assert.Empty(estado.Recuerdos);

        // 3. Cuando intención, actos y memoria son válidos, se aplican en orden exacto:
        //    intención → actos → memoria → frase.
        var todoValido = new Respuesta
        {
            Intencion = new IntencionPropuesta
            {
                Objetivo = "preparar un estudio del lago para Ana",
                Porque = "me pidió verlo con luz de otoño",
                PlazoJuego = new TiempoJuego { Dia = 69, Hora = 1900 },
                Plan = new() { new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" } },
            },
            Frase = "Mañana al atardecer te tengo un estudio del lago en la pasarela.",
            Actos = new()
            {
                new Acto
                {
                    Tipo = TiposActo.Promete,
                    Obra = "nueva",
                    Tema = "estudio del lago al atardecer",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1900 },
                },
                new Acto
                {
                    Tipo = TiposActo.Cita,
                    Lugar = "pasarela_lago",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1900 },
                },
            },
            Memoria = new MemoriaPropuesta
            {
                Recordar = new() { "Prometí a Ana un estudio del lago y quedamos en la pasarela." },
            },
        };

        var resValido = AplicadorTurnoDialogo.Aplicar(todoValido, contexto, idIntencion: "int-lago-1");
        Assert.True(resValido.Aceptado);
        Assert.Equal(
            new[] { EtapasTurno.Intencion, EtapasTurno.Actos, EtapasTurno.Memoria, EtapasTurno.Frase },
            resValido.EtapasAplicadas);
        Assert.Equal(todoValido.Frase, resValido.FraseVisible);
        Assert.NotNull(estado.IntencionActiva);
        Assert.Equal("int-lago-1", estado.IntencionActiva!.Id);
        Assert.Equal(EstadoIntencion.Activa, resValido.IntencionActivada!.Estado);
        Assert.Equal(2, estado.Compromisos.Count);
        Assert.Equal(TiposActo.Promete, estado.Compromisos[0].Tipo);
        Assert.Equal("pendiente", estado.Compromisos[0].Estado);
        Assert.Equal(TiposActo.Cita, estado.Compromisos[1].Tipo);
        Assert.Equal("pasarela_lago", estado.Compromisos[1].Objeto);
        Assert.Single(estado.Recuerdos);
    }
}
