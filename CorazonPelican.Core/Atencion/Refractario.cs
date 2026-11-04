using System;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>
    /// Período refractario (GSS §4.2), en tiempo real: tras cada pulsión, durante una semivida no
    /// pasan disparadores de estímulo (pico, deriva, transición) y después sus umbrales siguen
    /// elevados por <c>1 + subida · 0,5^(t / semivida)</c> hasta volver a la base.
    /// </summary>
    public sealed class Refractario
    {
        private readonly double subida;
        private readonly long semividaMs;
        private long? ultimoMs;

        public Refractario(Perfil.Refractario parametros)
        {
            this.subida = parametros.Subida;
            this.semividaMs = (long)(parametros.SemividaS * 1000);
        }

        public long VentanaMs => this.semividaMs;

        public void Registrar(long ahoraMs) => this.ultimoMs = ahoraMs;

        /// <summary>Si estamos dentro de la ventana que sigue a la última pulsión.</summary>
        public bool EnVentana(long ahoraMs) => this.ultimoMs is long ultimo && ahoraMs - ultimo < this.semividaMs;

        /// <summary>Multiplicador de los umbrales de estímulo; 1 si nunca hubo pulsión.</summary>
        public double Factor(long ahoraMs)
            => this.ultimoMs is long ultimo
                ? 1 + (this.subida * Math.Pow(0.5, (double)(ahoraMs - ultimo) / this.semividaMs))
                : 1;
    }
}
