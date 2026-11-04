using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Tests.Contrato;

/// <summary>El contrato de <c>schemas/</c> y los DTOs de <c>Core/Contrato</c> dicen lo mismo (ADS §2).</summary>
public class ContratoTests
{
    private static readonly JsonSerializerOptions Serializacion = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly Dictionary<string, Type> Dtos = new()
    {
        ["pulso"] = typeof(Pulso),
        ["respuesta"] = typeof(Respuesta),
        ["obra"] = typeof(Obra),
        ["resultado"] = typeof(Resultado),
        ["salud"] = typeof(Salud),
    };

    public static TheoryData<string> Validos() => Ejemplos("validos");

    public static TheoryData<string> Invalidos() => Ejemplos("invalidos");

    public static TheoryData<string> ValidosConDto()
    {
        var datos = new TheoryData<string>();
        foreach (var ruta in Directory.GetFiles(Esquemas.Ejemplos("validos"), "*.json").Order())
        {
            if (Dtos.ContainsKey(Esquemas.EsquemaDe(ruta)))
            {
                datos.Add(Path.GetFileName(ruta));
            }
        }

        return datos;
    }

    [Theory]
    [MemberData(nameof(Validos))]
    public void Ejemplo_valido_valida(string fichero)
    {
        var ruta = Path.Combine(Esquemas.Ejemplos("validos"), fichero);

        var resultado = Esquemas.Evaluar(Esquemas.EsquemaDe(ruta), Esquemas.Leer(ruta));

        Assert.True(resultado.IsValid, Describir(resultado));
    }

    [Theory]
    [MemberData(nameof(Invalidos))]
    public void Ejemplo_invalido_no_valida(string fichero)
    {
        var ruta = Path.Combine(Esquemas.Ejemplos("invalidos"), fichero);

        Assert.False(Esquemas.EsValido(Esquemas.EsquemaDe(ruta), Esquemas.Leer(ruta)));
    }

    /// <summary>
    /// Ida y vuelta por el DTO sin perder nada y sigue validando: si el esquema gana o pierde un
    /// campo y el DTO no, este test falla.
    /// </summary>
    [Theory]
    [MemberData(nameof(ValidosConDto))]
    public void Dto_conserva_el_ejemplo_y_sigue_validando(string fichero)
    {
        var ruta = Path.Combine(Esquemas.Ejemplos("validos"), fichero);
        var esquema = Esquemas.EsquemaDe(ruta);
        var original = Esquemas.Leer(ruta);

        var dto = original.Deserialize(Dtos[esquema])!;
        var vuelta = JsonSerializer.SerializeToNode(dto, Dtos[esquema], Serializacion);

        Assert.True(JsonNode.DeepEquals(original, vuelta), $"El DTO {Dtos[esquema].Name} no conserva {fichero}:\n{vuelta}");
        Assert.True(Esquemas.EsValido(esquema, vuelta));
    }

    /// <summary>Un cambio en los campos obligatorios de un esquema obliga a actualizar la instantánea compartida.</summary>
    [Fact]
    public void Campos_obligatorios_coinciden_con_la_instantanea()
    {
        var instantanea = Esquemas.Leer(Path.Combine(Esquemas.Directorio, "ejemplos", "obligatorios.json")).AsObject();

        foreach (var nombre in Esquemas.Nombres.Where(n => n != "comun"))
        {
            var esquema = JsonNode.Parse(File.ReadAllText(Path.Combine(Esquemas.Directorio, $"{nombre}.schema.json")))!;
            var reales = esquema["required"]!.AsArray().Select(n => n!.GetValue<string>());
            var esperados = instantanea[nombre]!.AsArray().Select(n => n!.GetValue<string>());

            Assert.Equal(esperados, reales);
        }
    }

    [Fact]
    public void Pensamiento_es_la_primera_propiedad_de_la_respuesta()
    {
        var esquema = JsonNode.Parse(File.ReadAllText(Path.Combine(Esquemas.Directorio, "respuesta.schema.json")))!;

        var primera = esquema["properties"]!.AsObject().First().Key;

        Assert.Equal("pensamiento", primera);
    }

    private static TheoryData<string> Ejemplos(string carpeta)
    {
        var datos = new TheoryData<string>();
        foreach (var ruta in Directory.GetFiles(Esquemas.Ejemplos(carpeta), "*.json").Order())
        {
            datos.Add(Path.GetFileName(ruta));
        }

        return datos;
    }

    private static string Describir(Json.Schema.EvaluationResults resultado)
        => string.Join("\n", (resultado.Details ?? Array.Empty<Json.Schema.EvaluationResults>())
            .Where(d => d.HasErrors)
            .SelectMany(d => d.Errors!.Select(e => $"{d.InstanceLocation}: {e.Value}")));
}
