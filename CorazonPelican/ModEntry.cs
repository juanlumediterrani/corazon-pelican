using CorazonPelican.Adaptador;
using CorazonPelican.Core.Perfil;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CorazonPelican
{
    /// <summary>Punto de entrada del mod. El registro de módulos (ADS §2) llega con CP-005.</summary>
    public sealed class ModEntry : Mod
    {
        private CargadorPerfiles perfiles = null!;

        public override void Entry(IModHelper helper)
        {
            this.perfiles = new CargadorPerfiles(helper, this.Monitor);
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            // Modo normal hasta que CP-005 añada el modo de desarrollo a la configuración.
            var validos = this.perfiles.CargarValidos(ModoValidacion.Normal);
            this.Monitor.Log(
                $"Corazón Pelican cargado (contrato {Core.CorazonCore.VersionContrato}); perfiles válidos: {(validos.Count == 0 ? "ninguno" : string.Join(", ", validos.Keys))}.",
                LogLevel.Info);
        }
    }
}
