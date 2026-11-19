using System;
using System.Collections.Generic;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;
using StardewModdingAPI;

namespace CorazonPelican.Framework
{
    /// <summary>
    /// Contexto compartido por todos los módulos de Corazón Pelican (patrón de Pelican 4Ever, ADS §2).
    /// </summary>
    public sealed class ModContext
    {
        public const string Pelican4EverUniqueId = "Pelican4Ever.SVRTMP";

        public IModHelper Helper { get; }

        public IMonitor Monitor { get; }

        public IManifest Manifest { get; }

        public CorazonConfig Config { get; }

        /// <summary>Indica si el mod quedó activo tras validar <c>config.json</c>.</summary>
        public bool EstaActivo { get; internal set; }

        /// <summary>Indica si el motor autoritativo corre en la partida actual (solo anfitrión).</summary>
        public bool MotorActivoEnPartida { get; internal set; }

        /// <summary>Modo de reloj resuelto según <c>Config.ModoReloj</c> y si Pelican 4Ever está cargado.</summary>
        public ModoReloj ModoRelojEfectivo =>
            this.Config.ResolverModoReloj(this.Helper.ModRegistry.IsLoaded(Pelican4EverUniqueId));

        /// <summary>Todos los perfiles válidos cargados desde <c>Mods/CorazonPelican/Personajes</c>.</summary>
        public IReadOnlyDictionary<string, PerfilPersonaje> PerfilesValidos { get; internal set; } =
            new Dictionary<string, PerfilPersonaje>(StringComparer.Ordinal);

        /// <summary>Personajes configurados que tienen un perfil válido y quedan gobernados.</summary>
        public IReadOnlyDictionary<string, PerfilPersonaje> PersonajesGobernados { get; internal set; } =
            new Dictionary<string, PerfilPersonaje>(StringComparer.Ordinal);

        /// <summary>Personajes configurados en <c>config.json</c> sin perfil válido en el asset.</summary>
        public IReadOnlyList<string> PersonajesSinPerfil { get; internal set; } = Array.Empty<string>();

        /// <summary>Módulos registrados correctamente y módulos desactivados por fallo.</summary>
        public ResultadoRegistroModulos EstadoModulos { get; internal set; } =
            new(Array.Empty<string>(), Array.Empty<FalloModulo>());

        /// <summary>Estado observable de la API HTTP motor ↔ mente (lo actualizará <c>ApiModule</c> en CP-203).</summary>
        public string EstadoApi { get; set; } = "no iniciada (ApiModule pendiente)";

        public ModContext(IModHelper helper, IMonitor monitor, IManifest manifest, CorazonConfig config)
        {
            this.Helper = helper;
            this.Monitor = monitor;
            this.Manifest = manifest;
            this.Config = config;
            this.EstaActivo = true;
        }
    }
}
