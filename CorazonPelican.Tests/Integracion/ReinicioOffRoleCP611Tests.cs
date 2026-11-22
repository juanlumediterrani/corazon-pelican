using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Integracion;

/// <summary>
/// Pruebas del reinicio técnico off-role tras saltos de reloj administrativos de Pelican 4Ever (CP-611).
/// Verifica corte de intención/plan, liberación de control sin iniciar ruta previa, recarga de horario
/// canónico, reseteo de presupuesto/disparadores y rotación de época en <see cref="ApiMotor"/>.
/// </summary>
public class ReinicioOffRoleCP611Tests
{
    private sealed class MediosOffRoleFake : IMediosCuerpo
    {
        public string UbicacionActual => "LeahHouse";
        public int CasillaX => 3;
        public int CasillaY => 7;
        public bool EstaReclamado { get; private set; }
        public bool EstaEnAnimacionActividad { get; private set; }
        public EstadoRutaCuerpo EstadoRutaActual { get; private set; } = EstadoRutaCuerpo.Inactiva;
        public string? MotivoBloqueoRuta => null;
        public long DuracionAsentamientoSentarseMs => 0;

        public int VecesLiberarParaReubicacion { get; private set; }
        public int VecesVolverARutina { get; private set; }

        public void ReclamarControl(string motivo) => this.EstaReclamado = true;

        public bool IniciarRutaHacia(Lugar destino, int orientacionFinal)
        {
            this.EstadoRutaActual = EstadoRutaCuerpo.EnCurso;
            return true;
        }

        public void DetenerMovimiento() => this.EstadoRutaActual = EstadoRutaCuerpo.Inactiva;

        public bool AdoptarPosturaSentada(Lugar destino, int orientacion) => false;

        public bool IntentarMirarObjetivo(string objetivoRef) => false;

        public bool PuedeDibujar(out string? motivoBloqueo)
        {
            motivoBloqueo = null;
            return true;
        }

        public bool IniciarDibujo(string? obraId)
        {
            this.EstaEnAnimacionActividad = true;
            return true;
        }

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

        public void RestaurarHorarioCanonico()
        {
        }

