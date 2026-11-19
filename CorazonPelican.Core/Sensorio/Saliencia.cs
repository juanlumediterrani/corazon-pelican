using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>Capa de estado (GSS §3.2): cuánto se abre la atención y cuánta energía queda.</summary>
    /// <param name="Apertura">1 paseando (abierta); menor, absorta en algo (estrecha).</param>
    /// <param name="Energia">0 agotada … 1 descansada.</param>
    public sealed record EstadoAtencion(double Apertura, double Energia)
    {
        public static readonly EstadoAtencion Abierta = new(1.0, 1.0);

        /// <summary>Absorta en una tarea, como dibujar.</summary>
        public static readonly EstadoAtencion Estrecha = new(0.6, 1.0);

        public double Factor => Math.Max(0, this.Apertura) * (0.5 + (0.5 * Math.Clamp(this.Energia, 0, 1)));
    }

    /// <summary>
    /// Lo que no cambia entre entidades al calcular la saliencia de una observación: rasgos del
    /// perfil, estado, pesos de la intención activa y el umbral de periferia (tope sin visión).
    /// </summary>
    public sealed record ContextoSaliencia(
        IReadOnlyDictionary<string, double> Rasgos,
        EstadoAtencion Estado,
        double UmbralPeriferia,
        IReadOnlyDictionary<string, double>? PesosIntencion = null)
    {
        /// <summary>Devuelve un contexto con los pesos de atención de <paramref name="intencion"/> si está activa; si no, restaura 1.</summary>
        public ContextoSaliencia ConIntencion(IntencionPersonaje? intencion)
            => this with { PesosIntencion = intencion?.PesosAtencionActivos };

        /// <summary>Construye el contexto de saliencia a partir del perfil y, opcionalmente, la intención activa.</summary>
        public static ContextoSaliencia DesdePerfil(
            PerfilPersonaje perfil,
            EstadoAtencion? estado = null,
            IntencionPersonaje? intencionActiva = null)
        {
            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            return new ContextoSaliencia(
                perfil.Rasgos,
                estado ?? EstadoAtencion.Abierta,
                perfil.Umbrales.Periferia,
                intencionActiva?.PesosAtencionActivos);
        }
    }

    /// <summary>Saliencia de una entidad con cada capa por separado, para explicarla y depurarla.</summary>
    public sealed record DesgloseSaliencia(
        string Ref,
        double Rasgos,
        double Estado,
        double Intencion,
        double Novedad,
        double Relacion,
        double Distancia,
        bool Visible,
        double Total);

    /// <summary>
    /// Saliencia de las entidades observadas (GSS §3.2, ADS §2): producto de cinco capas (rasgos ×
    /// estado × intención × novedad × relación) atenuado por la distancia, llevado a [0, 1) con
    /// <c>s = x / (1 + x)</c>. Sin línea de visión, una entidad no pasa de la periferia. Función pura:
    /// la novedad se lee, no se actualiza.
    /// </summary>
    public static class Saliencia
    {
        /// <summary>Distancia (casillas) a la que una entidad sin relación pierde ~63 % de su peso.</summary>
        public const double AlcanceBaseCasillas = 12;

        /// <summary>Cuánto amplía cada corazón el peso y el alcance de una persona («lo nota llegar de lejos»).</summary>
        public const double PorCorazon = 0.1;

        public static DesgloseSaliencia Calcular(EntidadObservada entidad, int desdeX, int desdeY, ContextoSaliencia contexto, double novedad)
        {
            var rasgos = PesoPorEtiquetas(entidad.Etiquetas, contexto.Rasgos);
            var intencion = contexto.PesosIntencion is null ? 1.0 : PesoPorEtiquetas(entidad.Etiquetas, contexto.PesosIntencion);
            var relacion = 1 + (PorCorazon * Math.Max(0, entidad.Corazones ?? 0));
            var casillas = Math.Sqrt(Math.Pow(entidad.X - desdeX, 2) + Math.Pow(entidad.Y - desdeY, 2));
            var distancia = Math.Exp(-casillas / (AlcanceBaseCasillas * relacion));
            var bruto = rasgos * contexto.Estado.Factor * intencion * Math.Clamp(novedad, 0, 1) * relacion * distancia;

            var total = bruto / (1 + bruto);
            if (!entidad.Visible)
            {
                total = Math.Min(total, contexto.UmbralPeriferia);
            }

            return new DesgloseSaliencia(entidad.Ref, rasgos, contexto.Estado.Factor, intencion, novedad, relacion, distancia, entidad.Visible, total);
        }

        /// <summary>Saliencia de todas las entidades de una observación, desde la casilla del personaje.</summary>
        public static IReadOnlyList<DesgloseSaliencia> Calcular(Observacion observacion, ContextoSaliencia contexto, Novedad novedad)
            => observacion.Entidades
                .Select(e => Calcular(e, observacion.X, observacion.Y, contexto, novedad.Factor(e.Ref)))
                .ToList();

        /// <summary>El mayor peso entre las etiquetas de la entidad; una etiqueta sin peso cuenta como 1.</summary>
        private static double PesoPorEtiquetas(IReadOnlyList<string> etiquetas, IReadOnlyDictionary<string, double> pesos)
            => etiquetas.Count == 0 ? 1.0 : etiquetas.Max(e => pesos.TryGetValue(e, out var peso) ? peso : 1.0);
    }
}
