using CorazonPelican.Core;

namespace CorazonPelican.Tests;

/// <summary>Guardas de arquitectura: Core es dominio puro (ADS §2).</summary>
public class ArquitecturaTests
{
    [Theory]
    [InlineData("Stardew Valley")]
    [InlineData("StardewValley")]
    [InlineData("StardewModdingAPI")]
    [InlineData("0Harmony")]
    [InlineData("HarmonyLib")]
    public void Core_no_referencia_juego_smapi_ni_harmony(string prohibido)
    {
        var referencias = typeof(CorazonCore).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(prohibido, referencias);
    }

    [Fact]
    public void Core_no_contiene_nada_especifico_de_leah()
    {
        var tipos = typeof(CorazonCore).Assembly.GetTypes().Select(t => t.FullName ?? t.Name);

        Assert.DoesNotContain(tipos, t => t.Contains("Leah", StringComparison.OrdinalIgnoreCase));
    }
}
