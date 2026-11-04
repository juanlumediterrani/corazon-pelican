using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Tests.Simulacion;

/// <summary>
/// Reloj de los tests: el tiempo real es un contador que solo avanza cuando el test lo pide, y el
/// de juego se deriva de él con la escala del modo (GSS §7). Ningún test espera de verdad.
/// </summary>
public sealed class RelojVirtual : IReloj
{
    private long acumuladoSlot;

    public RelojVirtual(ModoReloj modo, int dia, int hora)
    {
        this.Modo = modo;
        this.Juego = new TiempoJuego { Dia = dia, Hora = hora };
    }

    public ModoReloj Modo { get; }

    public TiempoJuego Juego { get; }

    public long MilisegundosReales { get; private set; }

    /// <summary>Avanza el tiempo real y, con él, el de juego. A las 2:00 empieza el día siguiente a las 6:00.</summary>
    public void Avanzar(long milisegundos)
    {
        this.MilisegundosReales += milisegundos;
        this.acumuladoSlot += milisegundos;
        var porSlot = HoraJuego.MilisegundosPorDiezMinutos(this.Modo);
        while (this.acumuladoSlot >= porSlot)
        {
            this.acumuladoSlot -= porSlot;
            this.Juego.Hora = HoraJuego.SumarDiezMinutos(this.Juego.Hora);
            if (this.Juego.Hora >= HoraJuego.FinDia)
            {
                this.Juego.Dia++;
                this.Juego.Hora = HoraJuego.InicioDia;
            }
        }
    }
}
