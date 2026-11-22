using System;
using System.Collections.Generic;
using CorazonPelican.Adaptador;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Observabilidad;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
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

        /// <summary>Estado y cola de acciones de la API motor ↔ mente (CP-203).</summary>
        public ApiMotor Api { get; }

        /// <summary>Estado observable de la API HTTP motor ↔ mente (actualizado por <c>ApiModule</c>).</summary>
        public string EstadoApi { get; set; } = "no iniciada";

        /// <summary>Contador de coste por paso del bucle del motor (ADS §4, CP-101).</summary>
        public MedidorCosteBucle CostesBucle { get; } = new();

        /// <summary>Última observación recogida por personaje gobernado.</summary>
        public Dictionary<string, Observacion> UltimasObservaciones { get; } = new(StringComparer.Ordinal);

        /// <summary>Sucesos de Pelican 4Ever encolados en segundo plano para la siguiente observación (CP-602).</summary>
        public ColaSucesosPelican SucesosPelican { get; } = new();

        /// <summary>Motores por personaje gobernado en la partida actual (solo anfitrión, CP-109).</summary>
        public Dictionary<string, MotorHabitante> MotoresHabitante { get; } = new(StringComparer.Ordinal);

        /// <summary>Cuerpos nativos por personaje gobernado en la partida actual (solo anfitrión, CP-302).</summary>
        public Dictionary<string, CuerpoNpc> CuerposHabitante { get; } = new(StringComparer.Ordinal);

        /// <summary>Escritor de telemetría JSONL de la partida actual (solo anfitrión, CP-109).</summary>
        public TelemetriaJsonl? Telemetria { get; internal set; }

        /// <summary>Proveedor en vivo de observación para comandos de consola (<c>cp_escena</c>, <c>cp_pulso</c>).</summary>
        public Func<string, Observacion?>? ProveedorObservacion { get; internal set; }

        /// <summary>Reloj monotónico en milisegundos reales del bucle del anfitrión.</summary>
        public Func<long>? RelojMs { get; internal set; }

        /// <summary>Estado persistido por partida de todos los personajes gobernados (ADS §9, CP-107).</summary>
        public EstadoPartida EstadoPartida { get; internal set; } = new();

        /// <summary>
        /// Devuelve el estado persistido de <paramref name="personajeId"/> en la partida actual,
        /// creándolo si aún no existía (compartido entre <c>PersistenciaModule</c>, <c>HabitanteModule</c> y <c>ApiModule</c>).
        /// </summary>
        public EstadoPersonaje ObtenerOIniciarEstado(string personajeId)
            => this.EstadoPartida.ObtenerOIniciar(personajeId);

        public ModContext(IModHelper helper, IMonitor monitor, IManifest manifest, CorazonConfig config)
        {
            this.Helper = helper;
            this.Monitor = monitor;
            this.Manifest = manifest;
            this.Config = config;
            this.Api = new ApiMotor(manifest.Version.ToString());
            this.EstaActivo = true;
        }
    }
}
