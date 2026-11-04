using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Tiempo
{
    /// <summary>Modo de reloj del mundo (GSS §7). Elige el presupuesto del perfil y la escala de tiempo.</summary>
    public enum ModoReloj
    {
        /// <summary>10 minutos de juego ≈ 7 s reales.</summary>
        Vanilla,

        /// <summary>Pelican 4Ever: 10 minutos de juego = 10 minutos reales.</summary>
        Pelican,
    }

    /// <summary>
    /// Las dos horas que usa el motor (GSS §7): plazos en tiempo de juego; refractario, latencia y
    /// caducidades en tiempo real. En el juego lo implementa el adaptador; en los tests, un reloj virtual.
    /// </summary>
    public interface IReloj
    {
        ModoReloj Modo { get; }

        /// <summary>Día y hora de juego actuales.</summary>
        TiempoJuego Juego { get; }

        /// <summary>Milisegundos reales transcurridos desde un origen arbitrario; solo sirven para medir intervalos.</summary>
        long MilisegundosReales { get; }
    }

    /// <summary>Aritmética de la hora del juego (600 = 6:00 … 2600 = 2:00, en pasos de 10 minutos).</summary>
    public static class HoraJuego
    {
        public const int InicioDia = 600;

        public const int FinDia = 2600;

        /// <summary>Milisegundos reales por cada 10 minutos de juego en cada modo.</summary>
        public static long MilisegundosPorDiezMinutos(ModoReloj modo) => modo == ModoReloj.Vanilla ? 7_000 : 600_000;

        public static int SumarDiezMinutos(int hora) => hora % 100 == 50 ? hora + 50 : hora + 10;

        /// <summary>Minutos de juego transcurridos desde las 6:00.</summary>
        public static int MinutosDesdeInicio(int hora) => ((hora / 100) * 60) + (hora % 100) - 360;
    }
}
