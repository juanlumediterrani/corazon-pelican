using CorazonPelican.Core;

namespace CorazonPelican.Tests;

/// <summary>Guardas de arquitectura: Core es dominio puro (ADS §2) y no contiene a ningún personaje (GSS §1.2).</summary>
public class ArquitecturaTests
{
    [Theory]
    [InlineData("Stardew Valley")]
    [InlineData("StardewValley")]
    [InlineData("StardewModdingAPI")]
    [InlineData("0Harmony")]
    [InlineData("HarmonyLib")]
    [InlineData("Newtonsoft.Json")]
    public void Core_no_referencia_juego_smapi_harmony_ni_bibliotecas_json(string prohibido)
    {
        var referencias = typeof(CorazonCore).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(prohibido, referencias);
    }

    /// <summary>Equivale a <c>grep -ri leah CorazonPelican.Core</c> (criterio de CP-003).</summary>
    [Theory]
    [InlineData("leah")]
    [InlineData("pierre")]
    public void El_codigo_de_core_no_nombra_personajes(string personaje)
    {
        var ficheros = Directory.GetFiles(Repo.Ruta("CorazonPelican.Core"), "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        var conMencion = ficheros.Where(f => File.ReadAllText(f).Contains(personaje, StringComparison.OrdinalIgnoreCase));

        Assert.Empty(conMencion);
    }
}
