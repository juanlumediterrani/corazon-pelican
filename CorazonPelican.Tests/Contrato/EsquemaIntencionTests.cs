using System.Text.Json;
using System.Text.Json.Nodes;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Contrato;

/// <summary>R-13: la mente no elige casillas ni rutas. El esquema de la respuesta y el validador de Core las rechazan.</summary>
public class EsquemaIntencionTests
{
    private static PerfilPersonaje PerfilConPasarelaMedida()
    {
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        var pasarela = perfil.Lexico.Lugares.Single(l => l.Id == "pasarela_lago");
        pasarela.Area = new() { 40, 30, 1, 1 };
        return perfil;
    }

    private static Observacion EscenaBase()
        => new(68, 1700, "Mountain", 40, 30, 1.0, "soleado", Array.Empty<EntidadObservada>());

    [Theory]
    [InlineData("{\"x\": 12, \"y\": 30}")]
    [InlineData("{\"tile\": [12, 30]}")]
    [InlineData("{\"casilla\": \"12,30\"}")]
    [InlineData("{\"ruta\": [\"Town\", \"Mountain\"]}")]
    public void Plan_con_coordenadas_es_invalido_en_esquema_y_en_core(string extra)
    {
        var respuesta = Esquemas.Leer(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--promesa.json"));
        var paso = new JsonObject { ["accion"] = "ir_a", ["lugar"] = "pasarela_lago" };
        foreach (var (clave, valor) in JsonNode.Parse(extra)!.AsObject())
        {
            paso[clave] = valor?.DeepClone();
        }

        respuesta["intencion"]!["plan"] = new JsonArray(paso);

        Assert.False(Esquemas.EsValido("respuesta", respuesta));

        var dto = System.Text.Json.JsonSerializer.Deserialize<Respuesta>(respuesta.ToJsonString())!;
        var perfil = PerfilConPasarelaMedida();
        var resultado = ValidadorIntencion.Validar(dto.Intencion, EscenaBase(), perfil);

        Assert.False(resultado.EsValido);
        Assert.Contains(resultado.Motivos, m => m.Codigo == "argumento_invalido" && m.Campo.StartsWith("intencion.plan[0].", StringComparison.Ordinal));
    }

    [Fact]
    public void Plan_solo_con_lexico_es_valido_en_esquema_y_en_core()
    {
        var respuesta = Esquemas.Leer(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--promesa.json"));
        respuesta["intencion"]!["plan"] = new JsonArray(new JsonObject { ["accion"] = "ir_a", ["lugar"] = "pasarela_lago" });

        Assert.True(Esquemas.EsValido("respuesta", respuesta));

        var dto = System.Text.Json.JsonSerializer.Deserialize<Respuesta>(respuesta.ToJsonString())!;
        var perfil = PerfilConPasarelaMedida();
        var resultado = ValidadorIntencion.Validar(dto.Intencion, EscenaBase(), perfil);

        Assert.True(resultado.EsValido);
    }
}
