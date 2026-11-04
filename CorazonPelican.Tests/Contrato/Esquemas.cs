using System.Text.Json.Nodes;
using Json.Schema;

namespace CorazonPelican.Tests.Contrato;

/// <summary>Carga <c>schemas/</c> (fuente única del contrato) y valida documentos contra él.</summary>
internal static class Esquemas
{
    public static readonly string[] Nombres = { "comun", "pulso", "respuesta", "obra", "perfil", "resultado", "salud" };

    public static readonly string Directorio = Repo.Ruta("schemas");

    private static readonly Dictionary<string, JsonSchema> Cargados = Cargar();

    public static string Ejemplos(string carpeta) => Path.Combine(Directorio, "ejemplos", carpeta);

    /// <summary>Convención de los ejemplos: <c>&lt;esquema&gt;--&lt;caso&gt;.json</c>.</summary>
    public static string EsquemaDe(string ruta) => Path.GetFileName(ruta).Split("--")[0];

    public static JsonNode Leer(string ruta) => JsonNode.Parse(File.ReadAllText(ruta))!;

    public static JsonSchema Esquema(string nombre) => Cargados[nombre];

    public static EvaluationResults Evaluar(string nombre, JsonNode? documento)
    {
        var opciones = new EvaluationOptions { OutputFormat = OutputFormat.List };
        foreach (var esquema in Cargados.Values)
        {
            opciones.SchemaRegistry.Register(esquema);
        }

        return Cargados[nombre].Evaluate(documento, opciones);
    }

    public static bool EsValido(string nombre, JsonNode? documento) => Evaluar(nombre, documento).IsValid;

    private static Dictionary<string, JsonSchema> Cargar()
    {
        var cargados = new Dictionary<string, JsonSchema>();
        foreach (var nombre in Nombres)
        {
            cargados[nombre] = JsonSchema.FromFile(Path.Combine(Directorio, $"{nombre}.schema.json"));
        }

        return cargados;
    }
}
