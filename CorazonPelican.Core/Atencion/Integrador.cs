using System;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>
    /// Acumulador con fuga de saliencias menores (GSS §4.2): <c>dI/dt = Σs − I·ln2/τ</c>, integrado de
    /// forma exacta para que no dependa de cómo se trocee el tiempo. Trabaja en minutos de juego, como
    /// la novedad: «muchas cosas pequeñas» duran lo mismo en vanilla y en Pelican.
    /// </summary>
    public sealed class Integrador
    {
        public Integrador(double semividaMin)
        {
            if (semividaMin <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaMin));
            }

            this.SemividaMin = semividaMin;
        }

        public double SemividaMin { get; }

        public double Valor { get; private set; }

        /// <summary>Acumula <paramref name="suma"/> de saliencias sostenida durante <paramref name="minutosJuego"/>.</summary>
        public void Acumular(double suma, double minutosJuego)
        {
            if (minutosJuego <= 0)
            {
                return;
            }

            var tau = this.SemividaMin / Math.Log(2);
            var retiene = Math.Exp(-minutosJuego / tau);
            this.Valor = (this.Valor * retiene) + (Math.Max(0, suma) * tau * (1 - retiene));
        }

        /// <summary>Tras pensar, lo acumulado ya se ha pensado.</summary>
        public void Reiniciar() => this.Valor = 0;
    }
}
