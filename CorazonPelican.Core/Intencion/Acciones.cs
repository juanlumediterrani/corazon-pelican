using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>Vocabulario de acciones v1 (GSS §5.2). Igual que el enumerado <c>accion</c> de <c>schemas/comun.schema.json</c>.</summary>
    public static class Acciones
    {
        public static readonly IReadOnlyCollection<string> VocabularioV1 = new HashSet<string>
        {
            "ir_a", "sentarse", "mirar", "levantar_la_vista", "dibujar",
            "guardar_cuaderno", "gesto", "murmurar", "esperar", "volver_a_rutina",
        };

        /// <summary>Tipos de gesto válidos en v1 (<c>schemas/comun.schema.json#/$defs/pasoPlan</c>).</summary>
        public static readonly IReadOnlyCollection<string> TiposGestoV1 = new HashSet<string>
        {
            "saludo", "sorpresa", "alegria", "duda", "enfado", "tristeza", "corazon", "pensativa",
        };

        private static readonly Regex RefEntidad = new(@"\A[a-z][a-z_]*(:[^\s]+)?\z", RegexOptions.Compiled);
        private static readonly Regex IdObra = new(@"\A[a-z0-9_]+-[0-9]{4}\z", RegexOptions.Compiled);

        public static bool EsRefValida(string? referencia) => RefEntidad.IsMatch(referencia ?? "");

        public static bool EsIdObraValido(string? idObra) => IdObra.IsMatch(idObra ?? "");
    }
}
