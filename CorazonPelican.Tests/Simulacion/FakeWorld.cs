using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Tests.Simulacion;

/// <summary>Un lugar del escenario, con la casilla a la que se va al pedir <c>ir_a</c>.</summary>
public sealed record LugarSimulado(string Id, string Ubicacion, int X, int Y, string? Prosa = null)
{
    public Lugar AlLexico()
        => new() { Id = this.Id, Ubicacion = this.Ubicacion, Area = new() { this.X, this.Y, 1, 1 }, Prosa = this.Prosa ?? this.Id };
}

/// <summary>Posición del personaje gobernado en el mundo simulado.</summary>
public sealed class PosicionPersonaje
{
    public string Ubicacion { get; set; } = "";

    public int X { get; set; }

    public int Y { get; set; }
}

/// <summary>Una entidad del escenario; las que deambulan se mueven una casilla por segundo con el azar sembrado.</summary>
internal sealed class EntidadSimulada
{
    public required string Ref { get; init; }

    public required CategoriaEntidad Categoria { get; init; }

    public required string Nombre { get; init; }

    public required string Ubicacion { get; set; }

    public int X { get; set; }

    public int Y { get; set; }

    public IReadOnlyList<string> Etiquetas { get; init; } = Array.Empty<string>();

    public int? Corazones { get; init; }

    public bool Deambula { get; init; }

    public EntidadSimulada Clonar() => (EntidadSimulada)this.MemberwiseClone();

    public EntidadObservada Observar() => new(this.Ref, this.Categoria, this.Nombre, this.Ubicacion, this.X, this.Y, this.Etiquetas, this.Corazones);
}

/// <summary>
/// Mundo simulado para probar el motor sin juego (ADS §13): reloj virtual, entidades con identidad
/// estable, un personaje con cuerpo falso y una mente falsa. Todo es determinista para una semilla.
/// Se construye con <see cref="Escenario"/>.
/// </summary>
public sealed class FakeWorld
{
    /// <summary>Cadencia del sensorio: 15 ticks ≈ 250 ms (ADS §4).</summary>
    public const long PasoMs = 250;

    private readonly List<EntidadSimulada> entidades;
    private readonly Random azar;
    private readonly Func<int, string, HorarioDia?> horarios;
    private long ultimoDeambularMs;

    internal FakeWorld(RelojVirtual reloj, PosicionPersonaje personaje, Lexico lexico,
        List<EntidadSimulada> entidades, string clima, int semilla, FakeMind mente, Func<int, string, HorarioDia?>? horarios = null)
    {
        this.horarios = horarios ?? ((_, _) => null);
        this.Reloj = reloj;
        this.Personaje = personaje;
        this.LugaresOfrecidos = lexico.OfrecerLugares(_ => true);
        this.entidades = entidades;
        this.Clima = clima;
        this.azar = new Random(semilla);
        this.Mente = mente;
        this.Cuerpo = new FakeBody(this);
    }

    public RelojVirtual Reloj { get; }

    public PosicionPersonaje Personaje { get; }

    public LugaresOfrecidos LugaresOfrecidos { get; }

    public string Clima { get; set; }

    public FakeBody Cuerpo { get; }

    /// <summary>El horario que «el juego» elige hoy para el personaje, según el día y el clima (CP-011).</summary>
    public HorarioDia? HorarioHoy => this.horarios(this.Reloj.Juego.Dia, this.Clima);

    public FakeMind Mente { get; }

    /// <summary>Respuestas de la mente entregadas al motor, en orden.</summary>
    public List<Respuesta> RespuestasEntregadas { get; } = new();

    /// <summary>Entrega un pulso a la mente en el instante real actual.</summary>
    public void EmitirPulso(Pulso pulso) => this.Mente.Recibir(pulso, this.Reloj.MilisegundosReales);

    /// <summary>Avanza el mundo en pasos de <see cref="PasoMs"/>.</summary>
    public void Avanzar(TimeSpan duracion) => this.AvanzarObservando(duracion, observar: false);

    /// <summary>Avanza el mundo y devuelve la observación de cada paso del sensorio.</summary>
    public List<Observacion> AvanzarObservando(TimeSpan duracion) => this.AvanzarObservando(duracion, observar: true);

    public Observacion Observar()
    {
        var visibles = this.entidades
            .Where(e => e.Ubicacion == this.Personaje.Ubicacion)
            .Select(e => e.Observar())
            .ToList();
        return new Observacion(this.Reloj.Juego.Dia, this.Reloj.Juego.Hora, this.Personaje.Ubicacion,
            this.Personaje.X, this.Personaje.Y, Luz(this.Reloj.Juego.Hora), this.Clima, visibles);
    }

    /// <summary>Luz ambiental simple: pleno día hasta las 18:00, se apaga hasta las 20:00, noche 0,2.</summary>
    public static double Luz(int hora)
    {
        var minutos = HoraJuego.MinutosDesdeInicio(hora);
        const int Ocaso = 12 * 60;
        const int Noche = 14 * 60;
        if (minutos <= Ocaso)
        {
            return 1.0;
        }

        return minutos >= Noche ? 0.2 : 1.0 - (0.8 * (minutos - Ocaso) / (Noche - Ocaso));
    }

    internal void MoverPersonaje(Lugar destino)
    {
        this.Personaje.Ubicacion = destino.Ubicacion;
        this.Personaje.X = destino.Area[0];
        this.Personaje.Y = destino.Area[1];
    }

    private List<Observacion> AvanzarObservando(TimeSpan duracion, bool observar)
    {
        var observaciones = new List<Observacion>();
        var restante = (long)duracion.TotalMilliseconds;
        while (restante > 0)
        {
            var paso = Math.Min(PasoMs, restante);
            restante -= paso;
            this.Reloj.Avanzar(paso);
            var ahora = this.Reloj.MilisegundosReales;
            this.Deambular(ahora);
            this.Cuerpo.Avanzar(ahora);
            this.RespuestasEntregadas.AddRange(this.Mente.Recoger(ahora));
            if (observar)
            {
                observaciones.Add(this.Observar());
            }
        }

        return observaciones;
    }

    private void Deambular(long ahoraMs)
    {
        while (ahoraMs - this.ultimoDeambularMs >= 1_000)
        {
            this.ultimoDeambularMs += 1_000;
            foreach (var entidad in this.entidades.Where(e => e.Deambula))
            {
                switch (this.azar.Next(5))
                {
                    case 0: entidad.X++; break;
                    case 1: entidad.X = Math.Max(0, entidad.X - 1); break;
                    case 2: entidad.Y++; break;
                    case 3: entidad.Y = Math.Max(0, entidad.Y - 1); break;
                }
            }
        }
    }
}
