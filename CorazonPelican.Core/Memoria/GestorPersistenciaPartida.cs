using System;
using System.Collections.Generic;
using System.IO;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Resultado de cargar el estado persistido de una partida (CP-110).</summary>
    public sealed record ResultadoCargaPartida(
        bool Exito,
        bool EsPartidaNueva,
        bool Migrada,
        bool GuardadoBloqueado,
        string PartidaId,
        string? Diagnostico,
        EstadoPartida Estado);

    /// <summary>Resultado de intentar guardar el estado de la partida actual (CP-110).</summary>
    public sealed record ResultadoGuardadoPartida(
        bool Guardado,
        bool OmitidoPorProteccion,
        string? PartidaAfectada,
        string? Motivo);

    /// <summary>
    /// Coordina la carga, migración y protección de guardado de <see cref="EstadoPartida"/>.
    /// Si falla la lectura, la migración o la validación de una partida existente, bloquea el
    /// guardado para no sobrescribir los datos originales en disco (CP-110).
    /// </summary>
    public sealed class GestorPersistenciaPartida
    {
        public EstadoPartida EstadoActual { get; private set; } = new();

        public bool GuardadoBloqueado { get; private set; }

        public string? PartidaAfectada { get; private set; }

        public string? MotivoBloqueo { get; private set; }

        /// <summary>
        /// Carga y migra el estado de <paramref name="saveId"/> usando <paramref name="leer"/>.
        /// Una partida nueva (<c>null</c>) y una partida válida desbloquean el guardado;
        /// ante cualquier error de lectura, migración o validación, el guardado queda bloqueado
        /// para esa partida sin afectar a sesiones futuras.
        /// </summary>
        public ResultadoCargaPartida Cargar(
            string saveId,
            Func<EstadoPartida?> leer,
            IEnumerable<string>? personajesGobernados = null)
        {
            if (leer is null)
            {
                throw new ArgumentNullException(nameof(leer));
            }

            var partidaId = string.IsNullOrWhiteSpace(saveId) ? "partida-desconocida" : saveId.Trim();

            this.GuardadoBloqueado = false;
            this.PartidaAfectada = null;
            this.MotivoBloqueo = null;

            try
            {
                var leido = leer();
                var esPartidaNueva = leido is null;
                var migrada = false;
                EstadoPartida estado;

                if (leido is null)
                {
                    estado = new EstadoPartida();
                }
                else
                {
                    migrada = MigradorEstado.Migrar(leido);
                    var errores = PersistenciaEstado.Validar(leido);
                    if (errores.Count > 0)
                    {
                        throw new InvalidDataException(
                            $"Estado inválido en «{PersistenciaEstado.ClaveGuardado}»: {string.Join("; ", errores)}");
                    }

                    estado = leido;
                }

                InicializarPersonajes(estado, personajesGobernados);
                this.EstadoActual = estado;

                return new ResultadoCargaPartida(
                    Exito: true,
                    EsPartidaNueva: esPartidaNueva,
                    Migrada: migrada,
                    GuardadoBloqueado: false,
                    PartidaId: partidaId,
                    Diagnostico: null,
                    Estado: estado);
            }
            catch (Exception ex)
            {
                var diagnostico =
                    $"No se pudo cargar o migrar «{PersistenciaEstado.ClaveGuardado}» en la partida «{partidaId}» ({ex.Message}). " +
                    "El guardado queda bloqueado en esta sesión para proteger los datos originales.";

                this.GuardadoBloqueado = true;
                this.PartidaAfectada = partidaId;
                this.MotivoBloqueo = diagnostico;

                var respaldo = new EstadoPartida();
                InicializarPersonajes(respaldo, personajesGobernados);
                this.EstadoActual = respaldo;

                return new ResultadoCargaPartida(
                    Exito: false,
                    EsPartidaNueva: false,
                    Migrada: false,
                    GuardadoBloqueado: true,
                    PartidaId: partidaId,
                    Diagnostico: diagnostico,
                    Estado: respaldo);
            }
        }

        /// <summary>
        /// Escribe <see cref="EstadoActual"/> mediante <paramref name="escribir"/> solo si el
        /// guardado no fue bloqueado tras un fallo de carga o migración.
        /// </summary>
        public ResultadoGuardadoPartida Guardar(Action<EstadoPartida> escribir)
        {
            if (escribir is null)
            {
                throw new ArgumentNullException(nameof(escribir));
            }

            if (this.GuardadoBloqueado)
            {
                return new ResultadoGuardadoPartida(
                    Guardado: false,
                    OmitidoPorProteccion: true,
                    PartidaAfectada: this.PartidaAfectada,
                    Motivo: this.MotivoBloqueo);
            }

            escribir(this.EstadoActual);
            return new ResultadoGuardadoPartida(
                Guardado: true,
                OmitidoPorProteccion: false,
                PartidaAfectada: null,
                Motivo: null);
        }

        /// <summary>Restablece el estado en memoria y levanta cualquier bloqueo previo (al volver al título).</summary>
        public void Limpiar()
        {
            this.GuardadoBloqueado = false;
            this.PartidaAfectada = null;
            this.MotivoBloqueo = null;
            this.EstadoActual = new EstadoPartida();
        }

        private static void InicializarPersonajes(EstadoPartida estado, IEnumerable<string>? personajesGobernados)
        {
            if (personajesGobernados is null)
            {
                return;
            }

            foreach (var personajeId in personajesGobernados)
            {
                if (!string.IsNullOrWhiteSpace(personajeId))
                {
                    estado.ObtenerOIniciar(personajeId);
                }
            }
        }
    }
}
