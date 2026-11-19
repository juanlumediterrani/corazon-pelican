using System;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Carga, migra y guarda <see cref="EstadoPartida"/> junto a la partida (solo anfitrión, ADS §9, R-10, CP-107).
    /// </summary>
    internal sealed class PersistenciaModule : IModule, IDisposable
    {
        private ModContext? context;

        public string Nombre => "Persistencia";

        public LadoModulo Lado => LadoModulo.Anfitrion;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;

            contexto.Helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            contexto.Helper.Events.GameLoop.Saving += this.OnSaving;
            contexto.Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        }

        public void Dispose()
        {
            if (this.context is null)
            {
                return;
            }

            this.context.Helper.Events.GameLoop.SaveLoaded -= this.OnSaveLoaded;
            this.context.Helper.Events.GameLoop.Saving -= this.OnSaving;
            this.context.Helper.Events.GameLoop.ReturnedToTitle -= this.OnReturnedToTitle;
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (this.context is null || !Context.IsMainPlayer)
            {
                return;
            }

            EstadoPartida estado;
            try
            {
                var guardado = this.context.Helper.Data.ReadSaveData<EstadoPartida>(PersistenciaEstado.ClaveGuardado);
                estado = guardado ?? new EstadoPartida();

                if (MigradorEstado.Migrar(estado))
                {
                    this.context.Monitor.Log(
                        $"Estado de Corazón Pelican migrado al esquema v{estado.VersionEsquema}.",
                        LogLevel.Info);
                }
            }
            catch (Exception ex)
            {
                this.context.Monitor.Log(
                    $"No se pudo cargar o migrar «{PersistenciaEstado.ClaveGuardado}» ({ex.Message}). Se inicia un estado vacío.",
                    LogLevel.Error);
                estado = new EstadoPartida();
            }

            foreach (var personajeId in this.context.PersonajesGobernados.Keys)
            {
                estado.ObtenerOIniciar(personajeId);
            }

            this.context.EstadoPartida = estado;
            this.context.Monitor.Log(
                $"Estado persistido cargado (personajes: {estado.Personajes.Count}, esquema v{estado.VersionEsquema}).",
                LogLevel.Info);
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (this.context is null || !Context.IsMainPlayer)
            {
                return;
            }

            try
            {
                this.context.Helper.Data.WriteSaveData(PersistenciaEstado.ClaveGuardado, this.context.EstadoPartida);
                this.context.Monitor.Log(
                    $"Estado de Corazón Pelican guardado (personajes: {this.context.EstadoPartida.Personajes.Count}, esquema v{this.context.EstadoPartida.VersionEsquema}).",
                    LogLevel.Trace);
            }
            catch (Exception ex)
            {
                this.context.Monitor.Log(
                    $"No se pudo guardar «{PersistenciaEstado.ClaveGuardado}»: {ex}",
                    LogLevel.Error);
            }
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
        {
            if (this.context is null)
            {
                return;
            }

            this.context.EstadoPartida = new EstadoPartida();
        }
    }
}
