using System;
using System.Collections.Generic;
using System.IO;
using CorazonPelican.Adaptador;
using CorazonPelican.Core;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Framework;
using CorazonPelican.Modules;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CorazonPelican
{
    /// <summary>Punto de entrada del mod (ADS §2, CP-005).</summary>
    public sealed class ModEntry : Mod
    {
        private readonly List<IDisposable> modulosDescartables = new();
        private CargadorPerfiles? perfiles;
        private ModContext? context;

        public override void Entry(IModHelper helper)
        {
            CorazonConfig config;
            try
            {
                var rutaConfig = Path.Combine(helper.DirectoryPath, "config.json");
                var existiaConfig = File.Exists(rutaConfig);
                config = helper.ReadConfig<CorazonConfig>();
                if (!existiaConfig && config.Personajes.Count == 0)
                {
                    config.Personajes = new List<string> { "leah" };
                    helper.WriteConfig(config);
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"No se pudo leer config.json ({ex.Message}). Corazón Pelican queda INACTIVO.", LogLevel.Error);
                return;
            }

            var errores = config.Validate();
            if (errores.Count > 0)
            {
                foreach (var error in errores)
                {
                    this.Monitor.Log($"config.json: {error}", LogLevel.Error);
                }

                this.Monitor.Log("Configuración inválida: Corazón Pelican queda INACTIVO hasta corregir config.json.", LogLevel.Error);
                return;
            }

            this.perfiles = new CargadorPerfiles(helper, this.Monitor);
            this.context = new ModContext(helper, this.Monitor, this.ModManifest, config);

            this.RegistrarModulos(
                new PersistenciaModule(),
                new ApiModule(),
                new HabitanteModule(),
                new ConsolaModule());

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        }

        private void RegistrarModulos(params IModule[] modulos)
        {
            if (this.context is null)
            {
                return;
            }

            this.context.EstadoModulos = RegistroModulos.RegistrarTodos(
                modulos,
                m => m.Nombre,
                m =>
                {
                    m.Registrar(this.context);
                    if (m is IDisposable descartable)
                    {
                        this.modulosDescartables.Add(descartable);
                    }
                },
                nombre => this.Monitor.Log($"Módulo «{nombre}» registrado.", LogLevel.Trace),
                fallo => this.Monitor.Log($"Módulo «{fallo.Nombre}» falló al registrarse y queda desactivado:\n{fallo.Error}", LogLevel.Error));
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            if (this.context is null || this.perfiles is null)
            {
                return;
            }

            var modo = this.context.Config.ModoDesarrollo ? ModoValidacion.Desarrollo : ModoValidacion.Normal;
            var validos = this.perfiles.CargarValidos(modo);
            var seleccion = this.context.Config.SeleccionarPersonajes(validos);

            this.context.PerfilesValidos = validos;
            this.context.PersonajesGobernados = seleccion.Gobernados;
            this.context.PersonajesSinPerfil = seleccion.SinPerfil;

            foreach (var ausente in seleccion.SinPerfil)
            {
                this.Monitor.Log(
                    $"Personaje configurado «{ausente}» no tiene un perfil válido en {CargadorPerfiles.NombreAsset}: no será gobernado.",
                    LogLevel.Error);
            }

            var gobernados = seleccion.Gobernados.Count == 0
                ? "ninguno"
                : string.Join(", ", seleccion.Gobernados.Keys);
            this.Monitor.Log(
                $"Corazón Pelican cargado (contrato {CorazonCore.VersionContrato}, validación {modo}, reloj {this.context.Config.ModoReloj}->{this.context.ModoRelojEfectivo}); personajes gobernados: {gobernados}.",
                LogLevel.Info);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (this.context is null)
            {
                return;
            }

            if (!Context.IsMainPlayer)
            {
                this.context.MotorActivoEnPartida = false;
                this.Monitor.Log("Sesión como cliente multijugador: el motor de Corazón Pelican solo corre en el anfitrión.", LogLevel.Info);
                return;
            }

            this.context.CostesBucle.Reiniciar();
            this.context.UltimasObservaciones.Clear();
            this.context.MotorActivoEnPartida = true;
            this.Monitor.Log(
                $"Motor activo en anfitrión (personajes gobernados: {this.context.PersonajesGobernados.Count}, mente: {(this.context.Config.MenteHabilitada ? "on" : "off")}).",
                LogLevel.Info);
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            if (this.context is null)
            {
                return;
            }

            this.context.MotorActivoEnPartida = false;
            this.context.CostesBucle.Reiniciar();
            this.context.UltimasObservaciones.Clear();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var modulo in this.modulosDescartables)
                {
                    try
                    {
                        modulo.Dispose();
                    }
                    catch
                    {
                    }
                }

                this.modulosDescartables.Clear();
            }

            base.Dispose(disposing);
        }
    }
}
