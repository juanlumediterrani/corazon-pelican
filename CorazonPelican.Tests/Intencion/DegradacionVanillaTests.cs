using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

/// <summary>Guardas de degradación a la rutina vanilla (R-11, CP-303).</summary>
public class DegradacionVanillaTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static HorarioDia HorarioReferencia() => new("fall", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "Mountain", 40, 30),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
    });

    [Fact]
    public void Con_la_mente_apagada_FakeWorld_no_registra_ninguna_accion_del_motor_sobre_el_npc()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var horario = HorarioReferencia();

        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 4, deambula: true)
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: HoraJuego.InicioDia)
            .Construir(perfil.Lexico);

        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo)
        {
            Epoca = "epoca-r11",
            MenteHabilitada = false,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        var minutosPorPaso = FakeWorld.PasoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(mundo.Reloj.Modo);
        var pasosDia = 120 * 7 * 1000 / (int)FakeWorld.PasoMs;
        Pulso? primerPulso = null;

        for (var i = 0; i < pasosDia; i++)
        {
            var obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            var res = motor.ProcesarPaso(obs, mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, minutosPorPaso);
            primerPulso ??= res.Pulsos.FirstOrDefault();
        }

        Assert.NotNull(primerPulso);

        // Incluso si llegara una respuesta con plan mientras la mente está apagada, se rechaza sin tocar el cuerpo.
        var intento = motor.AplicarRespuesta(primerPulso!, new Respuesta
        {
            Epoca = primerPulso!.Epoca,
            BasadaEn = primerPulso.Id,
            Pensamiento = "Intento mover el cuerpo con la mente apagada.",
            Intencion = new IntencionPropuesta
            {
                Objetivo = "Ir a la orilla norte",
                Porque = "Probar guarda R-11",
                Plan = new List<PasoPlan>
                {
                    new() { Accion = "ir_a", Lugar = "orilla_norte" },
                },
            },
        });

        Assert.Equal(Resultado.Rechazada, intento.Estado);
        Assert.NotNull(intento.Motivos);
        Assert.Contains(intento.Motivos!, m => m.Codigo == "mente_deshabilitada");
        Assert.Empty(mundo.Cuerpo.Sucesos);
        Assert.False(mundo.Cuerpo.EstaReclamado);
        Assert.False(mundo.Cuerpo.Ocupado);
    }

    [Fact]
    public void Matar_el_proceso_de_la_mente_con_npc_reclamado_lo_devuelve_a_la_rutina_en_un_tramo_de_10_min_o_menos()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var horario = HorarioReferencia();

        var mundo = Escenario.Pasarela()
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 1300)
            .Construir(perfil.Lexico);

        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo)
        {
            Epoca = "epoca-caida",
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        var obsInicial = mundo.Observar();
        var forzado = motor.ForzarPulso(obsInicial, mundo.Reloj.MilisegundosReales, horario: horario);
        Assert.True(forzado.Emitido);

        // La mente reclama al personaje con un plan que lo lleva a orilla_norte y se queda dibujando.
        var resRespuesta = motor.AplicarRespuesta(forzado.Pulso!, new Respuesta
        {
            Epoca = forzado.Pulso!.Epoca,
            BasadaEn = forzado.Pulso.Id,
            Pensamiento = "Voy a acercarme a la orilla norte a dibujar.",
            Intencion = new IntencionPropuesta
            {
                Objetivo = "Dibujar en la orilla norte",
                Porque = "La luz entra limpia sobre el agua",
                Plan = new List<PasoPlan>
                {
                    new() { Accion = "ir_a", Lugar = "orilla_norte" },
                    new() { Accion = "dibujar", Obra = "nueva" },
                },
            },
        });
        Assert.Equal(Resultado.Aceptada, resRespuesta.Estado);

        // Avanzamos manteniendo vivo el latido de la mente hasta que llega a orilla_norte y empieza a dibujar.
        var minutosPorPaso = FakeWorld.PasoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(mundo.Reloj.Modo);
        while (mundo.Cuerpo.Continua != "dibujar")
        {
            var obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            motor.NotificarActividadMente(mundo.Reloj.MilisegundosReales);
            motor.ProcesarPaso(obs, mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, minutosPorPaso);
        }

        Assert.True(mundo.Cuerpo.EstaReclamado);
        Assert.Equal("dibujar", mundo.Cuerpo.Continua);
        Assert.Equal(64, mundo.Personaje.X);
        Assert.Equal(14, mundo.Personaje.Y);

        // Matamos el proceso de la mente (deja de enviar latidos/respuestas).
        // En <= 1 tramo de 10 min de juego (7 000 ms en modo Vanilla), debe degradar a rutina (Mountain 40,30).
        var instanteMuerteMs = mundo.Reloj.MilisegundosReales;
        var unTramoDiezMinMs = HoraJuego.MilisegundosPorDiezMinutos(mundo.Reloj.Modo);
        var pasosUnTramo = (int)(unTramoDiezMinMs / FakeWorld.PasoMs);

        for (var i = 0; i < pasosUnTramo; i++)
        {
            var obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            motor.ProcesarPaso(obs, mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, minutosPorPaso);
        }

        Assert.False(mundo.Cuerpo.EstaReclamado);
        Assert.Null(mundo.Cuerpo.Continua);
        Assert.Equal("mente_caida", motor.UltimoMotivoDegradacion);
        Assert.NotNull(motor.UltimaDegradacionMs);
        Assert.True(motor.UltimaDegradacionMs!.Value - instanteMuerteMs <= unTramoDiezMinMs);
        Assert.Equal("Mountain", mundo.Personaje.Ubicacion);
        Assert.Equal(40, mundo.Personaje.X);
        Assert.Equal(30, mundo.Personaje.Y);
    }

    [Fact]
    public void Cp_mente_off_a_mitad_de_una_intencion_cancela_el_plan_y_devuelve_al_horario()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var horario = HorarioReferencia();

        var mundo = Escenario.Pasarela()
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 1300)
            .Construir(perfil.Lexico);

        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo)
        {
            Epoca = "epoca-off",
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "sentarse", Lugar = "pasarela_lago" },
        }, "int-mitad");

        mundo.Avanzar(TimeSpan.FromSeconds(2));
        Assert.True(mundo.Cuerpo.Ocupado);
        Assert.True(mundo.Cuerpo.EstaReclamado);

        // Ejecutamos cp_mente off a mitad del plan.
        var degradado = motor.DesactivarMente(mundo.Reloj.MilisegundosReales);
        Assert.True(degradado);
        Assert.False(motor.MenteHabilitada);
        Assert.False(mundo.Cuerpo.Ocupado);
        Assert.False(mundo.Cuerpo.EstaReclamado);
        Assert.Equal("mente_deshabilitada", motor.UltimoMotivoDegradacion);
        Assert.Equal("Mountain", mundo.Personaje.Ubicacion);
        Assert.Equal(40, mundo.Personaje.X);
        Assert.Equal(30, mundo.Personaje.Y);
    }

    [Fact]
    public void Error_en_modulo_o_presupuesto_agotado_devuelve_al_personaje_reclamado_a_su_rutina()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var horario = HorarioReferencia();

        // 1. Sin presupuesto ordinario disponible tras concluir los pasos activos -> degrada por sin_presupuesto.
        var limiteOrdinario = perfil.Presupuesto.Vanilla.PulsosPorDiaJuego - perfil.Presupuesto.Vanilla.ReservadosCadenas;
        var estadoSinCupo = new EstadoPersonaje
        {
            Contadores = new ContadoresPulsos(Dia: 68, OrdinariosLibresGastados: limiteOrdinario, DespertarEmitido: true),
        };
        var mundoSinCupo = Escenario.Pasarela()
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 1400)
            .Construir(perfil.Lexico);
        var motorSinCupo = new MotorHabitante(perfil, plantillas, estadoSinCupo, mundoSinCupo.Reloj.Modo);
        motorSinCupo.ConectarEjecutorCuerpo(mundoSinCupo.Cuerpo.Ejecutor, () => mundoSinCupo.Reloj.MilisegundosReales);

        mundoSinCupo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "dibujar", Obra = "nueva" } }, "int-dibujo");
        Assert.True(mundoSinCupo.Cuerpo.EstaReclamado);

        var obs = mundoSinCupo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
        motorSinCupo.NotificarActividadMente(mundoSinCupo.Reloj.MilisegundosReales);
        motorSinCupo.ProcesarPaso(obs, mundoSinCupo.HorarioHoy, mundoSinCupo.Reloj.MilisegundosReales, 0.35);

        Assert.False(mundoSinCupo.Cuerpo.EstaReclamado);
        Assert.Equal("sin_presupuesto", motorSinCupo.UltimoMotivoDegradacion);

        // 2. Error en un módulo con personaje reclamado -> degrada inmediatamente por error_modulo.
        var estadoError = new EstadoPersonaje();
        var mundoError = Escenario.Pasarela()
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 1400)
            .Construir(perfil.Lexico);
        var motorError = new MotorHabitante(perfil, plantillas, estadoError, mundoError.Reloj.Modo);
        motorError.ConectarEjecutorCuerpo(mundoError.Cuerpo.Ejecutor, () => mundoError.Reloj.MilisegundosReales);

        mundoError.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" } }, "int-error");
        Assert.True(mundoError.Cuerpo.EstaReclamado);

        motorError.NotificarErrorModulo("excepción simulada", mundoError.Reloj.MilisegundosReales);
        Assert.False(mundoError.Cuerpo.EstaReclamado);
        Assert.False(mundoError.Cuerpo.Ocupado);
        Assert.Equal("error_modulo", motorError.UltimoMotivoDegradacion);
    }
}
