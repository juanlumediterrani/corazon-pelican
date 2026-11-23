using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Integracion;

/// <summary>
/// Pruebas de la iniciativa esporádica y afectiva del personaje para acercarse al jugador (CP-613).
/// Verifica: relación + humor + circunstancia, frenos anti-recurrencia, rechazo si no fue habilitada,
/// entrega al llegar y corte elegante si el jugador se aleja.
/// </summary>
public class IniciativaSocialCP613Tests
{
    private sealed class MediosAcercamientoFake : IMediosCuerpo
    {
        public string UbicacionActual => "LeahHouse";
        public int CasillaX => 3;
        public int CasillaY => 7;
        public bool EstaReclamado { get; private set; }
        public bool EstaEnAnimacionActividad { get; private set; }
        public EstadoRutaCuerpo EstadoRutaActual => EstadoRutaCuerpo.Inactiva;
        public string? MotivoBloqueoRuta => null;
        public long DuracionAsentamientoSentarseMs => 0;

        public EstadoAcercamientoJugador SiguienteEstado { get; set; } = EstadoAcercamientoJugador.EnCurso;
        public int VecesIniciarAcercamiento { get; private set; }
        public int VecesVolverARutina { get; private set; }

        public void ReclamarControl(string motivo) => this.EstaReclamado = true;
        public bool IniciarRutaHacia(Lugar destino, int orientacionFinal) => false;
        public void DetenerMovimiento() { }
        public bool AdoptarPosturaSentada(Lugar destino, int orientacion) => false;
        public bool IntentarMirarObjetivo(string objetivoRef) => false;
        public bool PuedeDibujar(out string? motivoBloqueo) { motivoBloqueo = null; return true; }
        public bool IniciarDibujo(string? obraId) => false;
        public void DetenerAnimacionActividad() => this.EstaEnAnimacionActividad = false;
        public bool HacerGesto(string tipo) => true;
        public bool Murmurar(string texto) => true;

        public bool VolverARutina()
        {
            this.VecesVolverARutina++;
            this.EstaReclamado = false;
            return true;
        }

        public bool AplicarCambioHorario(EntradaAgendaDia entrada) => true;
        public void RestaurarHorarioCanonico() { }

        public bool IniciarAcercamientoAJugador(string objetivoRef)
        {
            this.VecesIniciarAcercamiento++;
            return this.SiguienteEstado != EstadoAcercamientoJugador.CanceladoFueraDeAlcance;
        }

        public EstadoAcercamientoJugador EvaluarAcercamientoAJugador(string objetivoRef, int maxDistanciaCasillas)
            => this.SiguienteEstado;
    }

    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void EvaluadorIniciativaSocial_RespondeAHumorRelacionYCircunstanciaYRespetaEnfriamientos_CP613()
    {
        var perfil = CargarPerfil();
        var estado = new EstadoPersonaje();

        // Desconocido sin compromiso ni suceso (0 corazones) -> no dispara iniciativa.
        var obsDesconocido = new Observacion(
            Dia: 10,
            Hora: 1100,
            Ubicacion: "Forest",
            X: 33,
            Y: 25,
            Luz: 1.0,
            Clima: "soleado",
            Entidades: new[]
            {
                new EntidadObservada("farmer:5", CategoriaEntidad.Granjero, "Sam", "Forest", 33, 31, Array.Empty<string>(), Corazones: 0),
            });
        Assert.Empty(EvaluadorIniciativaSocial.Evaluar(obsDesconocido, estado, perfil, EstadoAtencion.Abierta));

        // Con vínculo (6 corazones), días sin hablar y humor vigente -> sí habilita iniciativa con motivo legible.
        estado.Humor = new EstadoHumor
        {
            Resumen = "me salió justo la línea del agua que buscaba",
            IntensidadInicial = 0.80,
            OrigenDiario = new List<string> { "ep-1" },
            CreadoEn = new TiempoJuego { Dia = 10, Hora = 900 },
        };
        estado.Relaciones["farmer:5"] = new RelacionEstado
        {
            Impresion = "buen amigo",
            UltimaVez = new TiempoJuego { Dia = 5, Hora = 1500 },
        };

        var candidatos = EvaluadorIniciativaSocial.Evaluar(obsDesconocido with
        {
            Entidades = new[]
            {
                new EntidadObservada("farmer:5", CategoriaEntidad.Granjero, "Sam", "Forest", 33, 33, Array.Empty<string>(), Corazones: 6),
            },
        }, estado, perfil, EstadoAtencion.Abierta);

        var candidato = Assert.Single(candidatos);
        Assert.Equal("farmer:5", candidato.InterlocutorRef);
        Assert.Contains("Sam", candidato.MotivoProsa);

        // Registrar iniciativa en el día 10 -> bloquea en día 11 y día 12 por enfriamiento global (3 días).
        EvaluadorIniciativaSocial.RegistrarIniciativa(estado, dia: 10, "farmer:5");
        Assert.Empty(EvaluadorIniciativaSocial.Evaluar(obsDesconocido with { Dia = 11 }, estado, perfil, EstadoAtencion.Abierta));
        Assert.Empty(EvaluadorIniciativaSocial.Evaluar(obsDesconocido with { Dia = 12 }, estado, perfil, EstadoAtencion.Abierta));

        // En día 13 pasó enfriamiento global (3 días), pero sigue activo enfriamiento por jugador (5 días).
        Assert.Empty(EvaluadorIniciativaSocial.Evaluar(obsDesconocido with
        {
            Dia = 13,
            Entidades = new[]
            {
                new EntidadObservada("farmer:5", CategoriaEntidad.Granjero, "Sam", "Forest", 33, 33, Array.Empty<string>(), Corazones: 6),
            },
        }, estado, perfil, EstadoAtencion.Abierta));
    }

