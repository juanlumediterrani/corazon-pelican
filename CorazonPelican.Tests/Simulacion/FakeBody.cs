using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Tests.Simulacion;

/// <summary>Lo que el cuerpo falso cuenta que ha pasado (alimenta los hitos de intención, CP-201).</summary>
public enum TipoSuceso
{
    AccionIniciada,
    AccionTerminada,
    AccionBloqueada,
    PlanTerminado,
}

public sealed record SucesoCuerpo(long MilisegundosReales, TipoSuceso Tipo, string Accion, string? Detalle = null);

/// <summary>
/// Cuerpo falso sobre <see cref="FakeWorld"/> que delega la máquina de estados en <see cref="EjecutorCuerpo"/>
/// y las clases por acción de <c>CorazonPelican.Core.Intencion</c> (CP-302), simulando los tiempos de
/// desplazamiento, asentamiento y animaciones sin depender de Stardew Valley.
/// </summary>
public sealed class FakeBody : IMediosCuerpo
{
    /// <summary>Un NPC recorre ~1,9 casillas por segundo real; cambiar de ubicación cuesta como 20 casillas.</summary>
    public const double MsPorCasilla = 533;

    public const int CasillasPorCambioDeUbicacion = 20;

    public const long MsSentarse = 1_000;

    public const long MsSalidaAnimacion = 100;

    private readonly FakeWorld mundo;
    private Lugar? destinoRutaPendiente;
    private long finRutaMs;
    private bool enAnimacionActividad;
    private int sucesosSincronizados;

    internal FakeBody(FakeWorld mundo)
    {
        this.mundo = mundo;
        var contexto = new ContextoEjecucionAccion(this, mundo.LugaresOfrecidos, mundo.Reloj.Modo);
        this.Ejecutor = new EjecutorCuerpo(contexto);
    }

    public EjecutorCuerpo Ejecutor { get; }

    public List<SucesoCuerpo> Sucesos { get; } = new();

    /// <summary>Acción continua en curso (p. ej. <c>dibujar</c>), si la hay.</summary>
    public string? Continua => this.Ejecutor.Continua;

    public bool Sentada => this.Ejecutor.Sentada;

    public bool Ocupado => this.Ejecutor.Ocupado;

    public EstadoAtencion EstadoAtencion => this.Ejecutor.Contexto.EstadoAtencion;

    public string? FocoObjetivoRef => this.Ejecutor.Contexto.FocoObjetivoRef;

    public string? UltimoGesto { get; private set; }

    public string? UltimoMurmullo { get; private set; }

    public bool ControlReclamado { get; private set; }

    /// <summary>Si es <c>true</c>, simula un camino obstruido que nunca llega para verificar el tope de duración máxima.</summary>
    public bool SimularRutaColgada { get; set; }

    /// <summary>Si es <c>true</c>, simula que el personaje no puede dibujar (p. ej. atuendo incompatible).</summary>
    public bool SimularDibujoNoDisponible { get; set; }

    public string UbicacionActual => this.mundo.Personaje.Ubicacion;

    public int CasillaX => this.mundo.Personaje.X;

    public int CasillaY => this.mundo.Personaje.Y;

    public bool EstaEnAnimacionActividad => this.enAnimacionActividad;

    public EstadoRutaCuerpo EstadoRutaActual { get; private set; } = EstadoRutaCuerpo.Inactiva;

    public string? MotivoBloqueoRuta { get; private set; }

    public long DuracionAsentamientoSentarseMs => MsSentarse;

    /// <summary>Sustituye lo que estuviera haciendo por un plan nuevo.</summary>
    public void Ejecutar(IEnumerable<PasoPlan> plan, string? idPlan = null)
    {
        this.SincronizarMedios(this.mundo.Reloj.MilisegundosReales);
        this.Ejecutor.EjecutarPlan(plan, this.mundo.Reloj.MilisegundosReales, idPlan);
        this.SincronizarSucesos();
    }

    internal void Avanzar(long ahoraMs)
    {
        this.SincronizarMedios(ahoraMs);
        this.Ejecutor.AvanzarTick(ahoraMs);
        this.SincronizarSucesos();
    }

