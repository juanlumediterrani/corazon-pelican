using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>Motivos de pulsión (GSS §4.1), con los mismos nombres que el contrato.</summary>
    public static class Motivos
    {
        public const string Pico = "pico";
        public const string Interlocucion = "interlocucion";
        public const string Deriva = "deriva";
        public const string Hito = "hito";
        public const string FinIntencion = "fin_intencion";
        public const string Compromiso = "compromiso";
        public const string Transicion = "transicion";
    }

    /// <summary>Una entrada que queda hoy en la agenda (pensamiento en cascada, GSS §4.1).</summary>
    public sealed record PendienteAgenda(string Hito, int Hora);

    /// <summary>
    /// La decisión de despertar a la mente: por qué, cuándo y sobre qué. Con ella se construye el pulso
    /// (escena, búfer, huella… en CP-104 y CP-203).
    /// </summary>
    public sealed record Disparo(
        string Motivo,
        long MilisegundosReales,
        int Dia,
        int Hora,
        string? Ref = null,
        HitoResuelto? Hito = null,
        IReadOnlyList<PendienteAgenda>? Agenda = null,
        string? Detalle = null,
        bool Anticipado = false);

    /// <summary>
    /// Decide cuándo algo merece pensamiento (GSS §4.1–4.2). Dos familias:
    /// <list type="bullet">
    /// <item>Estímulos (pico, deriva, transición): sujetos al período refractario.</item>
    /// <item>Rutina e interlocución (hitos, despertar, acostarse, fin de intención, compromiso, alguien
    /// que habla): atraviesan el refractario y consultan su cupo.</item>
    /// </list>
    /// Toda pulsión admitida reinicia el integrador y abre una ventana refractaria.
    /// </summary>
    public sealed class Disparadores
    {
        /// <summary>Luz por debajo de la cual «se ha puesto el sol».</summary>
        public const double LuzAnochecer = 0.5;

        private readonly PerfilPersonaje perfil;
        private readonly PresupuestoPulsos presupuesto;
        private readonly Integrador integrador;
        private readonly Refractario refractario;
        private readonly Queue<Disparo> transicionesPendientes = new();
        private readonly Dictionary<string, long> planesIntencion = new();
        private readonly List<Disparo> rutinaPendiente = new();
        private readonly Dictionary<string, double> salienciaAnterior = new(StringComparer.Ordinal);
        private HashSet<string> sobrePico = new();
        private string? anticipadoActivoRef;
        private int? dia;
        private int? horaAnterior;
        private int siguienteEntrada;
        private long inicioPlanRutinaMs;
        private bool? deDia;
        private string? clima;

        public Disparadores(PerfilPersonaje perfil, ModoReloj modo = ModoReloj.Vanilla, ContadoresPulsos? guardados = null)
        {
            this.perfil = perfil;
            this.presupuesto = new PresupuestoPulsos(perfil.Presupuesto, modo, guardados);
            this.integrador = new Integrador(perfil.Umbrales.DerivaSemividaMin);
            this.refractario = new Refractario(perfil.Refractario);
        }

        public ContadoresPulsos? Contadores => this.presupuesto.Estado;

        public bool TieneCupoOrdinarioDisponible => this.presupuesto.TieneCupoOrdinarioDisponible;

        /// <summary>Habilita o deshabilita el pensamiento anticipado cuando un granjero se aproxima (CP-507).</summary>
        public bool AnticipacionHabilitada { get; set; } = true;

        /// <summary>Limpia el estado de anticipación activa tras consumirse o cancelarse.</summary>
        public void LimpiarAnticipado(string? interlocutorRef = null)
        {
            if (interlocutorRef is null || string.Equals(this.anticipadoActivoRef, interlocutorRef, StringComparison.Ordinal))
            {
                this.anticipadoActivoRef = null;
            }
        }

        /// <summary>CP-502 recibe el identificador del compromiso que necesita una prórroga.</summary>
        public event Action<Disparo>? CompromisoSinCupo;

        public double ValorIntegrador => this.integrador.Valor;

        public long? UltimoPulsoMs { get; private set; }

        /// <summary>Hitos que no se pensaron porque ya se había pensado durante su plan.</summary>
        public List<HitoResuelto> Omitidos { get; } = new();

        /// <summary>
        /// Un paso del sensorio: la observación con sus bandas, el horario de hoy (o <c>null</c> en un
        /// festival) y cuánto tiempo de juego ha pasado desde el paso anterior.
        /// </summary>
        public IReadOnlyList<Disparo> Observar(Observacion observacion, IReadOnlyList<EntidadEnBanda> bandas, HorarioDia? horario, long ahoraMs, double minutosJuego)
        {
            var salida = new List<Disparo>();
            this.CambioDeDia(observacion, horario, ahoraMs, salida);
            this.Rutina(observacion, horario, ahoraMs, salida);
            foreach (var disparo in this.rutinaPendiente)
            {
                this.AgregarSiHayCupo(salida, disparo);
            }

            this.rutinaPendiente.Clear();

            this.DetectarTransiciones(observacion, ahoraMs);
            this.integrador.Acumular(
                bandas.Where(b => b.Banda is Banda.Periferia or Banda.Mencion).Sum(b => b.Saliencia),
                minutosJuego);
            this.Estimulos(observacion, bandas, ahoraMs, salida);
            this.DetectarAnticipacion(observacion, bandas, ahoraMs, salida);

            this.sobrePico = bandas.Where(b => b.Saliencia >= this.perfil.Umbrales.Pico).Select(b => b.Ref).ToHashSet();
            this.ActualizarSalienciasGranjeros(observacion, bandas);
            if (salida.Count > 0)
            {
                this.Registrar(ahoraMs);
            }

            return salida;
        }

        private void DetectarAnticipacion(Observacion observacion, IReadOnlyList<EntidadEnBanda> bandas, long ahoraMs, List<Disparo> salida)
        {
            if (!this.AnticipacionHabilitada
                || this.anticipadoActivoRef is not null
                || salida.Count > 0
                || this.refractario.EnVentana(ahoraMs))
            {
                return;
            }

            foreach (var entidad in observacion.Entidades)
            {
                if (entidad.Categoria != CategoriaEntidad.Granjero)
                {
                    continue;
                }

                var banda = bandas.FirstOrDefault(b => string.Equals(b.Ref, entidad.Ref, StringComparison.Ordinal));
                if (banda is null)
                {
                    continue;
                }

                if (this.salienciaAnterior.TryGetValue(entidad.Ref, out var previa)
                    && banda.Saliencia > previa + this.perfil.Umbrales.Histeresis
                    && banda.Saliencia >= this.perfil.Umbrales.Mencion
                    && banda.Saliencia < this.perfil.Umbrales.Pico)
                {
                    var countAntes = salida.Count;
                    var disparo = new Disparo(
                        Motivos.Interlocucion,
                        ahoraMs,
                        observacion.Dia,
                        observacion.Hora,
                        Ref: entidad.Ref,
                        Anticipado: true);
                    this.AgregarSiHayCupo(salida, disparo);
                    if (salida.Count > countAntes)
                    {
                        this.anticipadoActivoRef = entidad.Ref;
                    }

                    break;
                }
            }
        }

        private void ActualizarSalienciasGranjeros(Observacion observacion, IReadOnlyList<EntidadEnBanda> bandas)
        {
            var presentes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entidad in observacion.Entidades)
            {
                if (entidad.Categoria != CategoriaEntidad.Granjero)
                {
                    continue;
                }

                presentes.Add(entidad.Ref);
                var banda = bandas.FirstOrDefault(b => string.Equals(b.Ref, entidad.Ref, StringComparison.Ordinal));
                this.salienciaAnterior[entidad.Ref] = banda?.Saliencia ?? 0.0;
            }

            foreach (var existente in this.salienciaAnterior.Keys.Where(k => !presentes.Contains(k)).ToList())
            {
                this.salienciaAnterior.Remove(existente);
            }
        }

        /// <summary>Alguien le habla: pasa siempre, también en la ventana refractaria.</summary>
        public Disparo Interlocucion(string interlocutor, Observacion observacion, long ahoraMs)
        {
            var disparo = new Disparo(Motivos.Interlocucion, ahoraMs, observacion.Dia, observacion.Hora, Ref: interlocutor);
            this.presupuesto.Admitir(disparo);
            this.Registrar(ahoraMs);
            return disparo;
        }

        /// <summary>El cuerpo empieza un plan pedido por la mente.</summary>
        public void IniciarPlanIntencion(string idPlan, long ahoraMs) => this.planesIntencion[idPlan] = ahoraMs;

        /// <summary>
        /// El cuerpo termina ese plan: hito de intención, salvo que se pensara durante el plan y el perfil
        /// diga que se omita. Sale en el siguiente <see cref="Observar"/>.
        /// </summary>
        public void TerminarPlanIntencion(string idPlan, Observacion observacion, long ahoraMs)
        {
            var inicio = this.planesIntencion.GetValueOrDefault(idPlan, ahoraMs);
            this.planesIntencion.Remove(idPlan);
            var hito = HitosRutina.DeIntencion(idPlan, this.perfil.Rutina);
            if (hito.OmitirSiYaPenso && this.UltimoPulsoMs > inicio)
            {
                this.Omitidos.Add(hito);
                return;
            }

            this.rutinaPendiente.Add(new Disparo(Motivos.Hito, ahoraMs, observacion.Dia, observacion.Hora, Hito: hito));
        }

        /// <summary>Una intención termina (cumplida, caducada o bloqueada). Sale en el siguiente paso.</summary>
        public void FinIntencion(string idIntencion, string estado, Observacion observacion, long ahoraMs)
            => this.rutinaPendiente.Add(new Disparo(Motivos.FinIntencion, ahoraMs, observacion.Dia, observacion.Hora, Ref: idIntencion, Detalle: estado));

        /// <summary>Un compromiso se acerca a su plazo (la curva es de CP-502). Sale en el siguiente paso.</summary>
        public void Compromiso(string idCompromiso, Observacion observacion, long ahoraMs)
            => this.rutinaPendiente.Add(new Disparo(Motivos.Compromiso, ahoraMs, observacion.Dia, observacion.Hora, Ref: idCompromiso));

        /// <summary>
        /// El día termina antes de que el personaje se acueste (el jugador duerme, <c>DayEnding</c>): si
        /// aún no se acostó, la cadena de acostarse sale ahora.
        /// </summary>
        public Disparo? FinDeDia(Observacion observacion, long ahoraMs)
        {
            var disparo = new Disparo(Motivos.Hito, ahoraMs, observacion.Dia, observacion.Hora,
                Hito: HitosRutina.DelMotor(HitosRutina.Acostarse, this.perfil.Rutina), Detalle: "fin_de_dia");
            if (!this.presupuesto.Admitir(disparo))
            {
                return null;
            }

            this.Registrar(ahoraMs);
            return disparo;
        }

        /// <summary>
        /// Fuerza un pulso manual (<c>cp_pulso</c>) consumiendo saldo ordinario del presupuesto diario.
        /// Devuelve <c>null</c> si no queda saldo ordinario disponible.
        /// </summary>
        public Disparo? ForzarPulso(Observacion observacion, long ahoraMs, string motivo = "manual")
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            var detalle = string.IsNullOrWhiteSpace(motivo) ? "manual" : motivo.Trim();
            var motivoEfectivo = detalle is Motivos.Pico or Motivos.Deriva or Motivos.Transicion or Motivos.FinIntencion or Motivos.Compromiso
                ? detalle
                : Motivos.Deriva;
            var diaObs = Math.Max(1, observacion.Dia);
            var disparo = new Disparo(motivoEfectivo, ahoraMs, diaObs, observacion.Hora, Detalle: detalle);

            if (!this.presupuesto.Admitir(disparo))
            {
                return null;
            }

            this.Registrar(ahoraMs);
            return disparo;
        }

        private void CambioDeDia(Observacion o, HorarioDia? horario, long ahoraMs, List<Disparo> salida)
        {
            if (this.dia == o.Dia)
            {
                return;
            }

            if (this.dia is not null)
            {
                this.AgregarSiHayCupo(salida, new Disparo(Motivos.Hito, ahoraMs, this.dia.Value, HoraJuego.FinDia,
                    Hito: HitosRutina.DelMotor(HitosRutina.Acostarse, this.perfil.Rutina), Detalle: "fin_de_dia"));
            }

            var empiezaElDia = this.dia is not null || o.Hora == HoraJuego.InicioDia;
            this.dia = o.Dia;
            this.presupuesto.AbrirDia(o.Dia);
            this.salienciaAnterior.Clear();
            this.anticipadoActivoRef = null;

            // Amanecer con lluvia no es «empezar a llover»: luz y clima se toman de nuevo como referencia.
            this.deDia = null;
            this.clima = null;
            this.horaAnterior = o.Hora;
            this.inicioPlanRutinaMs = ahoraMs;

            // Si el motor arranca a media jornada, las entradas ya pasadas no son hitos.
            this.siguienteEntrada = horario?.Entradas.Count(e => e.Hora <= o.Hora) ?? 0;
            if (empiezaElDia)
            {
                this.AgregarSiHayCupo(salida, new Disparo(Motivos.Hito, ahoraMs, o.Dia, o.Hora,
                    Hito: HitosRutina.DelMotor(HitosRutina.Despertar, this.perfil.Rutina), Agenda: Agenda(horario, 0)));
            }
        }

        private void Rutina(Observacion o, HorarioDia? horario, long ahoraMs, List<Disparo> salida)
        {
            if (horario is null || this.Contadores!.AcostarseEmitido)
            {
                this.horaAnterior = o.Hora;
                return;
            }

            // Salto horario brusco en el mismo día (p. ej. arranque en NightHold 600 -> 2550 o p4e_time):
            // descartar hitos diurnos intermedios ya rebasados para no disparar ráfagas duplicadas (CP-608).
            if (this.horaAnterior is int previa && o.Hora - previa >= 60)
            {
                while (this.siguienteEntrada < horario.Entradas.Count - 1
                    && horario.Entradas[this.siguienteEntrada].Hora < o.Hora)
                {
                    this.siguienteEntrada++;
                }
            }

            this.horaAnterior = o.Hora;

            while (this.siguienteEntrada < horario.Entradas.Count && !AntesDeDisponerse(o.Hora, horario.Entradas[this.siguienteEntrada].Hora))
            {
                var indice = this.siguienteEntrada++;
                var entrada = horario.Entradas[indice];
                var ultimoPlanMs = this.inicioPlanRutinaMs;
                this.inicioPlanRutinaMs = ahoraMs;

                if (indice == horario.Entradas.Count - 1)
                {
                    // La última entrada del día es irse a dormir: es el propio acostarse.
                    this.AgregarSiHayCupo(salida, new Disparo(Motivos.Hito, ahoraMs, o.Dia, o.Hora,
                        Hito: HitosRutina.DelMotor(HitosRutina.Acostarse, this.perfil.Rutina), Agenda: Agenda(horario, indice + 1)));
                    return;
                }

                var hito = HitosRutina.Resolver(horario, entrada, this.perfil.Rutina);
                var pensoDuranteElPlan = this.UltimoPulsoMs > ultimoPlanMs || salida.Count > 0;
                if (hito.OmitirSiYaPenso && pensoDuranteElPlan)
                {
                    this.Omitidos.Add(hito);
                    continue;
                }

                this.AgregarSiHayCupo(salida, new Disparo(Motivos.Hito, ahoraMs, o.Dia, o.Hora, Hito: hito, Agenda: Agenda(horario, indice + 1)));
            }
        }

        private void DetectarTransiciones(Observacion o, long ahoraMs)
        {
            var deDiaAhora = o.Luz >= LuzAnochecer;
            if (this.deDia is bool antes && antes != deDiaAhora)
            {
                this.transicionesPendientes.Enqueue(new Disparo(Motivos.Transicion, ahoraMs, o.Dia, o.Hora, Detalle: deDiaAhora ? "amanece" : "anochece"));
            }

            if (this.clima is not null && this.clima != o.Clima)
            {
                this.transicionesPendientes.Enqueue(new Disparo(Motivos.Transicion, ahoraMs, o.Dia, o.Hora, Detalle: $"clima:{o.Clima}"));
            }

            this.deDia = deDiaAhora;
            this.clima = o.Clima;
        }

        private void Estimulos(Observacion o, IReadOnlyList<EntidadEnBanda> bandas, long ahoraMs, List<Disparo> salida)
        {
            if (salida.Count > 0 || this.refractario.EnVentana(ahoraMs))
            {
                return;
            }

            if (this.transicionesPendientes.Count > 0)
            {
                var transicion = this.transicionesPendientes.Dequeue();
                this.AgregarSiHayCupo(salida, transicion with { MilisegundosReales = ahoraMs, Dia = o.Dia, Hora = o.Hora });
                return;
            }

            var factor = this.refractario.Factor(ahoraMs);
            var pico = bandas
                .Where(b => b.Saliencia >= this.perfil.Umbrales.Pico * factor && !this.sobrePico.Contains(b.Ref))
                .OrderByDescending(b => b.Saliencia)
                .FirstOrDefault();
            if (pico is not null)
            {
                this.AgregarSiHayCupo(salida, new Disparo(Motivos.Pico, ahoraMs, o.Dia, o.Hora, Ref: pico.Ref));
                return;
            }

            if (this.integrador.Valor >= this.perfil.Umbrales.Deriva * factor)
            {
                this.AgregarSiHayCupo(salida, new Disparo(Motivos.Deriva, ahoraMs, o.Dia, o.Hora, Detalle: $"{this.integrador.Valor:0.##}"));
            }
        }

        private void AgregarSiHayCupo(List<Disparo> salida, Disparo candidato)
        {
            if (this.presupuesto.Admitir(candidato))
            {
                salida.Add(candidato);
            }
            else if (candidato.Motivo == Motivos.Compromiso)
            {
                this.CompromisoSinCupo?.Invoke(candidato);
            }
        }

        private void Registrar(long ahoraMs)
        {
            this.UltimoPulsoMs = ahoraMs;
            this.refractario.Registrar(ahoraMs);
            this.integrador.Reiniciar();
        }

        /// <summary>La pulsión de una entrada sale diez minutos de juego antes de su hora: al disponerse, cuando aún se puede cambiar.</summary>
        private static bool AntesDeDisponerse(int horaActual, int horaEntrada)
            => HoraJuego.MinutosDesdeInicio(horaActual) < HoraJuego.MinutosDesdeInicio(horaEntrada) - 10;

        private static IReadOnlyList<PendienteAgenda> Agenda(HorarioDia? horario, int desde)
            => horario is null
                ? new List<PendienteAgenda>()
                : horario.Entradas.Skip(desde).Select(e => new PendienteAgenda(HitosRutina.IdDe(horario, e), e.Hora)).ToList();
    }
}
