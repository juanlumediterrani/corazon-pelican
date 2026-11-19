using System.Text;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Sensorio;

/// <summary>
/// Guarda de R-01 y de las escenas doradas (GSS §3.4, §13; ADR-0002).
/// Para regenerar los ficheros <c>.escena.txt</c> tras cambiar plantillas de forma intencionada:
/// <c>CORAZON_ACTUALIZAR_ESCENAS=1 dotnet test CorazonPelican.sln -p:EnableModDeploy=false -p:EnableModZip=false</c>.
/// </summary>
public class EscenaDeterministaTests
{
    private const string VariableActualizarEscenas = "CORAZON_ACTUALIZAR_ESCENAS";

    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Plantillas_de_leah_son_validas_y_tienen_al_menos_tres_variantes_por_foco()
    {
        var plantillas = CargarPlantillasLeah();

        var resultado = PlantillasProsa.Validar(plantillas);

        Assert.True(resultado.EsValido, string.Join("\n", resultado.Errores));
        foreach (CategoriaEntidad categoria in Enum.GetValues<CategoriaEntidad>())
        {
            var clave = PlantillasProsa.ClaveCategoria(categoria);
            Assert.True(
                plantillas.Entidades[clave].Foco.Count >= PlantillasProsa.MinimoVariantesFoco,
                $"La categoría {clave} necesita ≥{PlantillasProsa.MinimoVariantesFoco} variantes de foco.");
        }
    }

    [Fact]
    public void Plantillas_también_se_deserializan_con_newtonsoft()
    {
        var json = File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json"));

        var plantillas = JsonConvert.DeserializeObject<PlantillasProsa>(json)!;

        Assert.True(PlantillasProsa.Validar(plantillas).EsValido);
    }

    [Fact]
    public void Validador_rechaza_categoria_de_foco_con_menos_de_tres_variantes()
    {
        var plantillas = CargarPlantillasLeah();
        plantillas.Entidades["terreno"].Foco.RemoveAt(0);
        if (plantillas.Entidades["terreno"].Foco.Count >= 3)
        {
            plantillas.Entidades["terreno"].Foco = plantillas.Entidades["terreno"].Foco.Take(2).ToList();
        }

        var resultado = PlantillasProsa.Validar(plantillas);

        Assert.False(resultado.EsValido);
        Assert.Contains(resultado.Errores, e => e.Campo == "entidades.terreno.foco");
    }

    /// <summary>R-01: la misma observación y estado producen exactamente la misma salida byte a byte.</summary>
    [Fact]
    public void Misma_entrada_produce_misma_salida_byte_a_byte()
    {
        var perfil = CargarPerfilLeah();
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9).Construir(perfil.Lexico);
        var observacion = mundo.Observar();
        var contexto = new ContextoEscena
        {
            Estado = new EstadoAtencion(1.0, 0.55),
            AccionesDisponibles = perfil.Acciones,
            ObraEnCurso = "el boceto del sauce",
        };

        var primera = GeneradorEscena.Generar(observacion, perfil, CargarPlantillasLeah(), contexto: contexto);
        var segunda = GeneradorEscena.Generar(observacion, perfil, CargarPlantillasLeah(), contexto: contexto);

