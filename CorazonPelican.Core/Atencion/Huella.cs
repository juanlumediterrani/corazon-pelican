using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>
    /// Acumulador de huella de atención entre pensamientos (GSS §4.4, P-8, P-10).
    /// Acumula <c>tiempo_en_foco_o_mencion × saliencia</c> con fuga temporal parametrizable, solo
    /// para entidades presentes en la observación en <see cref="Banda.Mencion"/> o <see cref="Banda.Foco"/>.
    /// Se reinicia al emitir cada pulso.
    /// </summary>
    public sealed class HuellaAtencion
    {
        public const double SemividaPorDefectoMin = 30.0;
        private const double UmbralResiduo = 1e-12;

        private readonly Dictionary<string, Acumulado> acumuladas = new(StringComparer.Ordinal);
        private double minutosDesdeUltimoPensamiento;

        public HuellaAtencion(double semividaMin = SemividaPorDefectoMin)
        {
            if (semividaMin <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaMin), "La semivida de fuga debe ser mayor que 0.");
            }

            this.SemividaMin = semividaMin;
        }

        public double SemividaMin { get; }

        /// <summary>Minutos de juego transcurridos desde el último pensamiento (redondeados al entero más próximo).</summary>
        public int MinutosDesdeUltimoPensamiento
            => Math.Max(0, (int)Math.Round(this.minutosDesdeUltimoPensamiento, MidpointRounding.AwayFromZero));

        /// <summary>Entidades atendidas desde el último pulso, ordenadas por atención descendente.</summary>
        public IReadOnlyList<EntidadAtendida> Entidades
            => this.acumuladas
                .Where(kv => kv.Value.Atencion > UmbralResiduo)
                .OrderByDescending(kv => kv.Value.Atencion)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => new EntidadAtendida(kv.Key, kv.Value.Prosa, kv.Value.Atencion))
                .ToList();

        /// <summary>
        /// Integra un paso de observación de <paramref name="minutosJuego"/> minutos de juego.
        /// Solo las entidades presentes en <paramref name="observacion"/> que alcancen <see cref="Banda.Mencion"/>
        /// o <see cref="Banda.Foco"/> acumulan atención; las demás solo sufren fuga.
        /// </summary>
        public void Observar(Observacion observacion, IReadOnlyList<EntidadEnBanda> bandas, double minutosJuego)
        {
            if (minutosJuego < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minutosJuego), "Los minutos de juego no pueden ser negativos.");
            }

            if (minutosJuego == 0)
            {
                return;
            }

            this.minutosDesdeUltimoPensamiento += minutosJuego;

            var tau = this.SemividaMin / Math.Log(2);
            var retiene = Math.Exp(-minutosJuego / tau);

            foreach (var clave in this.acumuladas.Keys.ToList())
            {
                var estado = this.acumuladas[clave];
                estado.Atencion *= retiene;
                if (estado.Atencion <= UmbralResiduo)
                {
                    this.acumuladas.Remove(clave);
                }
            }

            if (observacion.Entidades.Count == 0 || bandas.Count == 0)
            {
                return;
            }

            var observadasPorRef = new Dictionary<string, EntidadObservada>(StringComparer.Ordinal);
            foreach (var entidad in observacion.Entidades)
            {
                observadasPorRef[entidad.Ref] = entidad;
            }

            var factorIntegracion = tau * (1 - retiene);
            foreach (var enBanda in bandas)
            {
                if (enBanda.Banda < Banda.Mencion || enBanda.Saliencia <= 0)
                {
                    continue;
                }

                if (!observadasPorRef.TryGetValue(enBanda.Ref, out var entidad))
                {
                    continue;
                }

                var incremento = enBanda.Saliencia * factorIntegracion;
                if (this.acumuladas.TryGetValue(entidad.Ref, out var existente))
                {
                    existente.Prosa = entidad.Nombre;
                    existente.Atencion += incremento;
                }
                else
                {
                    this.acumuladas[entidad.Ref] = new Acumulado(entidad.Nombre, incremento);
                }
            }
        }

        /// <summary>Construye la huella determinista con las plantillas del perfil sin reiniciar el acumulador.</summary>
        public Contrato.Huella Generar(int dia, PlantillasProsa plantillas)
            => GeneradorAccion.GenerarHuella(this.MinutosDesdeUltimoPensamiento, this.Entidades, dia, plantillas);

        /// <summary>Construye la huella del pulso y reinicia el acumulador y el reloj desde el último pensamiento.</summary>
        public Contrato.Huella EmitirPulso(int dia, PlantillasProsa plantillas)
        {
            var huella = this.Generar(dia, plantillas);
            this.Reiniciar();
            return huella;
        }

        public void Reiniciar()
        {
            this.acumuladas.Clear();
            this.minutosDesdeUltimoPensamiento = 0;
        }

        private sealed class Acumulado
        {
            public Acumulado(string prosa, double atencion)
            {
                this.Prosa = prosa;
                this.Atencion = atencion;
            }

            public string Prosa { get; set; }
            public double Atencion { get; set; }
        }
    }
}
