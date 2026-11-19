using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>
    /// Ciclo de vida de una intención del personaje (GSS §5.1).
    /// <c>propuesta → validada → activa → cumplida | bloqueada | caducada | sustituida</c>,
    /// o <c>rechazada</c> desde <c>propuesta</c> / <c>validada</c>.
    /// </summary>
    public enum EstadoIntencion
    {
        Propuesta,
        Validada,
        Activa,
        Cumplida,
        Bloqueada,
        Caducada,
        Sustituida,
        Rechazada,
    }

    /// <summary>
    /// Intención validable y ejecutable de un personaje (GSS §5.1, ADS §4.1).
    /// Mantiene identidad estable, objetivo, plazo, pesos de atención, plan, cambios de rutina y
    /// máquina de estados con transición verificada.
    /// </summary>
    public sealed class IntencionPersonaje
    {
        private readonly Dictionary<string, double> pesosAtencion;
        private readonly List<PasoPlan> plan;
        private readonly List<CambioRutina> cambiosRutina;

        public IntencionPersonaje(
            string id,
            string objetivo,
            string porque,
            TiempoJuego? plazoJuego = null,
            IReadOnlyDictionary<string, double>? pesosAtencion = null,
            IReadOnlyList<PasoPlan>? plan = null,
            IReadOnlyList<CambioRutina>? cambiosRutina = null,
            long? basadaEn = null)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("El identificador de la intención no puede estar vacío.", nameof(id));
            }

            this.Id = id.Trim();
            this.Objetivo = objetivo ?? string.Empty;
            this.Porque = porque ?? string.Empty;
            this.PlazoJuego = plazoJuego is null ? null : new TiempoJuego { Dia = plazoJuego.Dia, Hora = plazoJuego.Hora };
            this.pesosAtencion = pesosAtencion is null
                ? new Dictionary<string, double>(StringComparer.Ordinal)
                : new Dictionary<string, double>(pesosAtencion, StringComparer.Ordinal);
            this.plan = plan is null
                ? new List<PasoPlan>()
                : plan.Where(p => p is not null).Select(ClonarPaso).ToList();
            this.cambiosRutina = cambiosRutina is null
                ? new List<CambioRutina>()
                : cambiosRutina.Where(c => c is not null).Select(ClonarCambio).ToList();
            this.BasadaEn = basadaEn;
            this.Estado = EstadoIntencion.Propuesta;
            this.MotivosRechazo = Array.Empty<MotivoRechazo>();
        }

        public string Id { get; }

        public string Objetivo { get; }

        public string Porque { get; }

        public TiempoJuego? PlazoJuego { get; }

        public IReadOnlyDictionary<string, double> PesosAtencion => this.pesosAtencion;

        public IReadOnlyList<PasoPlan> Plan => this.plan;

        public IReadOnlyList<CambioRutina> CambiosRutina => this.cambiosRutina;

        public long? BasadaEn { get; }

        public EstadoIntencion Estado { get; private set; }

        public IReadOnlyList<MotivoRechazo> MotivosRechazo { get; private set; }

        public bool EsTerminal => EsEstadoTerminal(this.Estado);

        public bool EstaActiva => this.Estado == EstadoIntencion.Activa;

        /// <summary>
        /// Pesos de atención aplicables a la saliencia mientras la intención está <see cref="EstadoIntencion.Activa"/>.
        /// Fuera de ese estado (o sin pesos declarados) devuelve <c>null</c> para que la capa valga 1.
        /// </summary>
        public IReadOnlyDictionary<string, double>? PesosAtencionActivos
            => this.Estado == EstadoIntencion.Activa && this.pesosAtencion.Count > 0 ? this.pesosAtencion : null;

        public static IntencionPersonaje DesdePropuesta(string id, IntencionPropuesta propuesta, long? basadaEn = null)
        {
            if (propuesta is null)
            {
                throw new ArgumentNullException(nameof(propuesta));
            }

            return new IntencionPersonaje(
                id,
                propuesta.Objetivo,
                propuesta.Porque,
                propuesta.PlazoJuego,
                propuesta.PesosAtencion,
                propuesta.Plan,
                propuesta.CambiosRutina,
                basadaEn);
        }

        public static bool EsEstadoTerminal(EstadoIntencion estado) => estado is
            EstadoIntencion.Cumplida or
            EstadoIntencion.Bloqueada or
            EstadoIntencion.Caducada or
            EstadoIntencion.Sustituida or
            EstadoIntencion.Rechazada;

        public static string NombreEstado(EstadoIntencion estado) => estado switch
        {
            EstadoIntencion.Propuesta => "propuesta",
            EstadoIntencion.Validada => "validada",
            EstadoIntencion.Activa => "activa",
            EstadoIntencion.Cumplida => "cumplida",
            EstadoIntencion.Bloqueada => "bloqueada",
            EstadoIntencion.Caducada => "caducada",
            EstadoIntencion.Sustituida => "sustituida",
            EstadoIntencion.Rechazada => "rechazada",
            _ => throw new ArgumentOutOfRangeException(nameof(estado), estado, null),
        };

        /// <summary>Compara un <see cref="TiempoJuego"/> contra el instante vigente <c>(dia, hora)</c>.</summary>
        public static int CompararTiempo(TiempoJuego tiempo, int dia, int hora)
        {
            if (tiempo is null)
            {
                throw new ArgumentNullException(nameof(tiempo));
            }

            var porDia = tiempo.Dia.CompareTo(dia);
            if (porDia != 0)
            {
                return porDia;
            }

            return HoraJuego.MinutosDesdeInicio(tiempo.Hora).CompareTo(HoraJuego.MinutosDesdeInicio(hora));
        }

        /// <summary>Indica si <paramref name="plazo"/> es estrictamente posterior a <c>(diaActual, horaActual)</c>.</summary>
        public static bool EsTiempoFuturo(TiempoJuego plazo, int diaActual, int horaActual)
            => CompararTiempo(plazo, diaActual, horaActual) > 0;

        /// <summary>Indica si <paramref name="plazo"/> ya se ha alcanzado o superado en <c>(diaActual, horaActual)</c>.</summary>
        public static bool HaVencidoPlazo(TiempoJuego plazo, int diaActual, int horaActual)
            => CompararTiempo(plazo, diaActual, horaActual) <= 0;

        public bool HaVencidoPlazo(Observacion observacion)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            return this.PlazoJuego is not null && HaVencidoPlazo(this.PlazoJuego, observacion.Dia, observacion.Hora);
        }

        public bool PuedeTransicionarA(EstadoIntencion destino) => (this.Estado, destino) switch
        {
            (EstadoIntencion.Propuesta, EstadoIntencion.Validada) => true,
            (EstadoIntencion.Propuesta, EstadoIntencion.Rechazada) => true,
            (EstadoIntencion.Validada, EstadoIntencion.Activa) => true,
            (EstadoIntencion.Validada, EstadoIntencion.Rechazada) => true,
            (EstadoIntencion.Activa, EstadoIntencion.Cumplida) => true,
            (EstadoIntencion.Activa, EstadoIntencion.Bloqueada) => true,
            (EstadoIntencion.Activa, EstadoIntencion.Caducada) => true,
            (EstadoIntencion.Activa, EstadoIntencion.Sustituida) => true,
            _ => false,
        };

        public void TransicionarA(EstadoIntencion destino)
        {
            if (!this.PuedeTransicionarA(destino))
            {
                throw new InvalidOperationException(
                    $"No se puede cambiar la intención «{this.Id}» de «{NombreEstado(this.Estado)}» a «{NombreEstado(destino)}».");
            }

            this.Estado = destino;
        }

        public void MarcarValidada() => this.TransicionarA(EstadoIntencion.Validada);

        public void Activar() => this.TransicionarA(EstadoIntencion.Activa);

        public void Rechazar(IReadOnlyList<MotivoRechazo>? motivos = null)
        {
            this.TransicionarA(EstadoIntencion.Rechazada);
            this.MotivosRechazo = motivos is null ? Array.Empty<MotivoRechazo>() : motivos.ToList();
        }

        public void Cumplir(Observacion? observacion = null, Disparadores? disparadores = null, long ahoraMs = 0)
            => this.FinalizarActiva(EstadoIntencion.Cumplida, observacion, disparadores, ahoraMs);

        public void Bloquear(Observacion? observacion = null, Disparadores? disparadores = null, long ahoraMs = 0)
            => this.FinalizarActiva(EstadoIntencion.Bloqueada, observacion, disparadores, ahoraMs);

        public void Sustituir(Observacion? observacion = null, Disparadores? disparadores = null, long ahoraMs = 0)
            => this.FinalizarActiva(EstadoIntencion.Sustituida, observacion, disparadores, ahoraMs);

        public void Caducar(Observacion? observacion = null, Disparadores? disparadores = null, long ahoraMs = 0)
            => this.FinalizarActiva(EstadoIntencion.Caducada, observacion, disparadores, ahoraMs);

        /// <summary>
        /// Si la intención está <see cref="EstadoIntencion.Activa"/> y su <see cref="PlazoJuego"/> venció en
        /// <paramref name="observacion"/>, pasa a <see cref="EstadoIntencion.Caducada"/> y emite la pulsión
        /// <c>fin_intencion</c> en <paramref name="disparadores"/> si se proporciona.
        /// </summary>
        public bool CaducarSiVencida(Observacion observacion, Disparadores? disparadores = null, long ahoraMs = 0)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            if (this.Estado != EstadoIntencion.Activa || !this.HaVencidoPlazo(observacion))
            {
                return false;
            }

            this.Caducar(observacion, disparadores, ahoraMs);
            return true;
        }

        public IntencionPropuesta APropuestaContrato() => new()
        {
            Objetivo = this.Objetivo,
            Porque = this.Porque,
            PlazoJuego = this.PlazoJuego is null ? null : new TiempoJuego { Dia = this.PlazoJuego.Dia, Hora = this.PlazoJuego.Hora },
            PesosAtencion = this.pesosAtencion.Count == 0 ? null : new Dictionary<string, double>(this.pesosAtencion, StringComparer.Ordinal),
            Plan = this.plan.Select(ClonarPaso).ToList(),
            CambiosRutina = this.cambiosRutina.Count == 0 ? null : this.cambiosRutina.Select(ClonarCambio).ToList(),
        };

        public IntencionActiva AIntencionActivaContrato() => new()
        {
            Id = this.Id,
            Objetivo = this.Objetivo,
        };

        public ContextoSaliencia AplicarAContexto(ContextoSaliencia contexto)
        {
            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            return contexto.ConIntencion(this);
        }

        private void FinalizarActiva(EstadoIntencion destino, Observacion? observacion, Disparadores? disparadores, long ahoraMs)
        {
            if (disparadores is not null && observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion), "Para emitir la pulsión de fin de intención hace falta la observación vigente.");
            }

            this.TransicionarA(destino);
            if (disparadores is not null && observacion is not null)
            {
                disparadores.FinIntencion(this.Id, NombreEstado(destino), observacion, ahoraMs);
            }
        }

        internal static PasoPlan ClonarPaso(PasoPlan paso) => new()
        {
            Accion = paso.Accion,
            Lugar = paso.Lugar,
            Objetivo = paso.Objetivo,
            Obra = paso.Obra,
            Tipo = paso.Tipo,
            Texto = paso.Texto,
            DuracionMin = paso.DuracionMin,
            CamposExtra = paso.CamposExtra is null ? null : new Dictionary<string, System.Text.Json.JsonElement>(paso.CamposExtra),
        };

        private static CambioRutina ClonarCambio(CambioRutina cambio) => new()
        {
            Hito = cambio.Hito,
            Cambio = cambio.Cambio,
            Hora = cambio.Hora,
            Plan = cambio.Plan?.Select(ClonarPaso).ToList(),
        };
    }
}
