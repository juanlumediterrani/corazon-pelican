using System.Text.Json;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Tests.Contrato;

/// <summary>P-1: una respuesta de otra carga de mundo se rechaza (ADS §4.1, paso 1).</summary>
public class ValidadorEpocaTests
{
    private static Respuesta Ejemplo()
        => JsonSerializer.Deserialize<Respuesta>(File.ReadAllText(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--promesa.json")))!;

    [Fact]
    public void Respuesta_de_la_epoca_vigente_pasa()
    {
        var respuesta = Ejemplo();

        Assert.Null(ValidadorEpoca.Comprobar(respuesta, respuesta.Epoca));
    }

    [Fact]
    public void Respuesta_de_otra_epoca_se_rechaza_con_codigo_y_prosa()
    {
        var motivo = ValidadorEpoca.Comprobar(Ejemplo(), "01JCNUEVAEPOCA0000000000000");

        Assert.NotNull(motivo);
        Assert.Equal("epoca_obsoleta", motivo!.Codigo);
        Assert.Equal("epoca", motivo.Campo);
        Assert.False(string.IsNullOrWhiteSpace(motivo.Prosa));
    }

    [Fact]
    public void Respuesta_sin_epoca_se_rechaza()
    {
        var respuesta = Ejemplo();
        respuesta.Epoca = "";

        Assert.NotNull(ValidadorEpoca.Comprobar(respuesta, "01JC8Q2M5V7K3T9W4X6Y8Z0A1B"));
    }
}
