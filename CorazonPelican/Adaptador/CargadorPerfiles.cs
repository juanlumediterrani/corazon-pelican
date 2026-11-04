using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorazonPelican.Core.Perfil;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CorazonPelican.Adaptador
{
    /// <summary>
    /// Publica los perfiles como asset <c>Mods/CorazonPelican/Personajes</c> (diccionario id → perfil),
    /// para que un pack de Content Patcher pueda editarlos o añadir personajes, y los valida al
    /// cargarlos: un perfil con errores no se gobierna (ADS §3.1, CP-003).
    /// </summary>
    internal sealed class CargadorPerfiles
    {
        public const string NombreAsset = "Mods/CorazonPelican/Personajes";

        private const string Carpeta = "assets/personajes";

        private readonly IModHelper helper;
        private readonly IMonitor monitor;

        public CargadorPerfiles(IModHelper helper, IMonitor monitor)
        {
            this.helper = helper;
            this.monitor = monitor;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        /// <summary>Carga el asset (con las ediciones de otros mods), valida cada perfil y devuelve los que se pueden gobernar.</summary>
        public IReadOnlyDictionary<string, PerfilPersonaje> CargarValidos(ModoValidacion modo)
        {
            var perfiles = this.helper.GameContent.Load<Dictionary<string, PerfilPersonaje>>(NombreAsset);
            var validos = new Dictionary<string, PerfilPersonaje>();
            foreach (var (clave, perfil) in perfiles)
            {
                var resultado = PerfilValidator.Validar(perfil, modo);
                if (clave != perfil.Id)
                {
                    resultado.Errores.Add(new HallazgoPerfil("id", $"la clave del asset «{clave}» no coincide con el id del perfil «{perfil.Id}»."));
                }

                foreach (var aviso in resultado.Avisos)
                {
                    this.monitor.Log($"Perfil «{clave}»: {aviso}", LogLevel.Warn);
                }

                foreach (var error in resultado.Errores)
                {
                    this.monitor.Log($"Perfil «{clave}»: {error}", LogLevel.Error);
                }

                if (resultado.EsValido)
                {
                    validos[clave] = perfil;
                }
                else
                {
                    this.monitor.Log($"Perfil «{clave}» rechazado ({resultado.Errores.Count} errores): ese personaje sigue siendo vanilla.", LogLevel.Error);
                }
            }

            return validos;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(NombreAsset))
            {
                e.LoadFrom(this.LeerPerfilesDelMod, AssetLoadPriority.Exclusive);
            }
        }

        /// <summary>Un perfil por fichero <c>assets/personajes/&lt;id&gt;.json</c>; los ficheros con otro punto (plantillas…) no son perfiles.</summary>
        private Dictionary<string, PerfilPersonaje> LeerPerfilesDelMod()
        {
            var perfiles = new Dictionary<string, PerfilPersonaje>();
            var carpeta = Path.Combine(this.helper.DirectoryPath, Carpeta);
            if (!Directory.Exists(carpeta))
            {
                return perfiles;
            }

            foreach (var fichero in Directory.GetFiles(carpeta, "*.json").Select(Path.GetFileName).OfType<string>()
                         .Where(f => f.Count(c => c == '.') == 1).OrderBy(f => f, StringComparer.Ordinal))
            {
                var perfil = this.helper.ModContent.Load<PerfilPersonaje>($"{Carpeta}/{fichero}");
                perfiles[Path.GetFileNameWithoutExtension(fichero)] = perfil;
            }

            return perfiles;
        }
    }
}
