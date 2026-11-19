using System;
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
        Suceso,
    }

    /// <summary>Suceso local percibido durante una observación (p. ej. regalo recibido, GSS §3.1).</summary>
    public sealed record SucesoObservado(
        string Ref,
        string Tipo,
        string Descripcion,
        string? ActorRef = null,
        string? ObjetoId = null);

    /// <summary>
    /// Una entidad tal como la ve el sensorio: identidad estable (<c>Ref</c>), dónde está y qué la
    /// caracteriza. Las casillas son internas del motor: nunca llegan a la mente (R-13).
    /// </summary>
    /// <param name="Etiquetas">Rasgos que la entidad activa en la saliencia del personaje (p. ej. <c>agua</c>, <c>madera</c>, <c>persona</c>).</param>
    /// <param name="Corazones">Solo para personas con relación: corazones con el personaje observador.</param>
    /// <param name="Visible">Si hay línea de visión desde el personaje (sin ella, la entidad no pasa de la periferia).</param>
    /// <param name="Distancia">Distancia euclídea en casillas desde el observador (opcional; si falta se calcula con las coordenadas).</param>
    public sealed record EntidadObservada(
        string Ref,
        CategoriaEntidad Categoria,
        string Nombre,
        string Ubicacion,
        int X,
        int Y,
        IReadOnlyList<string> Etiquetas,
        int? Corazones = null,
        bool Visible = true,
        double? Distancia = null)
    {
        public double DistanciaDesde(int desdeX, int desdeY)
            => this.Distancia ?? CalcularDistancia(desdeX, desdeY, this.X, this.Y);

        public static double CalcularDistancia(int desdeX, int desdeY, int hastaX, int hastaY)
        {
            var dx = hastaX - desdeX;
            var dy = hastaY - desdeY;
            return Math.Sqrt((dx * dx) + (dy * dy));
        }
    }

    /// <summary>
    /// Lo que el sensorio observa en un paso (ADS §4, pasos 3–4): la ubicación del personaje y las
    /// entidades que hay en ella. La rellena el adaptador del juego (CP-101) o el FakeWorld de los tests.
    /// </summary>
    /// <param name="Luz">0 (noche cerrada) a 1 (pleno día).</param>
    /// <param name="Sucesos">Sucesos locales observados en este paso (p. ej. regalo recibido).</param>
    public sealed record Observacion(
        int Dia,
        int Hora,
        string Ubicacion,
        int X,
        int Y,
        double Luz,
        string Clima,
        IReadOnlyList<EntidadObservada> Entidades,
        IReadOnlyList<SucesoObservado>? Sucesos = null);
}
