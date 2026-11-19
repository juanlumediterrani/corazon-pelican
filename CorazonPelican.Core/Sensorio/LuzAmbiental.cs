using System;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>
    /// Cálculo determinista de luz ambiental en [0, 1] a partir de hora del juego, clima y entorno interior/exterior.
    /// </summary>
    public static class LuzAmbiental
    {
        public const double LuzNoche = 0.2;
        public const double LuzInterior = 0.85;

        public static double Calcular(
            int hora,
            string clima,
            bool esExterior = true,
            int horaOcaso = 1800,
            int horaNoche = 2000)
        {
            if (!esExterior)
            {
                return LuzInterior;
            }

            var minutosActuales = HoraJuego.MinutosDesdeInicio(hora);
            var minutosOcaso = HoraJuego.MinutosDesdeInicio(horaOcaso);
            var minutosNoche = Math.Max(minutosOcaso + 1, HoraJuego.MinutosDesdeInicio(horaNoche));

            double baseLuz;
            if (minutosActuales <= minutosOcaso)
            {
                baseLuz = 1.0;
            }
            else if (minutosActuales >= minutosNoche)
            {
                baseLuz = LuzNoche;
            }
            else
            {
                var progreso = (double)(minutosActuales - minutosOcaso) / (minutosNoche - minutosOcaso);
                baseLuz = 1.0 - ((1.0 - LuzNoche) * progreso);
            }

            var factorClima = clima switch
            {
                "tormenta" => 0.75,
                "lluvia" or "lluvia_verde" => 0.85,
                "nieve" => 0.90,
                _ => 1.0,
            };

            return Math.Clamp(baseLuz * factorClima, 0.0, 1.0);
        }
    }
}
