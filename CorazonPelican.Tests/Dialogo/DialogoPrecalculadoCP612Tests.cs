using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

/// <summary>
/// Pruebas del diálogo precalculado al detectar al jugador (sin pantallas de espera ni textos hardcoded),
/// modalidades de afirmación o pregunta con opciones + vuelta, y repetición en el mismo día (CP-612).
/// </summary>
public class DialogoPrecalculadoCP612Tests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Pregunta_precalculada_con_opciones_entrega_vuelta_al_elegir_y_repite_vuelta_en_el_mismo_dia_CP612()
    {
        var perfil = CargarPerfil();
        var motor = new MotorHabitante(
            perfil,
            CargarPlantillas(),
            new EstadoPersonaje(),
            ModoReloj.Pelican,
            publicarPulso: p =>
            {
                p.Id = 42;
                return p;
            })
        {
            Epoca = "ep-612",
            Idioma = "en",
            MenteHabilitada = true,
            AnticipacionHabilitada = true,
        };

        DialogoPreparado? notificado = null;
        motor.DialogoPreparadoDisponible += (_, dlg) => notificado = dlg;

        // Paso previo sin jugador en la habitación; luego entra el jugador directamente en rango de mención.
        var obsAntes = new Observacion(
            Dia: 3,
            Hora: 1020,
            Ubicacion: "LeahHouse",
            X: 3,
            Y: 7,
            Luz: 1.0,
            Clima: "soleado",
            Entidades: Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obsAntes, horario: null, ahoraMs: 500, minutosJuego: 0);

        var obsEntrada = new Observacion(
            Dia: 3,
            Hora: 1030,
            Ubicacion: "LeahHouse",
            X: 3,
            Y: 7,
            Luz: 1.0,
            Clima: "soleado",
            Entidades: new[]
            {
                new EntidadObservada(
                    "farmer:77",
                    CategoriaEntidad.Granjero,
                    "Alex",
                    "LeahHouse",
                    X: 6,
                    Y: 7,
                    Etiquetas: new[] { "persona" },
                    Corazones: 2),
            });

        var paso = motor.ProcesarPaso(obsEntrada, horario: null, ahoraMs: 1_000, minutosJuego: 0);
        var pulso = Assert.Single(paso.Pulsos, p => p.Motivo == Motivos.Interlocucion && p.Anticipado == true);
        Assert.Equal("en", pulso.Idioma);
        Assert.Equal("farmer:77", pulso.Interlocutor?.Ref);

        var respuesta = new Respuesta
        {
            Epoca = "ep-612",
            BasadaEn = pulso.Id,
            Pensamiento = "Alex just stepped into my cottage, let me ask what they think of the light.",
            Dialogo = new DialogoPreparado
            {
                Para = "farmer:77",
                Tipo = "pregunta",
                Texto = "Hey, do you think this corner gets enough light for sculpting?",
                Opciones = new List<OpcionDialogoPreparado>
                {
                    new() { Id = "yes", Texto = "Yes, morning light hits right there.", Respuesta = "Thought so too, it makes the wood grain pop." },
                    new() { Id = "outside", Texto = "You might get better light by the forest.", Respuesta = "Good point, I might head out there later." },
                },
            },
        };

        var res = motor.AplicarRespuesta(pulso, respuesta);
        Assert.Equal(Resultado.Aceptada, res.Estado);
        Assert.NotNull(notificado);

        // 1. Primera interacción: entrega pregunta + opciones al instante.
        var inst1 = motor.ConsultarDialogoInstantaneo(3, "farmer:77");
        Assert.Equal(ModoDialogoInstantaneo.Pregunta, inst1.Modo);
        Assert.Equal(2, inst1.Opciones?.Count);

        // Simulamos la sincronización hacia un cliente remoto en modData.
        var jsonPrep = ProtocoloMultijugador.SerializarPreparado(3, notificado!);
        var enCliente1 = ProtocoloMultijugador.ResolverDialogoDesdeInstantaneas(3, 77, jsonUltimo: null, jsonPreparado: jsonPrep);
        Assert.Equal(ModoDialogoInstantaneo.Pregunta, enCliente1.Modo);

        // 2. Jugador elige la segunda opción -> devuelve la vuelta exacta y queda registrada.
        var eleccion = motor.RegistrarConsumoDialogoInstantaneo(3, 1030, "farmer:77", "Alex", "outside", null, 2_000);
        Assert.True(eleccion.Aceptada);
        Assert.Equal("Good point, I might head out there later.", eleccion.Vuelta);

        // 3. Otra acción de diálogo en el mismo día devuelve únicamente la vuelta (último mensaje), no la pregunta.
        var inst2 = motor.ConsultarDialogoInstantaneo(3, "farmer:77");
        Assert.Equal(ModoDialogoInstantaneo.RepetirUltimaVuelta, inst2.Modo);
        Assert.Equal("Good point, I might head out there later.", inst2.Texto);

        var jsonUltimo = ProtocoloMultijugador.SerializarUltimoMensaje(3, eleccion.Vuelta);
        var enCliente2 = ProtocoloMultijugador.ResolverDialogoDesdeInstantaneas(3, 77, jsonUltimo: jsonUltimo, jsonPreparado: jsonPrep);
        Assert.Equal(ModoDialogoInstantaneo.RepetirUltimaVuelta, enCliente2.Modo);
        Assert.Equal("Good point, I might head out there later.", enCliente2.Texto);

        // 4. Si se acerca otro jugador en un día nuevo (sin diálogo precalculado todavía), cede a vanilla (Ninguno).
        var sinNada = motor.ConsultarDialogoInstantaneo(4, "farmer:77");
        Assert.Equal(ModoDialogoInstantaneo.Ninguno, sinNada.Modo);

        // 5. En el mismo día, si otro jugador ya terminó hoy, no vuelve a emitir anticipación redundante para ese jugador.
        var pasoRepetido = motor.ProcesarPaso(obsEntrada, horario: null, ahoraMs: 60_000, minutosJuego: 1);
        Assert.DoesNotContain(pasoRepetido.Pulsos, p => p.Anticipado == true);
    }
}
