using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>
    /// Migraciones puras por <c>version_esquema</c> y normalización de invariantes de
    /// <see cref="EstadoPartida"/> y <see cref="EstadoPersonaje"/> (ADS §9, CP-107).
    /// </summary>
    public static class MigradorEstado
    {
        public const int VersionEsquemaActual = EstadoPartida.VersionActual;

        /// <summary>
        /// Migra <paramref name="estado"/> y todos sus personajes hasta <see cref="VersionEsquemaActual"/>.
        /// Devuelve <c>true</c> si se aplicó alguna migración o reparación.
        /// </summary>
        /// <exception cref="NotSupportedException">Si la versión de esquema es negativa o superior a la soportada.</exception>
        public static bool Migrar(EstadoPartida estado)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            ValidarVersionSoportada(estado.VersionEsquema, EstadoPartida.VersionActual, "partida");

            var cambiado = false;

            // v0/v1 -> v2: almacén por partida versionado, contadores de presupuesto y entidades en episodios.
            if (estado.VersionEsquema < 2)
            {
                estado.Personajes ??= new Dictionary<string, EstadoPersonaje>(StringComparer.Ordinal);
                estado.VersionEsquema = 2;
                cambiado = true;
            }

            if (estado.Personajes is null)
            {
                estado.Personajes = new Dictionary<string, EstadoPersonaje>(StringComparer.Ordinal);
                cambiado = true;
            }

            foreach (var (id, personaje) in estado.Personajes.ToList())
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    estado.Personajes.Remove(id);
                    cambiado = true;
                    continue;
                }

                if (personaje is null)
                {
                    estado.Personajes[id] = new EstadoPersonaje();
                    cambiado = true;
                    continue;
                }

                if (Migrar(personaje))
                {
                    cambiado = true;
                }
            }

            return cambiado;
        }

        /// <summary>
        /// Migra un <see cref="EstadoPersonaje"/> individual hasta <see cref="EstadoPersonaje.VersionActual"/>
        /// y garantiza que sus colecciones e ids de episodio sean consistentes.
        /// </summary>
        /// <exception cref="NotSupportedException">Si la versión de esquema es negativa o superior a la soportada.</exception>
        public static bool Migrar(EstadoPersonaje estado)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            ValidarVersionSoportada(estado.VersionEsquema, EstadoPersonaje.VersionActual, "personaje");

            var cambiado = false;

            // v0/v1 -> v2: normalización de colecciones, entidades de episodio, SiguienteIdEpisodio y contadores diarios.
            if (estado.VersionEsquema < 2)
            {
                estado.VersionEsquema = 2;
                cambiado = true;
            }

            if (estado.Diario is null)
            {
                estado.Diario = new List<EpisodioDiario>();
                cambiado = true;
            }

            if (estado.Recuerdos is null)
            {
                estado.Recuerdos = new List<Recuerdo>();
                cambiado = true;
            }

            if (estado.Relaciones is null)
            {
                estado.Relaciones = new Dictionary<string, RelacionEstado>(StringComparer.Ordinal);
                cambiado = true;
            }

            if (estado.Portafolio is null)
            {
                estado.Portafolio = new List<ObraPortafolio>();
                cambiado = true;
            }

            if (estado.Compromisos is null)
            {
                estado.Compromisos = new List<CompromisoEstado>();
                cambiado = true;
            }

            if (estado.Diario.RemoveAll(e => e is null) > 0)
            {
                cambiado = true;
            }

            if (estado.Recuerdos.RemoveAll(r => r is null) > 0)
            {
                cambiado = true;
            }

            if (estado.Portafolio.RemoveAll(o => o is null) > 0)
            {
                cambiado = true;
            }

            if (estado.Compromisos.RemoveAll(c => c is null) > 0)
            {
                cambiado = true;
            }

            foreach (var (clave, relacion) in estado.Relaciones.ToList())
            {
                if (string.IsNullOrWhiteSpace(clave) || relacion is null)
                {
                    estado.Relaciones.Remove(clave);
                    cambiado = true;
                }
            }

            var idsExistentes = new HashSet<string>(StringComparer.Ordinal);
            var maxNumeroEpisodio = 0;

            foreach (var episodio in estado.Diario)
            {
                if (episodio.Entidades is null)
                {
                    episodio.Entidades = new List<string>();
                    cambiado = true;
                }

                if (episodio.Saliencia < 0)
                {
                    episodio.Saliencia = 0;
                    cambiado = true;
                }

                if (!string.IsNullOrWhiteSpace(episodio.Id))
                {
                    idsExistentes.Add(episodio.Id);
                    if (TryParseNumeroEpisodio(episodio.Id, out var numero))
                    {
                        maxNumeroEpisodio = Math.Max(maxNumeroEpisodio, numero);
                    }
                }
            }

            var siguienteNumero = Math.Max(1, Math.Max(estado.SiguienteIdEpisodio, maxNumeroEpisodio + 1));
            foreach (var episodio in estado.Diario)
            {
                if (!string.IsNullOrWhiteSpace(episodio.Id))
                {
                    continue;
                }

                string nuevoId;
                do
                {
                    nuevoId = $"ep-{siguienteNumero}";
                    siguienteNumero++;
                }
                while (!idsExistentes.Add(nuevoId));

                episodio.Id = nuevoId;
                cambiado = true;
            }

            var siguienteMinimo = Math.Max(siguienteNumero, maxNumeroEpisodio + 1);
            if (estado.SiguienteIdEpisodio < siguienteMinimo)
            {
                estado.SiguienteIdEpisodio = siguienteMinimo;
                cambiado = true;
            }

            var idsRecuerdosExistentes = new HashSet<string>(
                estado.Recuerdos.Where(r => !string.IsNullOrWhiteSpace(r.Id)).Select(r => r.Id),
                StringComparer.Ordinal);
            var siguienteRecuerdo = 1;
            foreach (var r in estado.Recuerdos)
            {
                if (!string.IsNullOrWhiteSpace(r.Id)
                    && r.Id.StartsWith("rec-", StringComparison.Ordinal)
                    && int.TryParse(r.Id.AsSpan(4), NumberStyles.None, CultureInfo.InvariantCulture, out var numRec)
                    && numRec >= siguienteRecuerdo)
                {
                    siguienteRecuerdo = numRec + 1;
                }
            }

            foreach (var recuerdo in estado.Recuerdos)
            {
                if (recuerdo.OrigenDiario is null)
                {
                    recuerdo.OrigenDiario = new List<string>();
                    cambiado = true;
                }

                if (recuerdo.Importancia < 0)
                {
                    recuerdo.Importancia = 0;
                    cambiado = true;
                }

                if (string.IsNullOrWhiteSpace(recuerdo.Id))
                {
                    string nuevoRecId;
                    do
                    {
                        nuevoRecId = $"rec-{siguienteRecuerdo}";
                        siguienteRecuerdo++;
                    }
                    while (!idsRecuerdosExistentes.Add(nuevoRecId));

                    recuerdo.Id = nuevoRecId;
                    cambiado = true;
                }
            }

            foreach (var compromiso in estado.Compromisos)
            {
                if (compromiso.PlazoVigente is null)
                {
                    compromiso.PlazoVigente = new TiempoJuego();
                    cambiado = true;
                }
            }

            if (estado.Contadores is { } contadores)
            {
                if (contadores.Dia < 1
                    || contadores.OrdinariosLibresGastados < 0
                    || contadores.ExtrasCompromisosGastados < 0
                    || contadores.Interlocuciones < 0)
                {
                    estado.Contadores = new ContadoresPulsos(
                        Math.Max(1, contadores.Dia),
                        Math.Max(0, contadores.OrdinariosLibresGastados),
                        Math.Max(0, contadores.ExtrasCompromisosGastados),
                        contadores.DespertarEmitido,
                        contadores.AcostarseEmitido,
                        Math.Max(0, contadores.Interlocuciones));
                    cambiado = true;
                }
            }

            return cambiado;
        }

        private static void ValidarVersionSoportada(int version, int versionMaxima, string ambito)
        {
            if (version < 0 || version > versionMaxima)
            {
                throw new NotSupportedException(
                    $"Versión de esquema de {ambito} no soportada: v{version} (máxima soportada: v{versionMaxima}).");
            }
        }

        private static bool TryParseNumeroEpisodio(string id, out int numero)
        {
            numero = 0;
            if (!id.StartsWith("ep-", StringComparison.Ordinal))
            {
                return false;
            }

            return int.TryParse(
                id.AsSpan(3),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out numero) && numero > 0;
        }
    }
}
