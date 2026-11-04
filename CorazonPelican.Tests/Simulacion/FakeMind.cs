using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Tests.Simulacion;

/// <summary>
/// Mente falsa sin hilos: recibe pulsos y deja cada respuesta disponible cuando el reloj virtual
/// alcanza su latencia. Rápida, lenta, caída o guionizada (ADS §13).
/// </summary>
public sealed class FakeMind
{
    private readonly long latenciaMs;
    private readonly Func<Pulso, Respuesta?> guion;
    private readonly bool caida;
    private readonly List<(long Listo, Respuesta Respuesta)> enCamino = new();

    private FakeMind(long latenciaMs, Func<Pulso, Respuesta?>? guion, bool caida)
    {
        this.latenciaMs = latenciaMs;
        this.guion = guion ?? PensarAlgo;
        this.caida = caida;
    }

    public List<Pulso> Recibidos { get; } = new();

    /// <summary>Responde en el siguiente paso del mundo.</summary>
    public static FakeMind Rapida(Func<Pulso, Respuesta?>? guion = null) => new(0, guion, caida: false);

    public static FakeMind Lenta(TimeSpan latencia, Func<Pulso, Respuesta?>? guion = null) => new((long)latencia.TotalMilliseconds, guion, caida: false);

    /// <summary>Recibe pulsos y no responde nunca (proceso caído o sin red).</summary>
    public static FakeMind Caida() => new(0, null, caida: true);

    /// <summary>Respuesta mínima válida: solo piensa.</summary>
    public static Respuesta PensarAlgo(Pulso pulso)
        => new() { Pensamiento = "…", Epoca = pulso.Epoca, BasadaEn = pulso.Id };

    public void Recibir(Pulso pulso, long ahoraMs)
    {
        this.Recibidos.Add(pulso);
        if (this.caida)
        {
            return;
        }

        var respuesta = this.guion(pulso);
        if (respuesta is not null)
        {
            this.enCamino.Add((ahoraMs + this.latenciaMs, respuesta));
        }
    }

    /// <summary>Respuestas cuya latencia ya se ha cumplido, en orden de llegada.</summary>
    public List<Respuesta> Recoger(long ahoraMs)
    {
        var listas = this.enCamino.Where(e => e.Listo <= ahoraMs).OrderBy(e => e.Listo).ToList();
        this.enCamino.RemoveAll(e => e.Listo <= ahoraMs);
        return listas.Select(e => e.Respuesta).ToList();
    }
}
