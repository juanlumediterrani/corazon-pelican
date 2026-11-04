using CorazonPelican.Core.Contrato;

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
/// Cuerpo falso: ejecuta un plan del vocabulario v1 con duraciones simuladas y mueve al personaje
/// por los lugares del escenario. Tres clases de acción:
/// instantáneas (<c>mirar</c>, <c>gesto</c>…), con duración (<c>ir_a</c>, <c>sentarse</c>, <c>esperar</c>)
/// y continuas (<c>dibujar</c>, sigue hasta que otra acción la sustituye). El plan termina cuando
/// acaba su última acción con duración o empieza una continua.
/// </summary>
public sealed class FakeBody
{
    /// <summary>Un NPC recorre ~1,9 casillas por segundo real; cambiar de ubicación cuesta como 20 casillas.</summary>
    public const double MsPorCasilla = 533;

    public const int CasillasPorCambioDeUbicacion = 20;

    public const long MsSentarse = 1_000;

    private static readonly HashSet<string> Instantaneas = new() { "mirar", "levantar_la_vista", "gesto", "murmurar", "guardar_cuaderno", "volver_a_rutina" };

    private readonly FakeWorld mundo;
    private readonly Queue<PasoPlan> pendientes = new();
    private PasoPlan? actual;
    private bool planEnCurso;
    private long terminaEn;

    internal FakeBody(FakeWorld mundo)
    {
        this.mundo = mundo;
    }

    public List<SucesoCuerpo> Sucesos { get; } = new();

    /// <summary>Acción continua en curso (p. ej. <c>dibujar</c>), si la hay.</summary>
    public string? Continua { get; private set; }

    public bool Sentada { get; private set; }

    public bool Ocupado => this.actual is not null || this.pendientes.Count > 0;

    /// <summary>Sustituye lo que estuviera haciendo por un plan nuevo.</summary>
    public void Ejecutar(IEnumerable<PasoPlan> plan)
    {
        this.pendientes.Clear();
        this.actual = null;
        this.planEnCurso = true;
        foreach (var paso in plan)
        {
            this.pendientes.Enqueue(paso);
        }

        this.Siguiente(this.mundo.Reloj.MilisegundosReales);
    }

    internal void Avanzar(long ahoraMs)
    {
        if (this.actual is not null && ahoraMs >= this.terminaEn)
        {
            this.Terminar(this.actual, ahoraMs);
            this.actual = null;
            this.Siguiente(ahoraMs);
        }
    }

    private void Siguiente(long ahoraMs)
    {
        while (this.actual is null && this.pendientes.Count > 0)
        {
            var paso = this.pendientes.Dequeue();
            var duracion = this.Iniciar(paso, ahoraMs);
            if (duracion is null)
            {
                continue;
            }

            if (duracion == 0)
            {
                this.Terminar(paso, ahoraMs);
                continue;
            }

            this.actual = paso;
            this.terminaEn = ahoraMs + duracion.Value;
        }

        if (this.planEnCurso && this.actual is null && this.pendientes.Count == 0)
        {
            this.planEnCurso = false;
            this.Sucesos.Add(new(ahoraMs, TipoSuceso.PlanTerminado, "plan"));
        }
    }

    /// <summary>Devuelve la duración en ms, 0 si es instantánea, o <c>null</c> si se bloquea o es continua.</summary>
    private long? Iniciar(PasoPlan paso, long ahoraMs)
    {
        this.Sucesos.Add(new(ahoraMs, TipoSuceso.AccionIniciada, paso.Accion, paso.Lugar ?? paso.Objetivo ?? paso.Obra));
        switch (paso.Accion)
        {
            case "ir_a":
                if (paso.Lugar is null || !this.mundo.Lugares.TryGetValue(paso.Lugar, out var destino))
                {
                    this.Sucesos.Add(new(ahoraMs, TipoSuceso.AccionBloqueada, paso.Accion, $"lugar desconocido: {paso.Lugar}"));
                    this.pendientes.Clear();
                    this.planEnCurso = false;
                    return null;
                }

                this.Continua = null;
                this.Sentada = false;
                var casillas = Math.Abs(destino.X - this.mundo.Personaje.X) + Math.Abs(destino.Y - this.mundo.Personaje.Y)
                    + (destino.Ubicacion == this.mundo.Personaje.Ubicacion ? 0 : CasillasPorCambioDeUbicacion);
                this.mundo.MoverPersonaje(destino);
                return (long)(casillas * MsPorCasilla);

            case "sentarse":
                this.Sentada = true;
                return MsSentarse;

            case "esperar":
                return (paso.DuracionMin ?? 10) / 10 * Core.Tiempo.HoraJuego.MilisegundosPorDiezMinutos(this.mundo.Reloj.Modo);

            case "dibujar":
                this.Continua = "dibujar";
                this.Sucesos.Add(new(ahoraMs, TipoSuceso.AccionTerminada, paso.Accion, "continua"));
                return null;

            default:
                if (paso.Accion == "guardar_cuaderno")
                {
                    this.Continua = null;
                }

                return Instantaneas.Contains(paso.Accion) ? 0 : null;
        }
    }

    private void Terminar(PasoPlan paso, long ahoraMs)
        => this.Sucesos.Add(new(ahoraMs, TipoSuceso.AccionTerminada, paso.Accion));
}
