using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Resultado de una pasada de envejecimiento y poda del diario.</summary>
    public sealed record ResultadoConsolidacion(int EpisodiosEliminados, int EpisodiosRestantes, int TamanoBytes);

    /// <summary>
    /// Envejecimiento temporal y poda del diario con tope <c>TamanoMaxMemoriaKB</c> (GSS §9.2, ADS §9, §12.1),
    /// además de la proyección de episodios del día hacia <see cref="Pulso.EpisodiosDiario"/> (CP-211).
    /// </summary>
    public static class Consolidacion
    {
        public const double SemividaEnvejecimientoPorDefectoDias = 14.0;

        /// <summary>
        /// Saliencia efectiva de un episodio tras envejecer desde <see cref="EpisodioDiario.DiaJuego"/>
        /// hasta <paramref name="diaActual"/>. Función pura e idempotente.
        /// </summary>
        public static double SalienciaEnvejecida(
            EpisodioDiario episodio,
            int diaActual,
            double semividaDias = SemividaEnvejecimientoPorDefectoDias)
        {
            if (semividaDias <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaDias), "La semivida de envejecimiento debe ser mayor que 0.");
            }

            var diasTranscurridos = Math.Max(0, diaActual - episodio.DiaJuego);
            return Math.Max(0, episodio.Saliencia) * Math.Pow(0.5, diasTranscurridos / semividaDias);
        }

        /// <summary>Tamaño actual del estado serializado en UTF-8.</summary>
        public static int MedirTamanoBytes(EstadoPersonaje estado)
            => JsonSerializer.SerializeToUtf8Bytes(estado).Length;

        /// <summary>
        /// Envejece y poda el diario del personaje para no superar <paramref name="tamanoMaxMemoriaKB"/> KB.
        /// Conserva prioritariamente los episodios del día actual y los de mayor saliencia envejecida.
        /// </summary>
        public static ResultadoConsolidacion Consolidar(
            EstadoPersonaje estado,
            int diaActual,
            int tamanoMaxMemoriaKB,
            double semividaDias = SemividaEnvejecimientoPorDefectoDias,
            double? salienciaMinimaEnvejecida = null)
        {
            if (tamanoMaxMemoriaKB <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tamanoMaxMemoriaKB), "El tope de memoria debe ser mayor que 0 KB.");
            }

            if (semividaDias <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaDias), "La semivida debe ser mayor que 0.");
            }

            var iniciales = estado.Diario.Count;
            if (salienciaMinimaEnvejecida is double umbralMinimo)
            {
                estado.Diario.RemoveAll(e =>
                    e.DiaJuego < diaActual && SalienciaEnvejecida(e, diaActual, semividaDias) < umbralMinimo);
            }

            var maxBytes = tamanoMaxMemoriaKB * 1024;
            var tamanoBytes = MedirTamanoBytes(estado);

            if (tamanoBytes > maxBytes && estado.Diario.Count > 0)
            {
                var ordenPoda = estado.Diario
                    .Select((ep, idx) => new
                    {
                        Indice = idx,
                        EsDeHoy = ep.DiaJuego >= diaActual ? 1 : 0,
                        Valor = SalienciaEnvejecida(ep, diaActual, semividaDias),
                        ep.DiaJuego,
                        ep.Hora,
                    })
                    .OrderBy(x => x.EsDeHoy)
                    .ThenBy(x => x.Valor)
                    .ThenBy(x => x.DiaJuego)
                    .ThenBy(x => x.Hora)
                    .ThenBy(x => x.Indice)
                    .Select(x => x.Indice)
                    .ToList();

                var bajo = 1;
                var alto = ordenPoda.Count;
                var mejorK = ordenPoda.Count;

                while (bajo <= alto)
                {
                    var medio = bajo + ((alto - bajo) / 2);
                    var candidato = FiltrarSinIndices(estado, ordenPoda, medio);
                    if (MedirTamanoBytes(candidato) <= maxBytes)
                    {
                        mejorK = medio;
                        alto = medio - 1;
                    }
                    else
                    {
                        bajo = medio + 1;
                    }
                }

                var podados = new HashSet<int>(ordenPoda.Take(mejorK));
                estado.Diario = estado.Diario.Where((_, idx) => !podados.Contains(idx)).ToList();
                tamanoBytes = MedirTamanoBytes(estado);
            }

            return new ResultadoConsolidacion(
                iniciales - estado.Diario.Count,
                estado.Diario.Count,
                tamanoBytes);
        }

        /// <summary>Vuelca las entradas actuales del búfer al diario asignando ids estables por personaje.</summary>
        public static IReadOnlyList<EpisodioDiario> RegistrarDesdeBufer(EstadoPersonaje estado, Bufer bufer)
        {
            var creados = new List<EpisodioDiario>();
            foreach (var entrada in bufer.Entradas)
            {
                if (string.IsNullOrWhiteSpace(entrada.Prosa))
                {
                    continue;
                }

                creados.Add(estado.AgregarEpisodio(
                    entrada.Dia,
                    entrada.Hora,
                    entrada.Prosa,
                    entrada.Saliencia,
                    entrada.Entidades));
            }

            return creados;
        }

        /// <summary>
        /// Proyecta los episodios del día <paramref name="diaJuego"/> al mapa <c>id → prosa</c>
        /// requerido por <see cref="Pulso.EpisodiosDiario"/> (<c>schemas/pulso.schema.json</c>).
        /// </summary>
        public static Dictionary<string, string> ProyectarEpisodiosDelDia(EstadoPersonaje estado, int diaJuego)
        {
            var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var episodio in estado.Diario
                .Where(e => e.DiaJuego == diaJuego
                    && !string.IsNullOrWhiteSpace(e.Id)
                    && !string.IsNullOrWhiteSpace(e.ResumenProsa))
                .OrderBy(e => e.Hora))
            {
                mapa[episodio.Id] = episodio.ResumenProsa;
            }

            return mapa;
        }

        /// <summary>
        /// Rellena <see cref="Pulso.EpisodiosDiario"/> con los episodios del día y despeja
        /// <see cref="Pulso.DiarioDia"/> para respetar la exclusión mutua de <c>schemas/pulso.schema.json</c>.
        /// </summary>
        public static void AplicarEpisodiosAlPulso(Pulso pulso, EstadoPersonaje estado, int diaJuego)
        {
            var mapa = ProyectarEpisodiosDelDia(estado, diaJuego);
            pulso.EpisodiosDiario = mapa.Count > 0 ? mapa : null;
            pulso.DiarioDia = null;
        }

        private static EstadoPersonaje FiltrarSinIndices(EstadoPersonaje origen, List<int> ordenPoda, int cantidadAPodar)
        {
            var excluidos = new HashSet<int>(ordenPoda.Take(cantidadAPodar));
            var diarioFiltrado = new List<EpisodioDiario>(Math.Max(0, origen.Diario.Count - excluidos.Count));
            for (var i = 0; i < origen.Diario.Count; i++)
            {
                if (!excluidos.Contains(i))
                {
                    diarioFiltrado.Add(origen.Diario[i]);
                }
            }

            return new EstadoPersonaje
            {
                VersionEsquema = origen.VersionEsquema,
                SiguienteIdEpisodio = origen.SiguienteIdEpisodio,
                Diario = diarioFiltrado,
                Recuerdos = origen.Recuerdos,
                Relaciones = origen.Relaciones,
                Portafolio = origen.Portafolio,
                Compromisos = origen.Compromisos,
                Contadores = origen.Contadores,
                IntencionActiva = origen.IntencionActiva,
            };
        }
    }
}