        Assert.Equal(Encoding.UTF8.GetBytes(primera.TextoCompleto), Encoding.UTF8.GetBytes(segunda.TextoCompleto));
        Assert.Equal(primera.Escena, segunda.Escena);
    }

    /// <summary>Dos días distintos con la misma observación no producen el mismo texto.</summary>
    [Theory]
    [InlineData(CategoriaEntidad.Granjero, "farmer:1", "Ana")]
    [InlineData(CategoriaEntidad.Npc, "npc:Elliott", "Elliott")]
    [InlineData(CategoriaEntidad.Animal, "animal:1", "una ardilla")]
    [InlineData(CategoriaEntidad.Objeto, "obj:Mountain:40,30:pasarela", "la pasarela de madera")]
    [InlineData(CategoriaEntidad.Terreno, "agua:Mountain:lago", "el lago")]
    public void Dias_distintos_con_misma_observacion_producen_texto_distinto_en_todas_las_categorias(
        CategoriaEntidad categoria,
        string referencia,
        string nombre)
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var lugares = perfil.Lexico.OfrecerLugares(_ => true);
        var bandas = new[] { new EntidadEnBanda(referencia, Banda.Foco, 0.85) };

        var escenas = new List<string>();
        foreach (var dia in new[] { 10, 11, 12 })
        {
            var obs = new Observacion(
                Dia: dia,
                Hora: 1500,
                Ubicacion: "Mountain",
                X: 40,
                Y: 30,
                Luz: 0.9,
                Clima: "soleado",
                Entidades: new[]
                {
                    new EntidadObservada(referencia, categoria, nombre, "Mountain", 40, 32, new[] { "agua" }, Corazones: 6),
                });
            escenas.Add(GeneradorEscena.Generar(obs, bandas, lugares, plantillas).Escena);
        }

        Assert.Equal(3, escenas.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Prosa_de_accion_y_huella_son_deterministas_y_varian_por_dia()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var lugares = perfil.Lexico.OfrecerLugares(_ => true);
        var nombres = new Dictionary<string, string> { ["agua:Mountain:lago"] = "el lago" };

        var pasos = new PasoPlan[]
        {
            new() { Accion = "ir_a", Lugar = "pasarela_lago" },
            new() { Accion = "sentarse", Lugar = "pasarela_lago" },
            new() { Accion = "mirar", Objetivo = "agua:Mountain:lago" },
            new() { Accion = "levantar_la_vista" },
            new() { Accion = "dibujar", Obra = "nueva" },
            new() { Accion = "guardar_cuaderno" },
            new() { Accion = "gesto", Tipo = "saludo" },
            new() { Accion = "murmurar", Texto = "qué quietud" },
            new() { Accion = "esperar", DuracionMin = 20 },
            new() { Accion = "volver_a_rutina" },
        };

        foreach (var paso in pasos)
        {
            var porDia = new[] { 1, 2, 3 }.Select(d => GeneradorAccion.GenerarAccion(paso, d, plantillas, lugares, nombres)).ToList();
            Assert.Equal(porDia[0], GeneradorAccion.GenerarAccion(paso, 1, plantillas, lugares, nombres));
            Assert.Equal(3, porDia.Distinct(StringComparer.Ordinal).Count());
        }

        var entidad = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });
        Assert.NotNull(GeneradorAccion.GenerarPercepcion(entidad, Banda.Mencion, 68, plantillas));
        Assert.Equal(
            GeneradorAccion.GenerarPercepcion(entidad, Banda.Foco, 68, plantillas),
            GeneradorAccion.GenerarPercepcion(entidad, Banda.Foco, 68, plantillas));
        Assert.Null(GeneradorAccion.GenerarPercepcion(entidad, Banda.Periferia, 68, plantillas));
        Assert.Null(GeneradorAccion.GenerarPercepcion(entidad, Banda.Ausente, 68, plantillas));

        var atendidas = new EntidadAtendida[]
        {
            new("agua:Mountain:lago", "el lago", 0.81),
            new("farmer:1", "Ana", 0.45),
        };
        var huellaA = GeneradorAccion.GenerarHuella(95, atendidas, 68, plantillas);
        var huellaB = GeneradorAccion.GenerarHuella(95, atendidas, 68, plantillas);
        Assert.Equal(huellaA.Prosa, huellaB.Prosa);
        Assert.Equal(95, huellaA.DesdeUltimoPensamientoMinJuego);
        Assert.Contains("el lago", huellaA.Prosa);
        Assert.Contains("Ana", huellaA.Prosa);
        Assert.NotEqual(huellaA.Prosa, GeneradorAccion.GenerarHuella(95, atendidas, 69, plantillas).Prosa);
    }

    public static TheoryData<string> CasosEscenasDoradas() => new()
    {
        "otono-pasarela",
        "lluvia-tarde",
        "noche-en-el-lago",
        "jugador-acercandose",
    };

    [Theory]
    [MemberData(nameof(CasosEscenasDoradas))]
    public void Escena_dorada_coincide_con_instantanea(string nombre)
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var (observacion, contexto, novedad) = ConstruirEscenaDorada(nombre, perfil);

        var generada = GeneradorEscena.Generar(observacion, perfil, plantillas, novedad: novedad, contexto: contexto);
        var actual = NormalizarSaltos(generada.TextoCompleto) + "\n";

        var ruta = Repo.Ruta("CorazonPelican.Tests", "Sensorio", "EscenasDoradas", $"{nombre}.escena.txt");
        if (Environment.GetEnvironmentVariable(VariableActualizarEscenas) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
            File.WriteAllText(ruta, actual, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(ruta), $"Falta la escena dorada {ruta}. Ejecuta con {VariableActualizarEscenas}=1 para generarla.");
        var esperada = NormalizarSaltos(File.ReadAllText(ruta, Encoding.UTF8));
        Assert.Equal(esperada, actual);
    }

    private static (Observacion Observacion, ContextoEscena Contexto, Novedad Novedad) ConstruirEscenaDorada(string nombre, PerfilPersonaje perfil)
    {
        var novedad = new Novedad();
        switch (nombre)
        {
            case "otono-pasarela":
            {
                // Escena fundacional (GSS §1, §3.4): otoño en la pasarela del lago al atardecer con viento.
                var obs = new Observacion(
                    Dia: 68,
                    Hora: 1740,
                    Ubicacion: "Mountain",
                    X: 40,
                    Y: 30,
                    Luz: 0.48,
                    Clima: "viento",
                    Entidades: new[]
                    {
                        new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" }),
                        new EntidadObservada("obj:Mountain:40,30:pasarela", CategoriaEntidad.Objeto, "la pasarela de madera", "Mountain", 40, 30, new[] { "madera" }),
                        new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 21, new[] { "persona" }, Corazones: 6),
                        new EntidadObservada("npc:Linus", CategoriaEntidad.Npc, "Linus", "Mountain", 5, 2, new[] { "persona" }, Corazones: 1, Visible: false),
                    });
                var ctx = new ContextoEscena
                {
                    Estado = new EstadoAtencion(1.0, 0.75),
                    AccionesDisponibles = perfil.Acciones,
                    ObraEnCurso = "el boceto del sauce",
                    PesosIntencion = new Dictionary<string, double> { ["agua"] = 2.0 },
                };
                return (obs, ctx, novedad);
            }

            case "lluvia-tarde":
            {
                // Tarde de lluvia en la mesa de esculpir, absorta (apertura estrecha).
                var obs = new Observacion(
                    Dia: 40,
                    Hora: 1520,
                    Ubicacion: "LeahHouse",
                    X: 3,
                    Y: 7,
                    Luz: 0.78,
                    Clima: "lluvia",
                    Entidades: new[]
                    {
                        new EntidadObservada("obj:LeahHouse:3,7:mesa", CategoriaEntidad.Objeto, "tu mesa de esculpir", "LeahHouse", 3, 7, new[] { "madera" }),
                        new EntidadObservada("obj:LeahHouse:4,7:talla", CategoriaEntidad.Objeto, "la pieza de madera", "LeahHouse", 4, 7, new[] { "madera" }),
                        new EntidadObservada("npc:Elliott", CategoriaEntidad.Npc, "Elliott", "LeahHouse", 14, 15, new[] { "persona" }, Corazones: 2, Visible: false),
                    });
                var ctx = new ContextoEscena
                {
                    Estado = EstadoAtencion.Estrecha,
                    AccionesDisponibles = perfil.Acciones,
                    IntencionActiva = "terminar el corte lateral",
                };
                return (obs, ctx, novedad);
            }

            case "noche-en-el-lago":
            {
                // Noche fría de invierno en el lago; poca energía y un conocido en periferia.
                var obs = new Observacion(
                    Dia: 95,
                    Hora: 2130,
                    Ubicacion: "Mountain",
                    X: 40,
                    Y: 30,
                    Luz: 0.20,
                    Clima: "nieve",
                    Entidades: new[]
                    {
                        new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" }),
                        new EntidadObservada("npc:Linus", CategoriaEntidad.Npc, "Linus", "Mountain", 40, 12, new[] { "persona" }, Corazones: 2, Visible: true),
                    });
                var ctx = new ContextoEscena
                {
                    Estado = new EstadoAtencion(0.8, 0.25),
                    AccionesDisponibles = new[] { "ir_a", "esperar", "volver_a_rutina" },
                };
                return (obs, ctx, novedad);
            }

            case "jugador-acercandose":
            {
                // Mañana de primavera en el claro del bosque: un granjero con alta amistad muy cerca (foco).
                var obs = new Observacion(
                    Dia: 10,
                    Hora: 1100,
                    Ubicacion: "Forest",
                    X: 33,
                    Y: 25,
                    Luz: 1.0,
                    Clima: "soleado",
                    Entidades: new[]
                    {
                        new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Forest", 33, 22, new[] { "persona" }, Corazones: 9),
                        new EntidadObservada("terreno:Forest:arroyo", CategoriaEntidad.Terreno, "el arroyo", "Forest", 28, 25, new[] { "agua" }),
                        new EntidadObservada("animal:Forest:ardilla", CategoriaEntidad.Animal, "una ardilla", "Forest", 47, 25, Array.Empty<string>()),
                    });
                var ctx = new ContextoEscena
                {
                    Estado = EstadoAtencion.Abierta,
                    AccionesDisponibles = perfil.Acciones,
                    PesosIntencion = new Dictionary<string, double> { ["persona"] = 2.0 },
                };
                return (obs, ctx, novedad);
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(nombre), nombre, null);
        }
    }

    private static string NormalizarSaltos(string texto) => texto.Replace("\r\n", "\n", StringComparison.Ordinal);
}
