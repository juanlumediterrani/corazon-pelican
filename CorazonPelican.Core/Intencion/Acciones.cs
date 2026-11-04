using System.Collections.Generic;

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
    }
}
