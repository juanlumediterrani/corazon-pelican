using System;
using System.Globalization;
using System.Linq;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>
    /// Construye identificadores estables (<c>Ref</c>) para las entidades y sucesos del sensorio
    /// (GSS §3.1, ADS §3.3). Los mismos formatos sirven en juego real y en simulaciones.
    /// </summary>
    public static class IdentidadesObservacion
    {
        public static string Granjero(long id)
            => $"farmer:{id.ToString(CultureInfo.InvariantCulture)}";

        public static string Granjero(string id)
            => $"farmer:{Limpiar(id)}";

        public static string Npc(string nombre)
            => $"npc:{Limpiar(nombre)}";

        public static string Animal(long id)
            => $"animal:{id.ToString(CultureInfo.InvariantCulture)}";

        public static string Animal(string id)
            => $"animal:{Limpiar(id)}";

        public static string Mascota(Guid id)
            => $"mascota:{id:D}";

        public static string Mascota(string id)
            => $"mascota:{Limpiar(id)}";

        public static string Objeto(string ubicacion, int x, int y, string id)
            => $"obj:{Limpiar(ubicacion)}:{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}:{Limpiar(id)}";

        public static string Mueble(string ubicacion, int x, int y, string id)
            => $"mueble:{Limpiar(ubicacion)}:{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}:{Limpiar(id)}";

        public static string Agua(string ubicacion, int x, int y)
            => $"agua:{Limpiar(ubicacion)}:{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}";

        public static string AguaZona(string ubicacion, string zona = "orilla")
            => $"agua:{Limpiar(ubicacion)}:{Limpiar(zona)}";

        public static string Terreno(string ubicacion, int x, int y, string tipo)
            => $"terreno:{Limpiar(ubicacion)}:{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)}:{Limpiar(tipo)}";

        public static string TerrenoZona(string ubicacion, string tipo)
            => $"terreno:{Limpiar(ubicacion)}:{Limpiar(tipo)}";

        public static string Suceso(string tipo, params string[] partes)
        {
            if (partes is null || partes.Length == 0)
            {
                return $"suceso:{Limpiar(tipo)}";
            }

            return $"suceso:{Limpiar(tipo)}:{string.Join(":", partes.Select(Limpiar))}";
        }

        private static string Limpiar(string? segmento)
        {
            if (string.IsNullOrWhiteSpace(segmento))
            {
                return "desconocido";
            }

            return segmento.Trim().Replace(':', '_');
        }
    }
}