        public void LiberarControlParaReubicacion()
        {
            this.VecesLiberarParaReubicacion++;
            this.DetenerAnimacionActividad();
            this.DetenerMovimiento();
            this.EstaReclamado = false;
        }
    }

    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    [Fact]
    public void RestablecerOffRole_CortaPlanLiberaControlReiniciaPresupuestoYYaNoEmiteHitosPasados_CP611()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = PlantillasProsa.DesdeJson(File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));
        var estado = new EstadoPersonaje();
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Pelican,
            publicarPulso: p =>
            {
                p.Id = 1;
                return p;
            })
        { Epoca = "ep-vieja" };
        var medios = new MediosOffRoleFake();
        var ejecutor = new EjecutorCuerpo(new ContextoEjecucionAccion(medios, motor.LugaresOfrecidos, ModoReloj.Pelican));
        motor.ConectarEjecutorCuerpo(ejecutor, () => 20_000);

        var horario = new HorarioDia(
            "Mon",
            new[]
            {
                new EntradaHorario(1100, "SeedShop", 11, 23),
                new EntradaHorario(1700, "LeahHouse", 8, 5),
                new EntradaHorario(2400, "LeahHouse", 2, 4),
            });

        var obs2200 = new Observacion(
            Dia: 1,
            Hora: 2200,
            Ubicacion: "LeahHouse",
            X: 3,
            Y: 7,
            Luz: 0.2,
            Clima: "soleado",
            Entidades: new[]
            {
                new EntidadObservada(
                    Ref: "granjero:1",
                    Categoria: CategoriaEntidad.Granjero,
                    Nombre: "Alex",
                    Ubicacion: "LeahHouse",
                    X: 3,
                    Y: 8,
                    Etiquetas: Array.Empty<string>()),
            });

        motor.ProcesarPaso(obs2200, horario, ahoraMs: 1_000, minutosJuego: 1.0);
        var forzado = motor.ForzarPulso(obs2200, ahoraMs: 1_100, horario: horario);
        Assert.NotNull(forzado.Pulso);

        var respuesta = new Respuesta
        {
            Pensamiento = "La tarde avanza con buena luz para bocetar.",
            Epoca = "ep-vieja",
            BasadaEn = forzado.Pulso!.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "bocetar en el caballete",
                Porque = "luz tranquila",
                PlazoJuego = new TiempoJuego { Dia = 1, Hora = 2330 },
                Plan = new List<PasoPlan> { new() { Accion = "dibujar", Obra = "nueva" } },
            },
        };

        var resAplicar = motor.AplicarRespuesta(forzado.Pulso, respuesta);
        Assert.True(
            resAplicar.Estado == Resultado.Aceptada,
            $"Rechazada: {string.Join("; ", resAplicar.Motivos?.Select(m => $"{m.Codigo}:{m.Campo}:{m.Prosa}") ?? Array.Empty<string>())}");
        motor.ProcesarPaso(obs2200, horario, ahoraMs: 1_500, minutosJuego: 0.5);

        Assert.True(medios.EstaReclamado);
        Assert.NotNull(motor.IntencionActivaVigente);

        // Abrir turno de diálogo activo para verificar limpieza total.
        var turno = motor.IniciarTurnoDialogo(obs2200, "granjero:1", "Alex", 1_900, 5.0);
        Assert.True(motor.Dialogo.HayTurnoActivo);
        Assert.NotNull(turno);

        // Salto off-role hacia las 14:00 (después de 11:00, antes de 17:00).
        var teniaEstado = motor.RestablecerOffRole(dia: 1, nuevaHora: 1400, ahoraMs: 10_000, horario: horario, restaurarRutinaLocal: false);

        Assert.True(teniaEstado);
        Assert.False(medios.EstaReclamado);
        Assert.False(medios.EstaEnAnimacionActividad);
        Assert.Equal(1, medios.VecesLiberarParaReubicacion);
        Assert.Equal(0, medios.VecesVolverARutina);
        Assert.Null(motor.IntencionActivaVigente);
        Assert.Null(motor.Estado.IntencionActiva);
        Assert.False(motor.Dialogo.HayTurnoActivo);
        Assert.Equal(0, motor.Disparadores.Contadores!.OrdinariosLibresGastados);
        Assert.True(motor.Disparadores.TieneCupoOrdinarioDisponible);
        Assert.Empty(motor.Bufer.Entradas);

        // Primer paso tras el salto a las 14:00 (ya recolocada en SeedShop): no dispara el hito pasado de las 11:00.
        var obs1400 = obs2200 with { Hora = 1400, Ubicacion = "SeedShop", X = 11, Y = 23, Entidades = Array.Empty<EntidadObservada>() };
        var pasoPostSalto = motor.ProcesarPaso(obs1400, horario, ahoraMs: 10_500, minutosJuego: 0.5);
        Assert.DoesNotContain(pasoPostSalto.Disparos, d => d.Hito?.Id.Contains("1100") == true);
    }

    [Fact]
    public async Task RotarEpocaEnSaltoOffRole_DescartaRespuestasDeEpocaAnteriorYDespiertaLongPoll_CP611()
    {
        var api = new ApiMotor();
        api.IniciarEpoca("ep-antes", new[] { "leah" });
        var pulso = api.EmitirPulso(new Pulso
        {
            Epoca = "ep-antes",
            Personaje = "leah",
            Motivo = Motivos.Pico,
            TiempoJuego = new TiempoJuego { Dia = 1, Hora = 2200 },
        });

        var espera = api.EsperarPulsosAsync("ep-antes", since: pulso.Id, esperaMs: 5_000);
        api.IniciarEpoca("ep-despues", new[] { "leah" });

        var resultadoPoll = await espera;
        Assert.True(resultadoPoll.ConflictoEpoca);
        Assert.Equal("ep-despues", resultadoPoll.EpocaActual);

        var resRespuestaVieja = api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Epoca = "ep-antes",
            BasadaEn = pulso.Id,
        });
        Assert.Equal(Resultado.Rechazada, resRespuestaVieja.Estado);
        Assert.Contains(resRespuestaVieja.Motivos ?? new List<MotivoRechazo>(), m => m.Codigo == "epoca_obsoleta");
    }
}
