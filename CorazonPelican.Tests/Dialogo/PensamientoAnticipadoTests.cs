using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

public class PensamientoAnticipadoTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static Observacion CrearObservacionConGranjero(int dia, int hora, string ubicacion, string clima, int? granjeroX, int? granjeroY)
    {
        var entidades = granjeroX is int gx && granjeroY is int gy
            ? new[]
            {
                new EntidadObservada(
                    "farmer:1",
                    CategoriaEntidad.Granjero,
                    "Álex",
                    ubicacion,
                    gx,
                    gy,
                    new[] { "vecino" },
                    Corazones: 2),
            }
            : Array.Empty<EntidadObservada>();

        return new Observacion(
            dia,
            hora,
            ubicacion,
            X: 33,
            Y: 25,
            Luz: 1.0,
            Clima: clima,
            Entidades: entidades);
    }

    [Fact]
    public void Jugador_que_se_acerca_emite_pulso_anticipado_y_hablar_reutiliza_respuesta_sin_nuevo_pulso()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var pulsosEmitidos = new List<Pulso>();
        long nextId = 1;

        var motor = new MotorHabitante(perfil, plantillas, estado, ModoReloj.Vanilla, publicarPulso: p =>
        {
            p.Id = nextId++;
            pulsosEmitidos.Add(p);
            return p;
        })
        {
            Epoca = "epoca-cp507",
            MenteHabilitada = true,
            AnticipacionHabilitada = true,
        };

        // Paso 1: el granjero aparece lejos (saliencia baja, sin anticipación todavía).
        var obsLejos = CrearObservacionConGranjero(dia: 10, hora: 1000, "Forest", "soleado", granjeroX: 33, granjeroY: 42);
        var paso1 = motor.ProcesarPaso(obsLejos, horario: null, ahoraMs: 1_000, minutosJuego: 0);
        Assert.DoesNotContain(paso1.Pulsos, p => p.Anticipado == true);

        // Paso 2: el granjero se acerca lo suficiente para subir hacia el pico -> emite pulso anticipado.
        var obsAcercandose = CrearObservacionConGranjero(dia: 10, hora: 1000, "Forest", "soleado", granjeroX: 33, granjeroY: 28);
        var paso2 = motor.ProcesarPaso(obsAcercandose, horario: null, ahoraMs: 2_000, minutosJuego: 0);
        var pulsoAnticipado = Assert.Single(paso2.Pulsos, p => p.Motivo == Motivos.Interlocucion && p.Anticipado == true);
        Assert.Equal("farmer:1", pulsoAnticipado.Interlocutor?.Ref);
        Assert.Equal(1, motor.Disparadores.Contadores?.Anticipaciones);
        Assert.Equal(1, motor.Disparadores.Contadores?.OrdinariosLibresGastados);

        // La mente responde antes de que el jugador pulse hablar.
        var resMente = motor.AplicarRespuesta(pulsoAnticipado, new Respuesta
        {
            Epoca = "epoca-cp507",
            BasadaEn = pulsoAnticipado.Id,
            Frase = "Qué bien que te acerques al claro hoy.",
        });
        Assert.Equal(Resultado.Aceptada, resMente.Estado);
        Assert.True(motor.Dialogo.TieneRespuestaAnticipada);

        // Paso 3: el jugador habla con Leah -> se reutiliza el pulso y la frase anticipada sin nuevo pulso.
        var totalAntesHablar = pulsosEmitidos.Count;
        var reaccion = motor.IniciarTurnoDialogo(
            obsAcercandose,
            "farmer:1",
            "Álex",
            ahoraMs: 2_500);

        Assert.True(reaccion.ReutilizoAnticipado);
        Assert.Equal(pulsoAnticipado.Id, reaccion.Pulso?.Id);
        Assert.Equal("Qué bien que te acerques al claro hoy.", reaccion.FraseAnticipada);
        Assert.Equal(totalAntesHablar, pulsosEmitidos.Count);
        Assert.False(motor.Dialogo.TieneRespuestaAnticipada);
    }

    [Fact]
    public void Jugador_que_pasa_de_largo_cancela_anticipacion_y_descarta_respuesta()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var api = new ApiMotor();
        api.IniciarEpoca("epoca-cp507-cancel", new[] { perfil.Id });

        long nextId = 1;
        var motor = new MotorHabitante(perfil, plantillas, estado, ModoReloj.Vanilla, publicarPulso: p =>
        {
            p.Id = nextId++;
            return p;
        })
        {
            Epoca = "epoca-cp507-cancel",
            MenteHabilitada = true,
            AnticipacionHabilitada = true,
        };
        motor.AnticipacionCancelada += (pulsoId, interlocutorRef, motivo) =>
        {
            api.RegistrarEvento(
                "anticipacion_cancelada",
                perfil.Id,
                $"Anticipación cancelada ({motivo}) para {interlocutorRef}",
                new Dictionary<string, object?>
                {
                    ["pulso_id"] = pulsoId,
                    ["interlocutor"] = interlocutorRef,
                    ["motivo"] = motivo,
                });
        };

        var obsLejos = CrearObservacionConGranjero(dia: 10, hora: 1000, "Forest", "soleado", granjeroX: 33, granjeroY: 42);
        motor.ProcesarPaso(obsLejos, horario: null, ahoraMs: 1_000, minutosJuego: 0);

        var obsAcercandose = CrearObservacionConGranjero(dia: 10, hora: 1000, "Forest", "soleado", granjeroX: 33, granjeroY: 28);
        var paso2 = motor.ProcesarPaso(obsAcercandose, horario: null, ahoraMs: 2_000, minutosJuego: 0);
        var pulsoAnticipado = Assert.Single(paso2.Pulsos, p => p.Anticipado == true);

        Assert.Equal(Resultado.Aceptada, motor.AplicarRespuesta(pulsoAnticipado, new Respuesta
        {
            Epoca = "epoca-cp507-cancel",
            BasadaEn = pulsoAnticipado.Id,
            Frase = "qué bien verte por el bosque.",
        }).Estado);
        Assert.True(motor.Dialogo.TieneRespuestaAnticipada);

        // El jugador pasa de largo y abandona la escena sin hablar.
        var obsSinJugador = CrearObservacionConGranjero(dia: 10, hora: 1010, "Forest", "soleado", granjeroX: null, granjeroY: null);
        motor.ProcesarPaso(obsSinJugador, horario: null, ahoraMs: 4_000, minutosJuego: 10);

        Assert.Null(motor.Dialogo.PulsoAnticipadoActual);
        Assert.False(motor.Dialogo.TieneRespuestaAnticipada);

        var eventos = api.ObtenerEventos();
        var cancelacion = Assert.Single(eventos.Eventos, e => e.Tipo == "anticipacion_cancelada");
        Assert.Equal(perfil.Id, cancelacion.Personaje);
        Assert.Equal("interlocutor_alejado", cancelacion.Datos?["motivo"]?.ToString());

        // La anticipación gastada queda contabilizada explícitamente en el presupuesto del día.
        Assert.Equal(1, motor.Disparadores.Contadores?.Anticipaciones);
        Assert.Equal(1, motor.Disparadores.Contadores?.OrdinariosLibresGastados);
    }

    [Fact]
    public void Cambio_relevante_de_escena_invalida_pulso_anticipado_y_emite_uno_nuevo()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var pulsosEmitidos = new List<Pulso>();
        long nextId = 1;

        var motor = new MotorHabitante(perfil, plantillas, estado, ModoReloj.Vanilla, publicarPulso: p =>
        {
            p.Id = nextId++;
            pulsosEmitidos.Add(p);
            return p;
        })
        {
            Epoca = "epoca-cp507-escena",
            MenteHabilitada = true,
            AnticipacionHabilitada = true,
        };

        var obsLejos = CrearObservacionConGranjero(dia: 10, hora: 1000, "Forest", "soleado", granjeroX: 33, granjeroY: 42);
        motor.ProcesarPaso(obsLejos, horario: null, ahoraMs: 1_000, minutosJuego: 0);

        var obsAcercandose = CrearObservacionConGranjero(dia: 10, hora: 1000, "Forest", "soleado", granjeroX: 33, granjeroY: 28);
        var paso2 = motor.ProcesarPaso(obsAcercandose, horario: null, ahoraMs: 2_000, minutosJuego: 0);
        var pulsoAnticipado = Assert.Single(paso2.Pulsos, p => p.Anticipado == true);

        // Cambia la ubicación antes de hablar: la escena cambió de forma relevante.
        var obsOtroMapa = CrearObservacionConGranjero(dia: 10, hora: 1010, "Town", "soleado", granjeroX: 33, granjeroY: 26);
        var reaccion = motor.IniciarTurnoDialogo(
            obsOtroMapa,
            "farmer:1",
            "Álex",
            ahoraMs: 2_500);

        Assert.False(reaccion.ReutilizoAnticipado);
        Assert.NotNull(reaccion.Pulso);
        Assert.NotEqual(pulsoAnticipado.Id, reaccion.Pulso!.Id);
        Assert.False(reaccion.Pulso.Anticipado);
    }
}
