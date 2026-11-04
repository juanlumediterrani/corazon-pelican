using System.Collections.Generic;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>
    /// Fuentes de hechos que el motor sabe reunir para acompañar al pulso de un hito (ADS §3.1,
    /// <c>contexto</c> de las anotaciones de rutina). Un perfil solo puede pedir estas; una fuente
    /// nueva (p. ej. las ventas de una tienda) exige ampliar el sensorio y esta lista.
    /// </summary>
    public static class FuentesContexto
    {
        public static readonly IReadOnlyCollection<string> Conocidas = new HashSet<string>
        {
            "luz", "clima", "energia", "obra_en_curso", "personas_cerca", "compromisos",
        };
    }
}
