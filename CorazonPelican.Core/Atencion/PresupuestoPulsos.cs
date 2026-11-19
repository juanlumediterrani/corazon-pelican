using System;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>Instantánea por personaje para el guardado (CP-107). Cada cadena tiene una plaza propia.</summary>
    public sealed record ContadoresPulsos(
        int Dia,
        int OrdinariosLibresGastados = 0,
        int ExtrasCompromisosGastados = 0,
        bool DespertarEmitido = false,
        bool AcostarseEmitido = false,
        int Interlocuciones = 0);

    /// <summary>Contabilidad diaria de un personaje en el hilo del motor (R-04, P-12).</summary>
    internal sealed class PresupuestoPulsos
    {
        private readonly PresupuestoModo limites;
        private ContadoresPulsos? actual;
        // Los sucesos de la víspera pueden drenarse junto al primer paso del nuevo día.
        private ContadoresPulsos? anterior;

        public PresupuestoPulsos(Presupuesto perfil, ModoReloj modo, ContadoresPulsos? guardados)
        {
            this.limites = modo switch
            {
                ModoReloj.Vanilla => perfil.Vanilla,
                ModoReloj.Pelican => perfil.Pelican,
                _ => throw new ArgumentOutOfRangeException(nameof(modo)),
            };
            this.actual = guardados;
        }

        public ContadoresPulsos? Estado => this.actual;

        public void AbrirDia(int dia)
        {
            if (this.actual is null || this.actual.Dia < dia)
            {
                this.anterior = this.actual;
                this.actual = new ContadoresPulsos(dia);
            }
        }

        public bool Admitir(Disparo disparo)
        {
            this.AbrirDia(disparo.Dia);
            if (this.actual!.Dia == disparo.Dia)
            {
                return this.Gastar(disparo, ref this.actual);
            }

            return this.anterior?.Dia == disparo.Dia && this.Gastar(disparo, ref this.anterior);
        }

        private bool Gastar(Disparo disparo, ref ContadoresPulsos? contadores)
        {
            var c = contadores!;
            if (disparo.Motivo == Motivos.Interlocucion)
            {
                contadores = c with { Interlocuciones = c.Interlocuciones + 1 };
                return true;
            }

            if (disparo.Motivo == Motivos.Hito && disparo.Hito?.Origen == HitosRutina.OrigenRutina)
            {
                if (disparo.Hito.Id == HitosRutina.Despertar)
                {
                    contadores = c with { DespertarEmitido = true };
                    return !c.DespertarEmitido;
                }

                if (disparo.Hito.Id == HitosRutina.Acostarse)
                {
                    contadores = c with { AcostarseEmitido = true };
                    return !c.AcostarseEmitido;
                }
            }

            if (c.OrdinariosLibresGastados < this.limites.PulsosPorDiaJuego - this.limites.ReservadosCadenas)
            {
                contadores = c with { OrdinariosLibresGastados = c.OrdinariosLibresGastados + 1 };
                return true;
            }

            if (disparo.Motivo == Motivos.Compromiso && c.ExtrasCompromisosGastados < this.limites.ExtraCompromisos)
            {
                contadores = c with { ExtrasCompromisosGastados = c.ExtrasCompromisosGastados + 1 };
                return true;
            }

            return false;
        }
    }
}
