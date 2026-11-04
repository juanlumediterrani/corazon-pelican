using System.Text.Json.Nodes;

namespace CorazonPelican.Tests.Contrato;

/// <summary>R-13: la mente no elige casillas ni rutas. El esquema de la respuesta no las admite.</summary>
public class EsquemaIntencionTests
{
    [Theory]
    [InlineData("{\"x\": 12, \"y\": 30}")]
    [InlineData("{\"tile\": [12, 30]}")]
    [InlineData("{\"casilla\": \"12,30\"}")]
    [InlineData("{\"ruta\": [\"Town\", \"Mountain\"]}")]
    public void Plan_con_coordenadas_es_invalido(string extra)
    {
        var respuesta = Esquemas.Leer(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--promesa.json"));
        var paso = new JsonObject { ["accion"] = "ir_a", ["lugar"] = "pasarela_lago" };
        foreach (var (clave, valor) in JsonNode.Parse(extra)!.AsObject())
        {
            paso[clave] = valor?.DeepClone();
        }

        respuesta["intencion"]!["plan"] = new JsonArray(paso);

        Assert.False(Esquemas.EsValido("respuesta", respuesta));
    }

    [Fact]
    public void Plan_solo_con_lexico_es_valido()
    {
        var respuesta = Esquemas.Leer(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--promesa.json"));
        respuesta["intencion"]!["plan"] = new JsonArray(new JsonObject { ["accion"] = "ir_a", ["lugar"] = "pasarela_lago" });

        Assert.True(Esquemas.EsValido("respuesta", respuesta));
    }
}
