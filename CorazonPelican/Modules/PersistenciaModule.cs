using System;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Carga, migra y guarda <see cref="EstadoPartida"/> junto a la partida (solo anfitrión, ADS §9, R-10, CP-107, CP-110).
    /// </summary>
    internal sealed class PersistenciaModule : IModule, IDisposable
    {
        private readonly GestorPersistenciaPartida gestor = new();
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

            var saveId = string.IsNullOrWhiteSpace(Constants.SaveFolderName)
                ? "partida-desconocida"
                : Constants.SaveFolderName;

            var resultado = this.gestor.Cargar(
                saveId,
                () => this.context.Helper.Data.ReadSaveData<EstadoPartida>(PersistenciaEstado.ClaveGuardado),
                this.context.PersonajesGobernados.Keys);

            this.context.EstadoPartida = resultado.Estado;

            if (!resultado.Exito)
            {
                this.context.Monitor.Log(resultado.Diagnostico!, LogLevel.Error);
                return;
            }

            if (resultado.Migrada)
            {
                this.context.Monitor.Log(
                    $"Estado de Corazón Pelican migrado al esquema v{resultado.Estado.VersionEsquema}.",
                    LogLevel.Info);
            }

            this.context.Monitor.Log(
                $"Estado persistido cargado (partida: {resultado.PartidaId}, personajes: {resultado.Estado.Personajes.Count}, esquema v{resultado.Estado.VersionEsquema}).",
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
                var resultado = this.gestor.Guardar(estado =>
                    this.context.Helper.Data.WriteSaveData(PersistenciaEstado.ClaveGuardado, estado));

                if (resultado.OmitidoPorProteccion)
                {
                    this.context.Monitor.Log(
                        $"Guardado omitido para proteger la memoria original de la partida «{resultado.PartidaAfectada}»: {resultado.Motivo}",
                        LogLevel.Warn);
                    return;
                }

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
            this.gestor.Limpiar();
            if (this.context is null)
            {
                return;
            }

            this.context.EstadoPartida = this.gestor.EstadoActual;
        }
    }
}
