using System;
using System.Collections.Generic;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>
    /// Habituación por entidad (GSS §3.2): lo que se mira mucho pierde novedad y la recupera con la
    /// ausencia. Se mide en minutos de juego, para que habituarse dure lo mismo en vanilla y en Pelican.
    /// </summary>
    public sealed class Novedad
    {
        private readonly Dictionary<string, double> habituacion = new();

        /// <param name="minutosHabituacion">Constante de tiempo de la habituación mientras se percibe.</param>
        /// <param name="semividaRecuperacionMin">Minutos de ausencia para recuperar la mitad de la novedad perdida.</param>
        /// <param name="piso">Novedad mínima: ni lo más visto deja de existir del todo.</param>
        public Novedad(double minutosHabituacion = 15, double semividaRecuperacionMin = 720, double piso = 0.3)
        {
            if (minutosHabituacion <= 0 || semividaRecuperacionMin <= 0 || piso is < 0 or > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(piso), "Parámetros de novedad fuera de rango.");
            }

            this.MinutosHabituacion = minutosHabituacion;
            this.SemividaRecuperacionMin = semividaRecuperacionMin;
            this.Piso = piso;
        }

        public double MinutosHabituacion { get; }

        public double SemividaRecuperacionMin { get; }

        public double Piso { get; }

        /// <summary>Factor de novedad en [piso, 1]: 1 para lo nunca visto.</summary>
        public double Factor(string referencia)
            => 1 - (this.habituacion.GetValueOrDefault(referencia) * (1 - this.Piso));

        /// <summary>
        /// Registra <paramref name="minutosJuego"/> de exposición a <paramref name="percibidas"/>; las demás
        /// entidades conocidas recuperan novedad durante ese tiempo.
        /// </summary>
        public void Exponer(IEnumerable<string> percibidas, double minutosJuego)
        {
            if (minutosJuego <= 0)
            {
                return;
            }

            var presentes = new HashSet<string>(percibidas);
            var recuperacion = Math.Pow(0.5, minutosJuego / this.SemividaRecuperacionMin);
            foreach (var referencia in new List<string>(this.habituacion.Keys))
            {
                if (!presentes.Contains(referencia))
                {
                    this.habituacion[referencia] *= recuperacion;
                }
            }

            var ganancia = 1 - Math.Exp(-minutosJuego / this.MinutosHabituacion);
            foreach (var referencia in presentes)
            {
                var h = this.habituacion.GetValueOrDefault(referencia);
                this.habituacion[referencia] = h + ((1 - h) * ganancia);
            }
        }
    }
}
