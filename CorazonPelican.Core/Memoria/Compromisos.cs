using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Estados operativos de un compromiso guardado (GSS §9.3, CP-502).</summary>
    public static class EstadosCompromiso
    {
        public const string Pendiente = "pendiente";
        public const string Cumplido = "cumplido";
        public const string Incumplido = "incumplido";
    }

    /// <summary>Estados de una petición de prórroga por falta de cupo (P-12, CP-502, CP-509).</summary>
    public static class EstadosProrroga
    {
        public const string Pendiente = "pendiente";
        public const string Aceptada = "aceptada";
        public const string Rechazada = "rechazada";
        public const string Vencida = "vencida";
    }

    /// <summary>Resultado de evaluar los compromisos activos en un paso del bucle.</summary>
    public sealed record ResultadoEvaluacionCompromisos(
        IReadOnlyList<CompromisoEstado> PulsosSolicitados,
        IReadOnlyList<CompromisoEstado> Vencidos);

    /// <summary>
    /// Curva de saliencia por plazo, expiración a memoria e inicio de prórrogas (R-09, GSS §9.3, CP-502).
    /// </summary>
    public static class GestorCompromisos
    {
        public const double VentanaSalienciaHorasPorDefecto = 24.0;
        public const double UmbralDisparoPorDefecto = 0.60;
        public const int DiasProrrogaPorDefecto = 2;

        public static double CalcularMinutosRestantes(CompromisoEstado compromiso, TiempoJuego ahora)
        {
            if (compromiso is null)
            {
                throw new ArgumentNullException(nameof(compromiso));
            }

            if (ahora is null)
            {
                throw new ArgumentNullException(nameof(ahora));
            }

            return ConvertirAMinutosAbsolutos(compromiso.PlazoVigente) - ConvertirAMinutosAbsolutos(ahora);
        }

        public static bool EstaVencido(CompromisoEstado compromiso, TiempoJuego ahora)
        {
            if (compromiso is null || ahora is null)
            {
                return false;
            }

            return string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal)
                && CalcularMinutosRestantes(compromiso, ahora) <= 0.0;
        }

        /// <summary>
        /// Saliencia monótona en <c>[0, 1]</c> según cuánto queda hasta el plazo vigente.
        /// Crece suavemente dentro de la ventana y alcanza <c>1.0</c> al vencimiento.
        /// </summary>
        public static double CalcularSaliencia(
            CompromisoEstado compromiso,
            TiempoJuego ahora,
            double ventanaHoras = VentanaSalienciaHorasPorDefecto)
        {
            if (compromiso is null)
            {
                throw new ArgumentNullException(nameof(compromiso));
            }

            if (ahora is null)
            {
                throw new ArgumentNullException(nameof(ahora));
            }

            if (ventanaHoras <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ventanaHoras), "La ventana de saliencia debe ser mayor que 0.");
            }

            if (!string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal))
            {
                return 0.0;
            }

            var horasRestantes = CalcularMinutosRestantes(compromiso, ahora) / 60.0;
            if (horasRestantes <= 0.0)
            {
                return 1.0;
            }

            if (horasRestantes >= ventanaHoras)
            {
                return 0.0;
            }

            var progreso = Math.Clamp(1.0 - (horasRestantes / ventanaHoras), 0.0, 1.0);
            return Math.Clamp(0.2 + (0.8 * Math.Pow(progreso, 1.5)), 0.0, 1.0);
        }

        /// <summary>
        /// Evalúa compromisos pendientes: marca vencidos como incumplidos (pasando a memoria) y solicita
        /// pulsión en <paramref name="disparadores"/> para aquellos cuya saliencia alcanza <paramref name="umbralDisparo"/>.
        /// </summary>
        public static ResultadoEvaluacionCompromisos Evaluar(
            EstadoPersonaje estado,
            Observacion observacion,
            Disparadores disparadores,
            long ahoraMs,
            ISet<string>? compromisosYaAvisados = null,
            double umbralDisparo = UmbralDisparoPorDefecto,
            double ventanaHoras = VentanaSalienciaHorasPorDefecto)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            if (disparadores is null)
            {
                throw new ArgumentNullException(nameof(disparadores));
            }

            var ahora = ClonarTiempo(observacion.Dia, observacion.Hora);
            var solicitados = new List<CompromisoEstado>();
            var vencidos = new List<CompromisoEstado>();

            foreach (var compromiso in estado.Compromisos)
            {
                if (!string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal))
                {
                    continue;
                }

                if (EstaVencido(compromiso, ahora))
                {
                    MarcarIncumplido(estado, compromiso, ahora);
                    vencidos.Add(compromiso);
                    continue;
                }

                if (compromiso.Prorroga is { Estado: EstadosProrroga.Pendiente })
                {
                    // Ya existe una petición de prórroga activa para este plazo: conservar una sola y el plazo vigente.
                    continue;
                }

                var saliencia = CalcularSaliencia(compromiso, ahora, ventanaHoras);
                if (saliencia < umbralDisparo)
                {
                    continue;
                }

                var claveAviso = ClaveAviso(compromiso);
                if (compromisosYaAvisados is not null && !compromisosYaAvisados.Add(claveAviso))
                {
                    continue;
                }

                disparadores.Compromiso(compromiso.Id, observacion, ahoraMs);
                solicitados.Add(compromiso);
            }

            return new ResultadoEvaluacionCompromisos(solicitados, vencidos);
        }

        /// <summary>
        /// Registra de forma idempotente una petición de prórroga cuando se agotaron plazas ordinarias y extra,
        /// conservando intacto <see cref="CompromisoEstado.PlazoVigente"/> (P-12, CP-502).
        /// </summary>
        public static PeticionProrrogaEstado? RegistrarPeticionProrroga(
            EstadoPersonaje estado,
            string compromisoId,
            TiempoJuego ahora,
            int diasProrroga = DiasProrrogaPorDefecto)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (string.IsNullOrWhiteSpace(compromisoId) || ahora is null)
            {
                return null;
            }

            var compromiso = estado.Compromisos.FirstOrDefault(
                c => string.Equals(c.Id, compromisoId, StringComparison.Ordinal));
            if (compromiso is null || !string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal))
            {
                return null;
            }

            if (compromiso.Prorroga is { Estado: EstadosProrroga.Pendiente } existente)
            {
                return existente;
            }

            var plazoOriginal = ClonarTiempo(compromiso.PlazoVigente);
            var plazoPropuesto = new TiempoJuego
            {
                Dia = plazoOriginal.Dia + Math.Max(1, diasProrroga),
                Hora = plazoOriginal.Hora,
            };

            var peticion = new PeticionProrrogaEstado
            {
                Id = $"pror-{compromiso.Id}",
                CompromisoId = compromiso.Id,
                Destinatario = compromiso.Destinatario,
                Objeto = compromiso.Objeto,
                PlazoOriginal = plazoOriginal,
                PlazoPropuesto = plazoPropuesto,
                Estado = EstadosProrroga.Pendiente,
                CreadaEn = ClonarTiempo(ahora),
            };

            compromiso.Prorroga = peticion;
            compromiso.PlazoPropuesto = ClonarTiempo(plazoPropuesto);
            return peticion;
        }

        /// <summary>
        /// Marca un compromiso vencido sin cumplir como <c>incumplido</c> y lo pasa al diario y a la memoria
        /// para que el personaje lo recuerde (y pueda disculparse) cuando vuelva a ver al destinatario.
        /// </summary>
        public static bool MarcarIncumplido(EstadoPersonaje estado, CompromisoEstado compromiso, TiempoJuego ahora)
        {
            if (estado is null || compromiso is null || ahora is null)
            {
                return false;
            }

            if (!string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal))
            {
                return false;
            }

            compromiso.Estado = EstadosCompromiso.Incumplido;
            if (compromiso.Prorroga is { Estado: EstadosProrroga.Pendiente } prorroga)
            {
                prorroga.Estado = EstadosProrroga.Vencida;
            }

            var dia = Math.Max(1, ahora.Dia);
            var hora = Math.Clamp(ahora.Hora, HoraJuego.InicioDia, HoraJuego.FinDia);
            var resumenEp = $"Venció el plazo de tu compromiso con {compromiso.Destinatario} ({compromiso.Objeto}) sin poder cumplirlo.";
            var entidades = string.IsNullOrWhiteSpace(compromiso.Destinatario)
                ? null
                : new[] { compromiso.Destinatario };
            var episodio = estado.AgregarEpisodio(dia, hora, resumenEp, 0.85, entidades);
            estado.AgregarRecuerdo(
                $"Incumplí mi promesa ({compromiso.Objeto}) con {compromiso.Destinatario} y debería disculparme cuando le vea.",
                0.85,
                new[] { episodio.Id });
            return true;
        }

        /// <summary>
        /// Proyecta los compromisos pendientes y los incumplidos cuando la persona destinataria está en escena
        /// o es el interlocutor del pulso (R-09, CP-502).
        /// </summary>
        public static List<CompromisoProximo> ProyectarParaPulso(
            EstadoPersonaje estado,
            IEnumerable<string>? presentes = null,
            string? interlocutorRef = null,
            int diaFallback = 1)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            var receptoresPresentes = new HashSet<string>(StringComparer.Ordinal);
            if (presentes is not null)
            {
                foreach (var ent in presentes.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    receptoresPresentes.Add(ent);
                }
            }

            if (!string.IsNullOrWhiteSpace(interlocutorRef))
            {
                receptoresPresentes.Add(interlocutorRef);
            }

            var salida = new List<CompromisoProximo>();
            foreach (var c in estado.Compromisos)
            {
                if (string.Equals(c.Estado, EstadosCompromiso.Cumplido, StringComparison.Ordinal))
                {
                    continue;
                }

                var esIncumplido = string.Equals(c.Estado, EstadosCompromiso.Incumplido, StringComparison.Ordinal);
                if (esIncumplido && !receptoresPresentes.Contains(c.Destinatario))
                {
                    continue;
                }

                var textoBase = string.IsNullOrWhiteSpace(c.Tipo) ? c.Objeto : $"{c.Tipo}: {c.Objeto}";
                var prosa = esIncumplido ? $"{textoBase} [incumplido]" : textoBase;
                salida.Add(new CompromisoProximo
                {
                    Id = c.Id,
                    Con = c.Destinatario,
                    Prosa = prosa,
                    PlazoJuego = new TiempoJuego
                    {
                        Dia = Math.Max(1, c.PlazoVigente?.Dia ?? diaFallback),
                        Hora = Math.Clamp(c.PlazoVigente?.Hora ?? HoraJuego.InicioDia, HoraJuego.InicioDia, HoraJuego.FinDia),
                    },
                });
            }

            return salida;
        }

        private static string ClaveAviso(CompromisoEstado c)
            => $"{c.Id}@{c.PlazoVigente?.Dia ?? 0}:{c.PlazoVigente?.Hora ?? 0}";

        private static TiempoJuego ClonarTiempo(TiempoJuego? origen)
            => ClonarTiempo(origen?.Dia ?? 1, origen?.Hora ?? HoraJuego.InicioDia);

        private static TiempoJuego ClonarTiempo(int dia, int hora)
            => new()
            {
                Dia = Math.Max(1, dia),
                Hora = Math.Clamp(hora, HoraJuego.InicioDia, HoraJuego.FinDia),
            };

        private static double ConvertirAMinutosAbsolutos(TiempoJuego? t)
        {
            if (t is null)
            {
                return 0.0;
            }

            var dia = Math.Max(1, t.Dia);
            var hora = Math.Clamp(t.Hora, HoraJuego.InicioDia, HoraJuego.FinDia);
            return ((dia - 1) * 1440.0) + ((hora / 100) * 60.0) + (hora % 100);
        }
    }
}
