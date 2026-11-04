using StardewModdingAPI;

namespace CorazonPelican
{
    /// <summary>Punto de entrada del mod. Los módulos (ADS §2) se registran a partir de CP-005.</summary>
    public sealed class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            this.Monitor.Log($"Corazón Pelican cargado (contrato {Core.CorazonCore.VersionContrato}, sin módulos todavía).", LogLevel.Info);
        }
    }
}
