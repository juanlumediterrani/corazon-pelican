namespace CorazonPelican.Core.Tiempo
{
    /// <summary>
    /// Guarda pura de sincronización de arranque y retención nocturna para el modo Pelican 1:1 (CP-608):
    /// evita emitir <c>despertar</c> o pasos de sensorio antes de que el reloj horario externo
    /// sincronice desde las 06:00 tras <c>SaveLoaded</c>/<c>DayStarted</c> o durante <c>NightHold</c>/<c>Rollover</c>.
    /// </summary>
    public sealed class GuardaCicloPelican
    {
        public const long EsperaSincronizacionRelojMs = 2_000;

        private long? inicioEsperaSincronizacionMs;

        public void MarcarInicioJornadaOCarga(long ahoraMs)
            => this.inicioEsperaSincronizacionMs = ahoraMs;

        public void Reiniciar()
            => this.inicioEsperaSincronizacionMs = null;

        public bool DebePausarSensorio(
            bool esModoPelican,
            bool enNightHold,
            bool enRollover,
            int horaJuego,
            long ahoraMs)
        {
            if (!esModoPelican)
            {
                this.inicioEsperaSincronizacionMs = null;
                return false;
            }

            if (enRollover)
            {
                this.inicioEsperaSincronizacionMs = ahoraMs;
                return true;
            }

            if (enNightHold)
            {
                this.inicioEsperaSincronizacionMs = null;
                return true;
            }

            if (this.inicioEsperaSincronizacionMs is long inicio)
            {
                if (horaJuego != HoraJuego.InicioDia || ahoraMs - inicio >= EsperaSincronizacionRelojMs)
                {
                    this.inicioEsperaSincronizacionMs = null;
                    return false;
                }

                return true;
            }

            return false;
        }
    }
}
