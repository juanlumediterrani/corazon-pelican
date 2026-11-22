using System;
using System.Collections.Generic;
using System.Globalization;

namespace CorazonPelican.Core.Tiempo
{
    /// <summary>
    /// Utilidades puras para resolver fechas y ventanas horarias de festivales, incluyendo las
    /// reprogramaciones y ventanas prime-time aplicadas por Pelican 4Ever sobre <c>Data/Festivals/*</c> (CP-609).
    /// </summary>
    public static class CalendarioFestivales
    {
        public static string? ResolverFestivalEnFecha(
            IReadOnlyDictionary<string, string>? festivalDates,
            string? season,
            int dayOfMonth)
        {
            if (festivalDates is null || string.IsNullOrWhiteSpace(season) || dayOfMonth <= 0)
            {
                return null;
            }

            var clave = $"{season.Trim().ToLowerInvariant()}{dayOfMonth.ToString(CultureInfo.InvariantCulture)}";
            if (festivalDates.TryGetValue(clave, out var nombre) && !string.IsNullOrWhiteSpace(nombre))
            {
                return nombre.Trim();
            }

            foreach (var par in festivalDates)
            {
                if (string.Equals(par.Key.Trim(), clave, StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(par.Value))
                {
                    return par.Value.Trim();
                }
            }

            return null;
        }

        public static (int InicioHora, int FinHora)? ParsearVentanaFestival(string? conditionsValue)
        {
            if (string.IsNullOrWhiteSpace(conditionsValue))
            {
                return null;
            }

            var partes = conditionsValue.Split('/');
            if (partes.Length < 2)
            {
                return null;
            }

            var horas = partes[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (horas.Length < 2)
            {
                return null;
            }

            if (!int.TryParse(horas[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var inicio)
                || !int.TryParse(horas[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var fin))
            {
                return null;
            }

            return (inicio, fin);
        }

        public static bool EstaEnVentanaFestival(int horaActual, (int InicioHora, int FinHora)? ventana)
            => ventana is { } v && horaActual >= v.InicioHora && horaActual <= v.FinHora;

        public static IReadOnlyList<string> ConstruirCondicionesDia(
            string? clima,
            bool esDiaFestival,
            bool esBoda = false)
        {
            var condiciones = new List<string>(3);
            if (esDiaFestival)
            {
                condiciones.Add("festival");
            }

            if (string.Equals(clima, "lluvia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(clima, "tormenta", StringComparison.OrdinalIgnoreCase))
            {
                condiciones.Add("lluvia");
            }

            if (esBoda)
            {
                condiciones.Add("boda");
            }

            return condiciones;
        }
    }
}
