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

public class OpcionesJugadorTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Nunca_ofrece_regalar_algo_que_no_esta_en_el_inventario()
    {
        var perfil = CargarPerfil();

        var sinInventario = GeneradorOpcionesJugador.Generar(new ContextoOpcionesJugador(
            perfil,
            Corazones: 2,
            Inventario: new[]
            {
                new ObjetoInventarioJugador("280", "Ñame", EsRegalable: true, Cantidad: 0),
                new ObjetoInventarioJugador("72", "Herramienta", EsRegalable: false, Cantidad: 1),
            },
            EncargosDisponibles: Array.Empty<PropuestaEncargo>(),
            MaxOpciones: 4));

        Assert.DoesNotContain(sinInventario, o => o.Categoria == "regalar");
        Assert.Contains(sinInventario, o => o.Id == GeneradorOpcionesJugador.IdDecirOtraCosa);

        var conRegaloReal = GeneradorOpcionesJugador.Generar(new ContextoOpcionesJugador(
            perfil,
            Corazones: 2,
            Inventario: new[]
            {
                new ObjetoInventarioJugador("196", "Ensalada", EsRegalable: true, Cantidad: 1),
            },
            EncargosDisponibles: Array.Empty<PropuestaEncargo>(),
            MaxOpciones: 3));

        var regalo = Assert.Single(conRegaloReal, o => o.Categoria == "regalar");
        Assert.Equal("regalar:196", regalo.Id);
        Assert.Equal("196", regalo.ItemId);
    }

    [Fact]
    public void Nunca_ofrece_pedir_encargo_si_la_apertura_no_lo_permite()
    {
        var perfil = CargarPerfil();
        var encargos = new[]
        {
            new PropuestaEncargo("talla_bosque", "Pedirle una talla del bosque"),
        };

        var corazonesCerrados = perfil.Apertura
            .Where(t => !t.Encargos)
            .Select(t => t.DesdeCorazones)
            .DefaultIfEmpty(0)
            .First();
        var opcionesCerradas = GeneradorOpcionesJugador.Generar(new ContextoOpcionesJugador(
            perfil,
            corazonesCerrados,
            Inventario: Array.Empty<ObjetoInventarioJugador>(),
            EncargosDisponibles: encargos,
            MaxOpciones: 4));

        Assert.DoesNotContain(opcionesCerradas, o => o.Categoria == "encargo");

        var corazonesAbiertos = perfil.Apertura.First(t => t.Encargos).DesdeCorazones;
        var opcionesAbiertas = GeneradorOpcionesJugador.Generar(new ContextoOpcionesJugador(
            perfil,
            corazonesAbiertos,
            Inventario: Array.Empty<ObjetoInventarioJugador>(),
            EncargosDisponibles: encargos,
            MaxOpciones: 4));

        var encargo = Assert.Single(opcionesAbiertas, o => o.Categoria == "encargo");
        Assert.Equal("encargo:talla_bosque", encargo.Id);
        Assert.Equal("talla_bosque", encargo.EncargoId);
    }

    [Fact]
    public void Las_opciones_tienen_identificadores_estables_e_incluyen_decir_otra_cosa()
    {
        var perfil = CargarPerfil();
        var ctx = new ContextoOpcionesJugador(
            perfil,
            Corazones: 6,
            Inventario: new[] { new ObjetoInventarioJugador("196", "Ensalada", EsRegalable: true, Cantidad: 2) },
            EncargosDisponibles: new[] { new PropuestaEncargo("boceto_lago", "Pedir un boceto del lago") },
            MaxOpciones: 3);

        var primera = GeneradorOpcionesJugador.Generar(ctx);
        var segunda = GeneradorOpcionesJugador.Generar(ctx);

        Assert.Equal(primera.Select(o => o.Id), segunda.Select(o => o.Id));
        Assert.Equal(4, primera.Count); // 3 concretas + «Decir otra cosa…»
        Assert.Equal(GeneradorOpcionesJugador.IdDecirOtraCosa, primera[^1].Id);
        Assert.Equal(GeneradorOpcionesJugador.TextoDecirOtraCosa, primera[^1].Etiqueta);
    }

    [Fact]
    public void El_texto_libre_llega_a_la_mente_tal_cual_con_longitud_maxima_aplicada()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        long nextId = 1;

        var motor = new MotorHabitante(perfil, plantillas, estado, ModoReloj.Vanilla, publicarPulso: p =>
        {
            p.Id = nextId++;
            return p;
        })
        {
            Epoca = "epoca-cp504",
            MenteHabilitada = true,
        };

        var obs = new Observacion(
            Dia: 12,
            Hora: 1200,
            Ubicacion: "Forest",
            X: 33,
            Y: 25,
            Luz: 1.0,
            Clima: "soleado",
            Entidades: new[]
            {
                new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Álex", "Forest", 33, 26, new[] { "vecino" }, 4),
            });

        var pulsoExacto = motor.EnviarTextoLibreJugador(obs, "farmer:1", "  ¿Me dibujarías el lago?  ", ahoraMs: 1_000);
        Assert.NotNull(pulsoExacto);
        Assert.Equal(Motivos.Interlocucion, pulsoExacto!.Motivo);
        Assert.Equal("¿Me dibujarías el lago?", pulsoExacto.Interlocutor?.Texto);

        var largo = new string('á', GeneradorOpcionesJugador.LongitudMaximaTextoLibre + 40);
        var pulsoRecortado = motor.EnviarTextoLibreJugador(obs, "farmer:1", largo, ahoraMs: 2_000);
        Assert.NotNull(pulsoRecortado);
        Assert.Equal(GeneradorOpcionesJugador.LongitudMaximaTextoLibre, pulsoRecortado!.Interlocutor?.Texto.Length);
        Assert.All(pulsoRecortado.Interlocutor!.Texto, c => Assert.Equal('á', c));

        Assert.Null(motor.EnviarTextoLibreJugador(obs, "farmer:1", "   ", ahoraMs: 3_000));
    }
}
