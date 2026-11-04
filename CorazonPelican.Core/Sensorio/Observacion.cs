using System.Collections.Generic;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>Clase de entidad perceptible (GSS §3.1).</summary>
    public enum CategoriaEntidad
    {
        Granjero,
        Npc,
        Animal,
        Objeto,
        Terreno,
    }

    /// <summary>
    /// Una entidad tal como la ve el sensorio: identidad estable (<c>Ref</c>), dónde está y qué la
    /// caracteriza. Las casillas son internas del motor: nunca llegan a la mente (R-13).
    /// </summary>
    /// <param name="Etiquetas">Rasgos que la entidad activa en la saliencia del personaje (p. ej. <c>agua</c>, <c>madera</c>, <c>persona</c>).</param>
    /// <param name="Corazones">Solo para personas con relación: corazones con el personaje observador.</param>
    public sealed record EntidadObservada(
        string Ref,
        CategoriaEntidad Categoria,
        string Nombre,
        string Ubicacion,
        int X,
        int Y,
        IReadOnlyList<string> Etiquetas,
        int? Corazones = null);

    /// <summary>
    /// Lo que el sensorio observa en un paso (ADS §4, pasos 3–4): la ubicación del personaje y las
    /// entidades que hay en ella. La rellena el adaptador del juego (CP-101) o el FakeWorld de los tests.
    /// </summary>
    /// <param name="Luz">0 (noche cerrada) a 1 (pleno día).</param>
    public sealed record Observacion(
        int Dia,
        int Hora,
        string Ubicacion,
        int X,
        int Y,
        double Luz,
        string Clima,
        IReadOnlyList<EntidadObservada> Entidades);
}