    private void SincronizarSucesos()
    {
        var origen = this.Ejecutor.Sucesos;
        while (this.sucesosSincronizados < origen.Count)
        {
            var s = origen[this.sucesosSincronizados++];
            var tipo = s.Tipo switch
            {
                TipoSucesoCuerpo.AccionIniciada => TipoSuceso.AccionIniciada,
                TipoSucesoCuerpo.AccionTerminada => TipoSuceso.AccionTerminada,
                TipoSucesoCuerpo.AccionBloqueada => TipoSuceso.AccionBloqueada,
                TipoSucesoCuerpo.PlanTerminado => TipoSuceso.PlanTerminado,
                _ => TipoSuceso.AccionTerminada,
            };
            this.Sucesos.Add(new SucesoCuerpo(s.MilisegundosReales, tipo, s.Accion, s.Detalle));
        }
    }

    public void ReclamarControl(string motivo)
        => this.ControlReclamado = true;

    public bool IniciarRutaHacia(Lugar destino, int orientacionFinal)
    {
        this.MotivoBloqueoRuta = null;
        var casillas = Math.Abs(destino.Area[0] - this.mundo.Personaje.X)
            + Math.Abs(destino.Area[1] - this.mundo.Personaje.Y)
            + (destino.Ubicacion == this.mundo.Personaje.Ubicacion ? 0 : CasillasPorCambioDeUbicacion);

        if (this.SimularRutaColgada)
        {
            this.destinoRutaPendiente = destino;
            this.finRutaMs = long.MaxValue;
            this.EstadoRutaActual = EstadoRutaCuerpo.EnCurso;
            return true;
        }

        var duracionMs = (long)(casillas * MsPorCasilla);
        this.mundo.MoverPersonaje(destino);
        if (duracionMs <= 0)
        {
            this.destinoRutaPendiente = null;
            this.EstadoRutaActual = EstadoRutaCuerpo.Completada;
            return true;
        }

        this.destinoRutaPendiente = destino;
        this.finRutaMs = this.mundo.Reloj.MilisegundosReales + duracionMs;
        this.EstadoRutaActual = EstadoRutaCuerpo.EnCurso;
        return true;
    }

    public void DetenerMovimiento()
    {
        this.destinoRutaPendiente = null;
        this.EstadoRutaActual = EstadoRutaCuerpo.Inactiva;
    }

    public bool AdoptarPosturaSentada(Lugar destino, int orientacion)
    {
        this.mundo.MoverPersonaje(destino);
        return true;
    }

    public bool IntentarMirarObjetivo(string objetivoRef)
        => this.mundo.ExisteEntidadVisible(objetivoRef);

    public bool PuedeDibujar(out string? motivoBloqueo)
    {
        if (this.SimularDibujoNoDisponible)
        {
            motivoBloqueo = "animación de dibujo no disponible con el atuendo actual";
            return false;
        }

        motivoBloqueo = null;
        return true;
    }

    public bool IniciarDibujo(string? obraId)
    {
        this.enAnimacionActividad = true;
        return true;
    }

    public void DetenerAnimacionActividad()
    {
        this.enAnimacionActividad = false;
    }

    public bool HacerGesto(string tipo)
    {
        this.UltimoGesto = tipo;
        return true;
    }

    public bool Murmurar(string texto)
    {
        this.UltimoMurmullo = texto;
        return true;
    }

    public bool VolverARutina()
    {
        this.ControlReclamado = false;
        var horario = this.mundo.HorarioHoy;
        if (horario is not null && horario.Entradas.Count > 0)
        {
            var hora = this.mundo.Reloj.Juego.Hora;
            var vigente = horario.Entradas.Where(e => e.Hora <= hora).OrderBy(e => e.Hora).LastOrDefault()
                ?? horario.Entradas.OrderBy(e => e.Hora).First();
            this.mundo.Personaje.Ubicacion = vigente.Ubicacion;
            this.mundo.Personaje.X = vigente.X;
            this.mundo.Personaje.Y = vigente.Y;
        }

        return true;
    }

    private void SincronizarMedios(long ahoraMs)
    {
        if (this.EstadoRutaActual == EstadoRutaCuerpo.EnCurso && !this.SimularRutaColgada && ahoraMs >= this.finRutaMs)
        {
            if (this.destinoRutaPendiente is not null)
            {
                this.mundo.MoverPersonaje(this.destinoRutaPendiente);
                this.destinoRutaPendiente = null;
            }

            this.EstadoRutaActual = EstadoRutaCuerpo.Completada;
        }
    }
}
