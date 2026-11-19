using System;
using CorazonPelican.Core.Api;
using CorazonPelican.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Módulo anfitrión de la API HTTP loopback motor ↔ mente (ADS §5, R-05, CP-203).
    /// Arranca el servidor en loopback, genera una época nueva en cada <c>SaveLoaded</c>
    /// y drena la cola de acciones en <c>UpdateTicked</c>.
    /// </summary>
    internal sealed class ApiModule : IModule, IDisposable
    {
        private ModContext? context;
        private ServidorApiMotor? servidor;

        public string Nombre => "Api";

        public LadoModulo Lado => LadoModulo.Anfitrion;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;

            contexto.Api.RegistrarProveedorEstado(
                personaje => contexto.EstadoPartida.ObtenerSiExiste(personaje),
                enHiloPrincipal: true);

            contexto.Helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            contexto.Helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            contexto.Helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            contexto.Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        }

        public void Dispose()
        {
            if (this.context is not null)
            {
                this.context.Helper.Events.GameLoop.GameLaunched -= this.OnGameLaunched;
                this.context.Helper.Events.GameLoop.SaveLoaded -= this.OnSaveLoaded;
                this.context.Helper.Events.GameLoop.UpdateTicked -= this.OnUpdateTicked;
                this.context.Helper.Events.GameLoop.ReturnedToTitle -= this.OnReturnedToTitle;
                this.context.Api.MarcarSinMundo("Módulo API detenido.");
            }

            this.DetenerServidor("detenida");
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            this.IniciarServidor();
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (this.context is null)
            {
                return;
            }

            if (!Context.IsMainPlayer)
            {
                this.context.Api.MarcarSinMundo("Sesión iniciada como cliente multijugador.");
                this.DetenerServidor("detenida (cliente multijugador)");
                return;
            }

            this.IniciarServidor();

            var nuevaEpoca = ApiMotor.GenerarEpoca();
            this.context.Api.IniciarEpoca(nuevaEpoca, this.context.PersonajesGobernados.Keys);

            if (this.servidor is { EstaEscuchando: true })
            {
                this.context.EstadoApi = $"activa en {this.servidor.Prefijo} (época {nuevaEpoca})";
                this.context.Monitor.Log(
                    $"API loopback activa en {this.servidor.Prefijo} (época {nuevaEpoca}, personajes: {this.context.PersonajesGobernados.Count}).",
                    LogLevel.Info);
            }
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            this.context.Api.DrenarCola();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            if (this.context is null)
            {
                return;
            }

            this.context.Api.MarcarSinMundo("Vuelta a la pantalla de título.");
            this.IniciarServidor();
        }

        private void IniciarServidor()
        {
            if (this.context is null)
            {
                return;
            }

            if (this.servidor is { EstaEscuchando: true })
            {
                this.context.EstadoApi = this.context.Api.MundoCargado
                    ? $"activa en {this.servidor.Prefijo} (época {this.context.Api.EpocaVigente})"
                    : $"activa en {this.servidor.Prefijo} (sin partida)";
                return;
            }

            try
            {
                this.servidor?.Dispose();
                this.servidor = new ServidorApiMotor(
                    this.context.Api,
                    this.context.Config.ApiPuerto,
                    this.context.Config.ApiToken,
                    log: msg => this.context.Monitor.Log(msg, LogLevel.Warn));

                this.servidor.Iniciar();
                this.context.EstadoApi = this.context.Api.MundoCargado
                    ? $"activa en {this.servidor.Prefijo} (época {this.context.Api.EpocaVigente})"
                    : $"activa en {this.servidor.Prefijo} (sin partida)";
                this.context.Monitor.Log($"Servidor API loopback iniciado en {this.servidor.Prefijo}.", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                this.servidor?.Dispose();
                this.servidor = null;
                this.context.EstadoApi = $"error al iniciar ({ex.Message})";
                this.context.Monitor.Log(
                    $"No se pudo iniciar la API HTTP en el puerto {this.context.Config.ApiPuerto}: {ex.Message}",
                    LogLevel.Error);
            }
        }

        private void DetenerServidor(string motivoEstado)
        {
            try
            {
                this.servidor?.Detener();
                this.servidor?.Dispose();
            }
            catch
            {
            }
            finally
            {
                this.servidor = null;
                if (this.context is not null)
                {
                    this.context.EstadoApi = motivoEstado;
                }
            }
        }
    }
}