    [Fact]
    public void AcercarseA_OfreceImpulsoEnPulsoEntregaAlLlegarYYaBloqueaSiJugadorSeAleja_CP613()
    {
        var perfil = CargarPerfil();
        var pulsos = new List<Pulso>();
        long nextId = 1;
        var estado = new EstadoPersonaje
        {
            Humor = new EstadoHumor
            {
                Resumen = "con muchas ganas de compartir el boceto",
                IntensidadInicial = 0.85,
                OrigenDiario = new List<string> { "ep-1" },
                CreadoEn = new TiempoJuego { Dia = 10, Hora = 900 },
            },
        };
        estado.Relaciones["farmer:7"] = new RelacionEstado { UltimaVez = new TiempoJuego { Dia = 4, Hora = 1200 } };

        var motor = new MotorHabitante(
            perfil,
            CargarPlantillas(),
            estado,
            ModoReloj.Pelican,
            publicarPulso: p =>
            {
                p.Id = nextId++;
                pulsos.Add(p);
                return p;
            })
        {
            Epoca = "ep-613",
            MenteHabilitada = true,
            AnticipacionHabilitada = true,
        };

        var medios = new MediosAcercamientoFake();
        var ejecutor = new EjecutorCuerpo(new ContextoEjecucionAccion(medios, motor.LugaresOfrecidos, ModoReloj.Pelican));
        motor.ConectarEjecutorCuerpo(ejecutor, () => 20_000);

        string? acercamientoCompletadoPara = null;
        motor.AcercamientoAlJugadorCompletado += obj => acercamientoCompletadoPara = obj;

        // Paso 1 sin jugador, paso 2 entra a 8 casillas -> pulso con acercarse_a(farmer:7) e impulso_social.
        var obsVacia = new Observacion(10, 1200, "Forest", 33, 25, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obsVacia, horario: null, ahoraMs: 1_000, minutosJuego: 0);

        var obsJugador = obsVacia with
        {
            Entidades = new[]
            {
                new EntidadObservada("farmer:7", CategoriaEntidad.Granjero, "Álex", "Forest", 33, 33, Array.Empty<string>(), Corazones: 6),
            },
        };
        var paso2 = motor.ProcesarPaso(obsJugador, horario: null, ahoraMs: 2_000, minutosJuego: 0);
        var pulso = Assert.Single(paso2.Pulsos);
        Assert.Contains("acercarse_a(farmer:7)", pulso.AccionesDisponibles);
        Assert.True(pulso.Hechos.ContainsKey("impulso_social"));

        // Mente decide acercarse + adjunta diálogo precalculado.
        var res = motor.AplicarRespuesta(pulso, new Respuesta
        {
            Pensamiento = "Llevo días sin ver a Álex y hoy estoy animada para enseñarle esto.",
            Epoca = "ep-613",
            BasadaEn = pulso.Id,
            Dialogo = new DialogoPreparado
            {
                Para = "farmer:7",
                Tipo = "afirmacion",
                Texto = "Qué alegría verte por aquí, justo pensaba en ti.",
            },
            Intencion = new IntencionPropuesta
            {
                Objetivo = "saludar a Álex",
                Porque = "me apetece acercarme",
                Plan = new List<PasoPlan> { new() { Accion = "acercarse_a", Objetivo = "farmer:7" } },
            },
        });
        Assert.Equal(Resultado.Aceptada, res.Estado);
        Assert.Equal(1, medios.VecesIniciarAcercamiento);

        // En rango -> dispara evento de llegada para entregar el diálogo.
        medios.SiguienteEstado = EstadoAcercamientoJugador.EnRango;
        ejecutor.AvanzarTick(ahoraMs: 3_000);
        Assert.Equal("farmer:7", acercamientoCompletadoPara);

        // Si en cambio el jugador sale del alcance antes de llegar -> cancela y vuelve a rutina sin atascarse.
        var mediosLejos = new MediosAcercamientoFake { SiguienteEstado = EstadoAcercamientoJugador.CanceladoFueraDeAlcance };
        var ejecutorLejos = new EjecutorCuerpo(new ContextoEjecucionAccion(mediosLejos, motor.LugaresOfrecidos, ModoReloj.Pelican));
        motor.ConectarEjecutorCuerpo(ejecutorLejos, () => 20_000);
        ejecutorLejos.EjecutarPlan(new[] { new PasoPlan { Accion = "acercarse_a", Objetivo = "farmer:7" } }, ahoraMs: 4_000, idPlan: "test-lejos");
        Assert.False(mediosLejos.EstaReclamado);
        Assert.Equal(1, mediosLejos.VecesVolverARutina);
    }
}
