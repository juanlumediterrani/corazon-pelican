using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Tests.Simulacion;

/// <summary>
/// Constructor legible de mundos simulados:
/// <c>Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9).Construir()</c>.
/// Las casillas son inventadas: el FakeWorld no pretende reproducir los mapas del juego.
/// </summary>
public sealed class Escenario
{
    private readonly List<EntidadSimulada> entidades = new();
    private readonly Dictionary<string, LugarSimulado> lugares = new();
    private readonly PosicionPersonaje personaje;
    private ModoReloj modo = ModoReloj.Vanilla;
    private int dia = 68;
    private int hora = 1700;
    private string clima = "soleado";
    private int semilla = 1;
    private FakeMind mente = FakeMind.Rapida();
    private int jugadores;
    private Func<int, string, HorarioDia?>? horarios;

    private Escenario(LugarSimulado inicio)
    {
        this.ConLugar(inicio);
        this.personaje = new PosicionPersonaje { Ubicacion = inicio.Ubicacion, X = inicio.X, Y = inicio.Y };
    }

    /// <summary>La escena fundacional (GSS §1): el personaje en la pasarela del lago, otoño, última hora de la tarde.</summary>
    public static Escenario Pasarela()
    {
        var escenario = new Escenario(new LugarSimulado("pasarela_lago", "Mountain", 40, 30, "la pasarela de madera del lago"))
            .ConLugar(new LugarSimulado("orilla_norte", "Mountain", 40, 18, "la orilla norte"))
            .ConLugar(new LugarSimulado("tu_casa", "LeahHouse", 8, 5, "tu casa"));
        escenario.entidades.Add(new EntidadSimulada
        {
            Ref = "agua:Mountain:lago", Categoria = CategoriaEntidad.Terreno, Nombre = "el lago", Ubicacion = "Mountain",
            X = 40, Y = 34, Etiquetas = new[] { "agua" },
        });
        escenario.entidades.Add(new EntidadSimulada
        {
            Ref = "obj:Mountain:40,30:pasarela", Categoria = CategoriaEntidad.Objeto, Nombre = "la pasarela de madera",
            Ubicacion = "Mountain", X = 40, Y = 30, Etiquetas = new[] { "madera" },
        });
        return escenario;
    }

    public Escenario ConLugar(LugarSimulado lugar)
    {
        this.lugares[lugar.Id] = lugar;
        return this;
    }

    /// <summary>Un granjero a <paramref name="distancia"/> casillas al norte del personaje.</summary>
    public Escenario ConJugador(string nombre, int corazones, int distancia, bool deambula = false)
    {
        this.jugadores++;
        this.entidades.Add(new EntidadSimulada
        {
            Ref = $"farmer:{this.jugadores}", Categoria = CategoriaEntidad.Granjero, Nombre = nombre,
            Ubicacion = this.personaje.Ubicacion, X = this.personaje.X, Y = this.personaje.Y - distancia,
            Etiquetas = new[] { "persona" }, Corazones = corazones, Deambula = deambula,
        });
        return this;
    }

    public Escenario ConNpc(string nombre, string ubicacion, int x, int y, bool deambula = true)
    {
        this.entidades.Add(new EntidadSimulada
        {
            Ref = $"npc:{nombre}", Categoria = CategoriaEntidad.Npc, Nombre = nombre, Ubicacion = ubicacion,
            X = x, Y = y, Etiquetas = new[] { "persona" }, Deambula = deambula,
        });
        return this;
    }

    /// <summary><paramref name="cantidad"/> personas sin relación repartidas alrededor, que deambulan (un festival).</summary>
    public Escenario ConMultitud(int cantidad, int radio = 15)
    {
        for (var i = 0; i < cantidad; i++)
        {
            this.entidades.Add(new EntidadSimulada
            {
                Ref = $"npc:multitud_{i}", Categoria = CategoriaEntidad.Npc, Nombre = "alguien", Ubicacion = this.personaje.Ubicacion,
                X = this.personaje.X - radio + ((i * 7) % (2 * radio)), Y = this.personaje.Y - radio + ((i * 11) % (2 * radio)),
                Etiquetas = new[] { "persona" }, Deambula = true,
            });
        }

        return this;
    }

    /// <summary>Cómo elige «el juego» el horario de cada día según el día y el clima.</summary>
    public Escenario ConHorarios(Func<int, string, HorarioDia?> horarios)
    {
        this.horarios = horarios;
        return this;
    }

    public Escenario ConClima(string clima)
    {
        this.clima = clima;
        return this;
    }

    public Escenario ALas(int dia, int hora)
    {
        this.dia = dia;
        this.hora = hora;
        return this;
    }

    public Escenario EnModo(ModoReloj modo)
    {
        this.modo = modo;
        return this;
    }

    public Escenario ConSemilla(int semilla)
    {
        this.semilla = semilla;
        return this;
    }

    public Escenario ConMente(FakeMind mente)
    {
        this.mente = mente;
        return this;
    }

    /// <summary>Cada llamada da un mundo independiente (copia entidades y posición).</summary>
    public FakeWorld Construir(Lexico? lexico = null)
        => new(
            new RelojVirtual(this.modo, this.dia, this.hora),
            new PosicionPersonaje { Ubicacion = this.personaje.Ubicacion, X = this.personaje.X, Y = this.personaje.Y },
            lexico ?? new Lexico { Lugares = this.lugares.Values.Select(lugar => lugar.AlLexico()).ToList() },
            this.entidades.Select(e => e.Clonar()).ToList(),
            this.clima,
            this.semilla,
            this.mente,
            this.horarios);
}
