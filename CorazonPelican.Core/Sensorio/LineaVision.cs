using System;
using System.Collections.Generic;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>
    /// Cálculo puro de línea de visión por casillas usando Bresenham (CP-101).
    /// Una entidad es visible si ninguna casilla estrictamente intermedia entre el observador y el
    /// objetivo es opaca (muro, edificio u objeto opaco).
    /// </summary>
    public static class LineaVision
    {
        /// <summary>Comprueba línea de visión desde (<paramref name="origenX"/>, <paramref name="origenY"/>) hasta (<paramref name="destinoX"/>, <paramref name="destinoY"/>).</summary>
        public static bool HayLineaVision(
            int origenX,
            int origenY,
            int destinoX,
            int destinoY,
            Func<int, int, bool> esOpaco)
        {
            if (esOpaco is null)
            {
                throw new ArgumentNullException(nameof(esOpaco));
            }

            if (origenX == destinoX && origenY == destinoY)
            {
                return true;
            }

            var x = origenX;
            var y = origenY;
            var dx = Math.Abs(destinoX - origenX);
            var dy = Math.Abs(destinoY - origenY);
            var sx = origenX < destinoX ? 1 : -1;
            var sy = origenY < destinoY ? 1 : -1;
            var err = dx - dy;

            while (x != destinoX || y != destinoY)
            {
                if ((x != origenX || y != origenY) && esOpaco(x, y))
                {
                    return false;
                }

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            return true;
        }

        /// <summary>Sobrecarga sobre una rejilla booleana donde <c>true</c> indica casilla opaca (fuera de límites = opaco).</summary>
        public static bool HayLineaVision(
            int origenX,
            int origenY,
            int destinoX,
            int destinoY,
            bool[,] mapaOpaco)
        {
            if (mapaOpaco is null)
            {
                throw new ArgumentNullException(nameof(mapaOpaco));
            }

            var ancho = mapaOpaco.GetLength(0);
            var alto = mapaOpaco.GetLength(1);
            return HayLineaVision(
                origenX,
                origenY,
                destinoX,
                destinoY,
                (x, y) => x < 0 || y < 0 || x >= ancho || y >= alto || mapaOpaco[x, y]);
        }

        /// <summary>Devuelve todas las casillas de la línea de Bresenham desde origen hasta destino (ambos inclusive).</summary>
        public static IReadOnlyList<(int X, int Y)> TrazarLinea(int origenX, int origenY, int destinoX, int destinoY)
        {
            var puntos = new List<(int X, int Y)>();
            var x = origenX;
            var y = origenY;
            var dx = Math.Abs(destinoX - origenX);
            var dy = Math.Abs(destinoY - origenY);
            var sx = origenX < destinoX ? 1 : -1;
            var sy = origenY < destinoY ? 1 : -1;
            var err = dx - dy;

            while (true)
            {
                puntos.Add((x, y));
                if (x == destinoX && y == destinoY)
                {
                    break;
                }

                var e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y += sy;
                }
            }

            return puntos;
        }
    }
}
