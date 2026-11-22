using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Observabilidad;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>Resultado de ejecutar un paso del bucle sensorio → atención → pulso de un personaje.</summary>
    public sealed record ResultadoPasoHabitante(
        EscenaGenerada? Escena,
        IReadOnlyList<Pulso> Pulsos,
        IReadOnlyList<Disparo> Disparos,
        IReadOnlyList<EntidadEnBanda> Bandas);

    /// <summary>Resultado de forzar un pulso manual (<c>cp_pulso</c>).</summary>
    public sealed record ResultadoPulsoForzado(
        bool Emitido,
        Pulso? Pulso,
        EscenaGenerada? Escena,
        string? MotivoRechazo)
    {
        public static ResultadoPulsoForzado Exito(Pulso pulso, EscenaGenerada escena)
            => new(Emitido: true, pulso, escena, MotivoRechazo: null);

        public static ResultadoPulsoForzado Rechazado(string motivo)
            => new(Emitido: false, Pulso: null, Escena: null, motivo);
    }

    /// <summary>
    /// Orquestador puro del ciclo por personaje (ADS §4): sensorio (saliencia, novedad, bandas),
    /// memoria de trabajo (búfer, huella), disparadores, generación de escena/pulso, diario y telemetría.
    /// No depende de SMAPI ni contiene lógica específica de ningún personaje.
    /// </summary>
    public sealed class MotorHabitante
    {
        private readonly TelemetriaJsonl? telemetria;
        private readonly MedidorCosteBucle? medidor;
        private readonly Func<Pulso, Pulso>? publicarPulso;
        private readonly int tamanoMaxMemoriaKB;
        private readonly int tiempoMaxRespuestaMs;
        private readonly string modoValidacionMenciones;
        private readonly Bufer pendientesDiario;
        private readonly Dictionary<string, Banda> bandasRegistradasBufer = new(StringComparer.Ordinal);
        private readonly HashSet<string> compromisosAvisados = new(StringComparer.Ordinal);
        private readonly RastreadorTasaMenciones rastreadorMenciones = new();
        private int? ultimoDiaBufer;
        private int? ultimoDiaAgenda;
        private long siguientePulsoId = 1;

        public MotorHabitante(
            PerfilPersonaje perfil,
            PlantillasProsa plantillas,
            EstadoPersonaje estado,
            ModoReloj modoReloj = ModoReloj.Vanilla,
            TelemetriaJsonl? telemetria = null,
            MedidorCosteBucle? medidor = null,
            Func<Pulso, Pulso>? publicarPulso = null,
            int? maxEntradasBufer = null,
            int tamanoMaxMemoriaKB = 512,
            int tiempoMaxRespuestaMs = 8000,
            string modoValidacionMenciones = CorazonConfig.ValidacionMencionesRechazar)
        {
            this.Perfil = perfil ?? throw new ArgumentNullException(nameof(perfil));
            this.Plantillas = plantillas ?? throw new ArgumentNullException(nameof(plantillas));
            this.Estado = estado ?? throw new ArgumentNullException(nameof(estado));
            if (tamanoMaxMemoriaKB <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tamanoMaxMemoriaKB));
            }

            if (tiempoMaxRespuestaMs <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tiempoMaxRespuestaMs));
            }

            this.ModoReloj = modoReloj;
            this.telemetria = telemetria;
            this.medidor = medidor;
            this.publicarPulso = publicarPulso;
            this.tamanoMaxMemoriaKB = tamanoMaxMemoriaKB;
            this.tiempoMaxRespuestaMs = tiempoMaxRespuestaMs;
            this.modoValidacionMenciones = string.Equals(modoValidacionMenciones, CorazonConfig.ValidacionMencionesAviso, StringComparison.OrdinalIgnoreCase)
                ? CorazonConfig.ValidacionMencionesAviso
                : CorazonConfig.ValidacionMencionesRechazar;

            var configAtencion = perfil.ObtenerConfigAtencion();
            var topeBufer = maxEntradasBufer ?? configAtencion.BuferMaxEntradas;

            this.Novedad = new Novedad();
            this.ClasificadorBandas = new ClasificadorBandas(perfil.Umbrales);
            this.Bufer = new Bufer(topeBufer, configAtencion.BuferVentanaRecienteMin, configAtencion.BuferSemividaAntiguasMin);
            this.pendientesDiario = new Bufer(Math.Max(topeBufer, 64), configAtencion.BuferVentanaRecienteMin, configAtencion.BuferSemividaAntiguasMin);
            this.Huella = new HuellaAtencion(configAtencion.HuellaSemividaMin);
            this.Disparadores = new Disparadores(perfil, modoReloj, estado.Contadores);
            this.Disparadores.CompromisoSinCupo += disparo =>
            {
                if (!string.IsNullOrWhiteSpace(disparo.Ref))
                {
                    GestorCompromisos.RegistrarPeticionProrroga(
                        this.Estado,
                        disparo.Ref,
                        new TiempoJuego { Dia = disparo.Dia, Hora = disparo.Hora });
                }
            };
            this.LugaresOfrecidos = perfil.Lexico.OfrecerLugares(_ => true);
            this.ContextoRutina = new ContextoRutina(Math.Max(1, estado.Contadores?.Dia ?? 1));
            this.Reflejos = new EvaluadorReflejos(perfil);
        }

        public PerfilPersonaje Perfil { get; }

        public PlantillasProsa Plantillas { get; }

        public EstadoPersonaje Estado { get; }

        public ModoReloj ModoReloj { get; }

        public Novedad Novedad { get; }

        public ClasificadorBandas ClasificadorBandas { get; }

        public Bufer Bufer { get; }

        public HuellaAtencion Huella { get; }

        public Disparadores Disparadores { get; }

        public LugaresOfrecidos LugaresOfrecidos { get; }

        public AgendaDia? AgendaActual { get; private set; }

        public ContextoRutina ContextoRutina { get; }

        public EvaluadorReflejos Reflejos { get; }

        public RastreadorTasaMenciones RastreadorMenciones => this.rastreadorMenciones;

        public Observacion? UltimaObservacion { get; private set; }

        public EscenaGenerada? UltimaEscena { get; private set; }

        public string Epoca { get; set; } = "local";

        /// <summary>Estado de atención vigente del cuerpo (p. ej. estrecha al dibujar, abierta al pasear o levantar la vista).</summary>
        public EstadoAtencion EstadoAtencionActual { get; set; } = EstadoAtencion.Abierta;

        /// <summary>Referencia de entidad enfocada por <c>mirar</c>, o <c>null</c> tras <c>levantar_la_vista</c> (GSS §3.6).</summary>
        public string? FocoSensorioRef { get; set; }

        /// <summary>Obra activa mientras el personaje está dibujando.</summary>
        public string? ObraEnCursoActual { get; set; }

        /// <summary>Intención activa en curso sobre el personaje, si la hay.</summary>
        public IntencionPersonaje? IntencionActivaVigente { get; private set; }

        /// <summary>Ejecutor del cuerpo conectado a este motor, si se vinculó (CP-302).</summary>
        public EjecutorCuerpo? EjecutorCuerpo { get; private set; }

        /// <summary>Indica si la mente externa está habilitada para gobernar el cuerpo (R-11, CP-303).</summary>
        public bool MenteHabilitada { get; set; } = true;

        /// <summary>Último motivo por el que el motor degradó al personaje a su rutina canónica (CP-303).</summary>
        public string? UltimoMotivoDegradacion { get; private set; }

        /// <summary>Instante en milisegundos reales de la última degradación a rutina.</summary>
        public long? UltimaDegradacionMs { get; private set; }

        /// <summary>Número total de degradaciones automáticas a rutina ejecutadas en la sesión.</summary>
        public int TotalDegradaciones { get; private set; }

        /// <summary>Evento emitido cuando el motor devuelve automáticamente al personaje a su horario canónico (CP-303).</summary>
        public event Action<string, long>? DegradadoARutina;

        private Func<long>? relojMotorMs;
        private long? ultimoContactoMenteMs;
        private long? pulsoPendienteDesdeMs;
        private bool menteCaidaReportada;
        private string? errorModuloPendiente;

        /// <summary>Registra actividad reciente del proceso de la mente (latido, consulta o respuesta).</summary>
        public void NotificarActividadMente(long ahoraMs)
        {
            this.ultimoContactoMenteMs = ahoraMs;
            this.menteCaidaReportada = false;
        }

        /// <summary>Notifica que el proceso de la mente ha caído o cerrado conexión.</summary>
        public void NotificarMenteCaida(long ahoraMs)
        {
            this.menteCaidaReportada = true;
            if (this.EstaReclamadoOActuando())
            {
                this.DegradarARutina("mente_caida", ahoraMs);
            }
        }

        /// <summary>Notifica un fallo de módulo para degradar inmediatamente el personaje a su horario vanilla (CP-303).</summary>
        public void NotificarErrorModulo(string detalle, long ahoraMs)
        {
            this.errorModuloPendiente = string.IsNullOrWhiteSpace(detalle) ? "error_modulo" : detalle;
            this.DegradarARutina("error_modulo", ahoraMs);
        }

        /// <summary>Desactiva la mente en caliente (<c>cp_mente off</c>) y devuelve inmediatamente el personaje a su rutina si estaba reclamado.</summary>
        public bool DesactivarMente(long ahoraMs)
        {
            this.MenteHabilitada = false;
            return this.DegradarARutina("mente_deshabilitada", ahoraMs);
        }

        /// <summary>
        /// Cancela cualquier plan o intención activa, detiene animaciones de actividad y devuelve al personaje
        /// a su tramo vigente del horario canónico si estaba reclamado o actuando (CP-303, R-11).
        /// </summary>
        public bool DegradarARutina(string motivo, long ahoraMs)
        {
            var motivoNormalizado = string.IsNullOrWhiteSpace(motivo) ? "degradacion" : motivo.Trim();
            this.pulsoPendienteDesdeMs = null;
            this.menteCaidaReportada = false;
            this.errorModuloPendiente = null;

            var teniaIntencion = this.IntencionActivaVigente is { EstaActiva: true } || this.Estado.IntencionActiva is not null;
            if (this.IntencionActivaVigente is { EstaActiva: true } activa)
            {
                activa.Sustituir(this.UltimaObservacion, null, ahoraMs);
            }

            this.IntencionActivaVigente = null;
            this.Estado.IntencionActiva = null;
            this.EstadoAtencionActual = EstadoAtencion.Abierta;
            this.FocoSensorioRef = null;
            this.ObraEnCursoActual = null;

            var actuabaEnCuerpo = false;
            if (this.EjecutorCuerpo is not null)
            {
                var medios = this.EjecutorCuerpo.Contexto.Medios;
                actuabaEnCuerpo = this.EjecutorCuerpo.Ocupado
                    || this.EjecutorCuerpo.Continua is not null
                    || this.EjecutorCuerpo.Sentada
                    || medios.EstaReclamado
                    || medios.EstaEnAnimacionActividad;

                this.EjecutorCuerpo.CancelarPlan();
                this.EjecutorCuerpo.Contexto.AccionContinua = null;
                this.EjecutorCuerpo.Contexto.ObraEnCurso = null;
                this.EjecutorCuerpo.Contexto.Sentada = false;
                this.EjecutorCuerpo.Contexto.FocoObjetivoRef = null;
                this.EjecutorCuerpo.Contexto.EstadoAtencion = EstadoAtencion.Abierta;

                if (actuabaEnCuerpo)
                {
                    if (medios.EstaEnAnimacionActividad)
                    {
                        medios.DetenerAnimacionActividad();
                    }

                    medios.DetenerMovimiento();
                    medios.VolverARutina();
                }
            }

            if (!teniaIntencion && !actuabaEnCuerpo)
            {
                return false;
            }

            this.UltimoMotivoDegradacion = motivoNormalizado;
            this.UltimaDegradacionMs = ahoraMs;
            this.TotalDegradaciones++;
            this.DegradadoARutina?.Invoke(motivoNormalizado, ahoraMs);
            return true;
        }

        private bool EstaReclamadoOActuando()
        {
            if (this.IntencionActivaVigente is { EstaActiva: true } || this.Estado.IntencionActiva is not null)
            {
                return true;
            }

            if (this.EjecutorCuerpo is null)
            {
                return false;
            }

            var medios = this.EjecutorCuerpo.Contexto.Medios;
            return this.EjecutorCuerpo.Ocupado
                || this.EjecutorCuerpo.Continua is not null
                || medios.EstaReclamado
                || medios.EstaEnAnimacionActividad;
        }

        private void EvaluarDegradacionAutomatica(long ahoraMs)
        {
            if (!this.MenteHabilitada)
            {
                if (this.EstaReclamadoOActuando())
                {
                    this.DegradarARutina("mente_deshabilitada", ahoraMs);
                }

                return;
            }

            if (this.errorModuloPendiente is not null)
            {
                this.DegradarARutina("error_modulo", ahoraMs);
                return;
            }

            if (this.menteCaidaReportada)
            {
                if (this.EstaReclamadoOActuando())
                {
                    this.DegradarARutina("mente_caida", ahoraMs);
                }
                else
                {
                    this.menteCaidaReportada = false;
                }

                return;
            }

            if (!this.EstaReclamadoOActuando())
            {
                return;
            }

            var tramoDiezMinMs = HoraJuego.MilisegundosPorDiezMinutos(this.ModoReloj);
            var topeCaidaPulsoMs = Math.Min((long)this.tiempoMaxRespuestaMs, tramoDiezMinMs);

            if (this.pulsoPendienteDesdeMs is long pendienteDesde && ahoraMs - pendienteDesde >= topeCaidaPulsoMs)
            {
                this.DegradarARutina("mente_caida", ahoraMs);
                return;
            }

            if (this.ultimoContactoMenteMs is long ultimoContacto && ahoraMs - ultimoContacto >= tramoDiezMinMs)
            {
                this.DegradarARutina("mente_caida", ahoraMs);
                return;
            }

            if (!this.Disparadores.TieneCupoOrdinarioDisponible
                && (this.EjecutorCuerpo is null || !this.EjecutorCuerpo.Ocupado))
            {
                this.DegradarARutina("sin_presupuesto", ahoraMs);
            }
        }

        /// <summary>Vincula un <see cref="EjecutorCuerpo"/> para registrar prosa de acción en el búfer y disparar hitos al terminar planes.</summary>
        public void ConectarEjecutorCuerpo(EjecutorCuerpo ejecutor, Func<long>? relojMs = null)
        {
            this.EjecutorCuerpo = ejecutor ?? throw new ArgumentNullException(nameof(ejecutor));
            this.relojMotorMs = relojMs;

            ejecutor.AccionIniciada += (paso, _) =>
            {
                this.SincronizarDesdeContextoCuerpo(ejecutor.Contexto);
                var obs = this.UltimaObservacion;
                var dia = obs?.Dia ?? Math.Max(1, this.Estado.Contadores?.Dia ?? 1);
                var hora = obs?.Hora ?? HoraJuego.InicioDia;
                var nombres = obs?.Entidades.ToDictionary(e => e.Ref, e => e.Nombre, StringComparer.Ordinal);
                this.RegistrarAccion(paso, dia, hora, nombresEntidades: nombres);
            };

            ejecutor.AccionBloqueada += (_, _, ahoraMs) =>
            {
                this.SincronizarDesdeContextoCuerpo(ejecutor.Contexto);
                if (this.IntencionActivaVigente is { EstaActiva: true } activa)
                {
                    activa.Bloquear(this.UltimaObservacion, this.Disparadores, ahoraMs);
                    this.Estado.IntencionActiva = null;
                }
            };

            ejecutor.PlanTerminado += (idPlan, ahoraMs) =>
            {
                this.SincronizarDesdeContextoCuerpo(ejecutor.Contexto);
                if (this.IntencionActivaVigente is { EstaActiva: true } activa
                    && (string.IsNullOrWhiteSpace(idPlan) || string.Equals(activa.Id, idPlan, StringComparison.Ordinal)))
                {
                    activa.Cumplir();
                    this.Estado.IntencionActiva = null;
                }

                if (!string.IsNullOrWhiteSpace(idPlan) && this.UltimaObservacion is { } obs)
                {
                    this.Disparadores.TerminarPlanIntencion(idPlan, obs, ahoraMs);
                }
            };
        }

        private void SincronizarDesdeContextoCuerpo(ContextoEjecucionAccion contexto)
        {
            this.EstadoAtencionActual = contexto.EstadoAtencion;
            this.FocoSensorioRef = contexto.FocoObjetivoRef;
            this.ObraEnCursoActual = contexto.AccionContinua == "dibujar" ? contexto.ObraEnCurso : null;
        }

        /// <summary>
        /// Ejecuta un paso completo del bucle para <paramref name="observacion"/>:
        /// saliencia/novedad, bandas, búfer y huella, disparadores y emisión de escena/pulso/diario.
        /// </summary>
        public ResultadoPasoHabitante ProcesarPaso(
            Observacion observacion,
            HorarioDia? horario,
            long ahoraMs,
            double minutosJuego,
            EstadoAtencion? estadoAtencion = null,
            IReadOnlyDictionary<string, double>? pesosIntencion = null,
            string? intencionActiva = null,
            string? obraEnCurso = null,
            AgendaDia? agendaDia = null)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            this.EvaluarDegradacionAutomatica(ahoraMs);

            if (this.EjecutorCuerpo is not null)
            {
                this.SincronizarDesdeContextoCuerpo(this.EjecutorCuerpo.Contexto);
            }

            var estadoEfectivo = estadoAtencion ?? this.EstadoAtencionActual;
            var pesosEfectivos = pesosIntencion ?? this.IntencionActivaVigente?.PesosAtencionActivos;
            var intencionEfectiva = intencionActiva ?? this.IntencionActivaVigente?.Id ?? this.Estado.IntencionActiva?.Id;
            var obraEfectiva = obraEnCurso ?? this.ObraEnCursoActual;

            var obs = NormalizarObservacion(observacion);
            this.UltimaObservacion = obs;
            this.SincronizarAgenda(obs.Dia, horario, agendaDia);

            if (this.ultimoDiaBufer is int diaPrevio && diaPrevio != obs.Dia)
            {
                this.bandasRegistradasBufer.Clear();
            }

            this.ultimoDiaBufer = obs.Dia;

            IReadOnlyList<DesgloseSaliencia> saliencias;
            using (this.medidor?.Medir("saliencia"))
            {
                var contextoSaliencia = new ContextoSaliencia(
                    this.Perfil.Rasgos,
                    estadoEfectivo,
                    this.Perfil.Umbrales.Periferia,
                    pesosEfectivos,
                    this.FocoSensorioRef);
                saliencias = Saliencia.Calcular(obs, contextoSaliencia, this.Novedad);
                this.Novedad.Exponer(obs.Entidades.Select(e => e.Ref), minutosJuego);
            }

            IReadOnlyList<EntidadEnBanda> bandas;
            using (this.medidor?.Medir("bandas"))
            {
                bandas = this.ClasificadorBandas.Clasificar(saliencias);
            }

            using (this.medidor?.Medir("bufer_huella"))
            {
                this.Huella.Observar(obs, bandas, minutosJuego);
                this.AlimentarBufer(obs, bandas);
            }

            if (this.MenteHabilitada && this.EjecutorCuerpo is not null)
            {
                using (this.medidor?.Medir("reflejos"))
                {
                    if (this.Reflejos.EvaluarYEjecutar(obs, bandas, this.EjecutorCuerpo, ahoraMs) is not null)
                    {
                        this.SincronizarDesdeContextoCuerpo(this.EjecutorCuerpo.Contexto);
                        estadoEfectivo = estadoAtencion ?? this.EstadoAtencionActual;
                        obraEfectiva = obraEnCurso ?? this.ObraEnCursoActual;
                    }
                }
            }

            GestorCompromisos.Evaluar(this.Estado, obs, this.Disparadores, ahoraMs, this.compromisosAvisados);

            IReadOnlyList<Disparo> disparos;
            using (this.medidor?.Medir("disparadores"))
            {
                disparos = this.Disparadores.Observar(obs, bandas, horario, ahoraMs, minutosJuego);
            }

            GestorProrrogaCartas.PrepararCartasPendientes(this.Estado, this.Perfil, obs.Entidades.Select(e => e.Ref));

            var pulsos = new List<Pulso>();
            EscenaGenerada? escenaEmitida = null;

            if (disparos.Count > 0)
            {
                using (this.medidor?.Medir("pulso_escena"))
                {
                    foreach (var disparo in disparos)
                    {
                        var (pulso, escena) = this.EmitirPulsoDesdeDisparo(
                            obs,
                            disparo,
                            bandas,
                            horario,
                            agendaDia,
                            estadoEfectivo,
                            pesosEfectivos,
                            intencionEfectiva,
                            obraEfectiva,
                            origenTelemetria: disparo.Motivo);
                        pulsos.Add(pulso);
                        escenaEmitida = escena;
                        if (this.MenteHabilitada && this.EstaReclamadoOActuando())
                        {
                            this.pulsoPendienteDesdeMs ??= ahoraMs;
                        }
                    }
                }
            }

            this.Estado.Contadores = this.Disparadores.Contadores;
            return new ResultadoPasoHabitante(escenaEmitida, pulsos, disparos, bandas);
        }

        /// <summary>
        /// Genera la escena actual (<c>cp_escena</c>, <c>GET /api/v1/escena/{personaje}</c>) sin gastar presupuesto,
        /// sin mutar ni reiniciar <see cref="Huella"/>, <see cref="Integrador"/>, <see cref="Novedad"/> ni el diario.
        /// </summary>
        public EscenaGenerada PrevisualizarEscena(
            Observacion observacion,
            EstadoAtencion? estadoAtencion = null,
            IReadOnlyDictionary<string, double>? pesosIntencion = null,
            string? intencionActiva = null,
            string? obraEnCurso = null)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            if (this.EjecutorCuerpo is not null)
            {
                this.SincronizarDesdeContextoCuerpo(this.EjecutorCuerpo.Contexto);
            }

            var estadoEfectivo = estadoAtencion ?? this.EstadoAtencionActual;
            var pesosEfectivos = pesosIntencion ?? this.IntencionActivaVigente?.PesosAtencionActivos;
            var intencionEfectiva = intencionActiva ?? this.IntencionActivaVigente?.Id ?? this.Estado.IntencionActiva?.Id;
            var obraEfectiva = obraEnCurso ?? this.ObraEnCursoActual;

            var obs = NormalizarObservacion(observacion);
            var contextoSaliencia = new ContextoSaliencia(
                this.Perfil.Rasgos,
                estadoEfectivo,
                this.Perfil.Umbrales.Periferia,
                pesosEfectivos,
                this.FocoSensorioRef);

            var saliencias = Saliencia.Calcular(obs, contextoSaliencia, this.Novedad);
            var bandasSinMutar = new ClasificadorBandas(this.Perfil.Umbrales).Clasificar(saliencias);
            var contextoEscena = this.CrearContextoEscena(estadoEfectivo, pesosEfectivos, intencionEfectiva, obraEfectiva);
            return GeneradorEscena.Generar(obs, bandasSinMutar, this.LugaresOfrecidos, this.Plantillas, contextoEscena);
        }

        /// <summary>Construye el DTO <see cref="EscenaActual"/> para <c>GET /api/v1/escena/{personaje}</c> sin efectos laterales.</summary>
        public EscenaActual ConstruirEscenaActualApi(Observacion? observacion = null)
        {
            var obs = observacion is null ? this.UltimaObservacion : NormalizarObservacion(observacion);
            if (obs is null)
            {
                return new EscenaActual
                {
                    Epoca = this.Epoca,
                    Personaje = this.Perfil.Id,
                    ObservacionDisponible = false,
                    Escena = string.Empty,
                };
            }

            var escena = this.PrevisualizarEscena(obs);
            return new EscenaActual
            {
                Epoca = this.Epoca,
                Personaje = this.Perfil.Id,
                ObservacionDisponible = true,
                TiempoJuego = new TiempoJuego { Dia = escena.Apendice.Dia, Hora = escena.Apendice.Hora },
                Ubicacion = obs.Ubicacion,
                Escena = escena.Escena,
                Hechos = escena.ConstruirHechosPulso(),
            };
        }

        /// <summary>
        /// Fuerza un pulso manual (<c>cp_pulso</c>) respetando la contabilidad del presupuesto ordinario.
        /// Si el saldo ordinario está agotado, devuelve un resultado rechazado sin consumir ni emitir pulso.
        /// </summary>
        public ResultadoPulsoForzado ForzarPulso(
            Observacion observacion,
            long ahoraMs,
            string motivo = "manual",
            EstadoAtencion? estadoAtencion = null,
            IReadOnlyDictionary<string, double>? pesosIntencion = null,
            string? intencionActiva = null,
            string? obraEnCurso = null,
            HorarioDia? horario = null,
            AgendaDia? agendaDia = null)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            var obs = NormalizarObservacion(observacion);
            this.UltimaObservacion = obs;
            this.SincronizarAgenda(obs.Dia, horario, agendaDia);

            var disparo = this.Disparadores.ForzarPulso(obs, ahoraMs, motivo);
            this.Estado.Contadores = this.Disparadores.Contadores;

            if (disparo is null)
            {
                return ResultadoPulsoForzado.Rechazado(
                    "Presupuesto ordinario de pulsos agotado para el día actual.");
            }

            var contextoSaliencia = new ContextoSaliencia(
                this.Perfil.Rasgos,
                estadoAtencion ?? EstadoAtencion.Abierta,
                this.Perfil.Umbrales.Periferia,
                pesosIntencion);
            var saliencias = Saliencia.Calcular(obs, contextoSaliencia, this.Novedad);
            var bandas = new ClasificadorBandas(this.Perfil.Umbrales).Clasificar(saliencias);

            var (pulso, escena) = this.EmitirPulsoDesdeDisparo(
                obs,
                disparo,
                bandas,
                horario,
                agendaDia,
                estadoAtencion,
                pesosIntencion,
                intencionActiva,
                obraEnCurso,
                origenTelemetria: disparo.Detalle ?? "manual");

            this.Estado.Contadores = this.Disparadores.Contadores;
            return ResultadoPulsoForzado.Exito(pulso, escena);
        }

        /// <summary>
        /// Cierra el día si el personaje aún no emitió su cadena de acostarse (p. ej. <c>DayEnding</c>).
        /// </summary>
        public Pulso? ProcesarFinDeDia(
            Observacion observacion,
            long ahoraMs,
            EstadoAtencion? estadoAtencion = null,
            IReadOnlyDictionary<string, double>? pesosIntencion = null,
            string? intencionActiva = null,
            string? obraEnCurso = null,
            HorarioDia? horario = null,
            AgendaDia? agendaDia = null)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            var obs = NormalizarObservacion(observacion);
            this.UltimaObservacion = obs;
            this.SincronizarAgenda(obs.Dia, horario, agendaDia);

            var disparo = this.Disparadores.FinDeDia(obs, ahoraMs);
            this.Estado.Contadores = this.Disparadores.Contadores;
            if (disparo is null)
            {
                return null;
            }

            var contextoSaliencia = new ContextoSaliencia(
                this.Perfil.Rasgos,
                estadoAtencion ?? EstadoAtencion.Abierta,
                this.Perfil.Umbrales.Periferia,
                pesosIntencion);
            var saliencias = Saliencia.Calcular(obs, contextoSaliencia, this.Novedad);
            var bandas = new ClasificadorBandas(this.Perfil.Umbrales).Clasificar(saliencias);

            var (pulso, _) = this.EmitirPulsoDesdeDisparo(
                obs,
                disparo,
                bandas,
                horario,
                agendaDia,
                estadoAtencion,
                pesosIntencion,
                intencionActiva,
                obraEnCurso,
                origenTelemetria: "fin_de_dia");

            this.Estado.Contadores = this.Disparadores.Contadores;
            return pulso;
        }

        /// <summary>Registra en el búfer (y pendiente de diario) una acción ejecutada por el propio personaje.</summary>
        public bool RegistrarAccion(PasoPlan paso, int dia, int hora, double saliencia = 0.8, IReadOnlyDictionary<string, string>? nombresEntidades = null)
        {
            if (paso is null)
            {
                throw new ArgumentNullException(nameof(paso));
            }

            var diaValido = Math.Max(1, dia);
            var horaValida = Math.Clamp(hora, HoraJuego.InicioDia, HoraJuego.FinDia);
            var registrada = this.Bufer.RegistrarAccion(paso, diaValido, horaValida, this.Plantillas, saliencia, this.LugaresOfrecidos, nombresEntidades);
            if (registrada)
            {
                this.pendientesDiario.RegistrarAccion(paso, diaValido, horaValida, this.Plantillas, saliencia, this.LugaresOfrecidos, nombresEntidades);
            }

            return registrada;
        }

        /// <summary>
        /// Valida atómicamente una respuesta de la mente contra <paramref name="pulso"/>, el perfil y el
        /// estado vigente (origen de diario, intención, cambios de rutina, actos y memoria) y, solo si todo
        /// es válido, aplica los cambios en el orden intención/agenda → actos → memoria (ADS §4.1, CP-213).
        /// </summary>
        public Resultado AplicarRespuesta(Pulso pulso, Respuesta respuesta)
        {
            if (pulso is null)
            {
                throw new ArgumentNullException(nameof(pulso));
            }

            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            if (!this.MenteHabilitada)
            {
                var rechazoMenteOff = Resultado.Rechazar(new MotivoRechazo(
                    "mente_deshabilitada",
                    "respuesta",
                    "la mente está deshabilitada y el personaje sigue su rutina canónica"));
                this.telemetria?.RegistrarRechazo(this.Perfil.Id, respuesta.BasadaEn, rechazoMenteOff, pulso.Epoca);
                return rechazoMenteOff;
            }

            var ahoraRespuestaMs = this.relojMotorMs?.Invoke();
            if (ahoraRespuestaMs is long msActual)
            {
                this.ultimoContactoMenteMs = msActual;
            }

            this.pulsoPendienteDesdeMs = null;
            this.menteCaidaReportada = false;

            var motivos = new List<MotivoRechazo>();
            var errorOrigen = ValidadorOrigenDiario.Comprobar(pulso, respuesta);
            if (errorOrigen is not null)
            {
                motivos.Add(errorOrigen);
            }

            var errorMemoria = AplicadorTurnoDialogo.PrevalidarMemoria(respuesta.Memoria);
            if (errorMemoria is not null)
            {
                motivos.Add(errorMemoria);
            }

            if (respuesta.Humor is not null)
            {
                var errorHumor = GestorHumor.ValidarPropuesta(pulso, respuesta, this.Estado);
                if (errorHumor is not null)
                {
                    motivos.Add(errorHumor);
                }
            }

            var obsVigente = this.UltimaObservacion ?? CrearObservacionDesdePulso(pulso);
            var agendaValidacion = this.AgendaActual ?? AgendaDia.DesdeHorario(null, this.Perfil);
            var cambios = respuesta.Intencion?.CambiosRutina;

            IntencionPersonaje? intencionValidada = null;
            if (respuesta.Intencion is not null)
            {
                var idIntencion = $"int-{(respuesta.BasadaEn > 0 ? respuesta.BasadaEn : 1)}";
                long? basadaEn = respuesta.BasadaEn > 0 ? respuesta.BasadaEn : null;
                var obrasConocidas = this.Estado.Portafolio
                    .Select(o => o.ObraId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();

                var (creada, resIntencion) = ValidadorIntencion.ValidarYCrear(
                    idIntencion,
                    respuesta.Intencion,
                    obsVigente,
                    this.Perfil,
                    this.LugaresOfrecidos,
                    agendaValidacion,
                    this.ContextoRutina,
                    obrasConocidas: obrasConocidas,
                    basadaEn: basadaEn);

                if (!resIntencion.EsValido)
                {
                    motivos.AddRange(resIntencion.Motivos);
                }
                else
                {
                    intencionValidada = creada;
                }
            }

            if (pulso.Hito?.Id == HitosRutina.Despertar && respuesta.Agenda is not null)
            {
                var planesPermitidos = this.RecopilarPlanesPermitidos(pulso, intencionValidada);
                motivos.AddRange(ValidadorCambiosRutina.ValidarAgendaDespertar(respuesta.Agenda, agendaValidacion, planesPermitidos));
                ValidarEstadosAlteradosEnAgendaDespertar(respuesta.Agenda, agendaValidacion, cambios, motivos);
            }

            if (respuesta.Actos is { Count: > 0 })
            {
                var contextoActos = new ContextoValidacionActos(
                    obsVigente,
                    this.Perfil,
                    this.LugaresOfrecidos,
                    this.Estado,
                    InterlocutorRef: pulso.Interlocutor?.Ref,
                    Pulso: pulso,
                    Agenda: agendaValidacion,
                    ContextoRutina: this.ContextoRutina);
                var resActos = ValidadorActos.Validar(respuesta.Actos, contextoActos);
                if (!resActos.EsValido)
                {
                    motivos.AddRange(resActos.Motivos);
                }
            }

            var resMenciones = ValidadorMenciones.Validar(pulso, respuesta, this.Estado, this.Perfil, obsVigente);
            var diaMenciones = pulso.TiempoJuego?.Dia is > 0 ? pulso.TiempoJuego.Dia : obsVigente.Dia;
            var estadisticaDia = this.rastreadorMenciones.Registrar(this.Perfil.Id, diaMenciones, resMenciones);
            this.telemetria?.RegistrarTasaMenciones(
                this.Perfil.Id,
                diaMenciones,
                estadisticaDia.MencionesTotales,
                estadisticaDia.MencionesNoAncladas,
                estadisticaDia.TasaNoAncladas,
                resMenciones.MencionesNoAncladas,
                pulso.Epoca,
                this.modoValidacionMenciones);

            if (!resMenciones.EsValido
                && string.Equals(this.modoValidacionMenciones, CorazonConfig.ValidacionMencionesRechazar, StringComparison.Ordinal))
            {
                motivos.AddRange(resMenciones.Motivos);
            }

            if (motivos.Count > 0)
            {
                var rechazo = new Resultado { Estado = Resultado.Rechazada, Motivos = motivos };
                this.telemetria?.RegistrarRechazo(this.Perfil.Id, respuesta.BasadaEn, rechazo, pulso.Epoca);
                return rechazo;
            }

            // Aplicación atómica en orden ADS §4.1: intención/agenda → actos → memoria.
            if (this.AgendaActual is not null && cambios is { Count: > 0 })
            {
                ValidadorCambiosRutina.ValidarYAplicar(cambios, this.AgendaActual, this.Perfil, this.ContextoRutina);
            }

            if (intencionValidada is not null)
            {
                if (this.IntencionActivaVigente is { EstaActiva: true } anterior)
                {
                    anterior.Sustituir();
                }

                intencionValidada.Activar();
                this.IntencionActivaVigente = intencionValidada;
                this.Estado.IntencionActiva = intencionValidada.AIntencionActivaContrato();

                if (this.EjecutorCuerpo is not null && intencionValidada.Plan.Count > 0)
                {
                    var ahoraMs = this.relojMotorMs?.Invoke() ?? 0;
                    this.Disparadores.IniciarPlanIntencion(intencionValidada.Id, ahoraMs);
                    this.EjecutorCuerpo.EjecutarPlan(intencionValidada.Plan, ahoraMs, intencionValidada.Id);
                }
            }

            if (respuesta.Actos is { Count: > 0 })
            {
                var compromisosCreados = new List<CompromisoEstado>();
                var obrasRegaladas = new List<ObraPortafolio>();
                foreach (var acto in respuesta.Actos)
                {
                    AplicadorTurnoDialogo.AplicarActo(acto, this.Estado, compromisosCreados, obrasRegaladas);
                    this.telemetria?.RegistrarActo(this.Perfil.Id, acto, obsVigente.Dia, obsVigente.Hora, aceptado: true);
                }
            }

            if (respuesta.Memoria is not null)
            {
                var resMemoria = Consolidacion.AplicarMemoria(this.Estado, pulso, respuesta);
                if (!resMemoria.Aceptada && resMemoria.MotivoRechazo is not null)
                {
                    var rechazoMem = Resultado.Rechazar(resMemoria.MotivoRechazo);
                    this.telemetria?.RegistrarRechazo(this.Perfil.Id, respuesta.BasadaEn, rechazoMem, pulso.Epoca);
                    return rechazoMem;
                }
            }

            if (respuesta.Humor is not null)
            {
                var resHumor = GestorHumor.AplicarPropuesta(this.Estado, pulso, respuesta, this.Perfil.ObtenerConfigHumor());
                if (!resHumor.Aceptada && resHumor.MotivoRechazo is not null)
                {
                    var rechazoHumor = Resultado.Rechazar(resHumor.MotivoRechazo);
                    this.telemetria?.RegistrarRechazo(this.Perfil.Id, respuesta.BasadaEn, rechazoHumor, pulso.Epoca);
                    return rechazoHumor;
                }
            }

            this.telemetria?.RegistrarRespuesta(respuesta, this.Perfil.Id);
            return new Resultado { Estado = Resultado.Aceptada };
        }

        /// <summary>Proyecta la instantánea de memoria para la API aplicando la atenuación vigente del humor.</summary>
        public InstantaneaMemoria ConstruirInstantaneaMemoria()
        {
            TiempoJuego? ahora = this.UltimaObservacion is { } obs
                ? new TiempoJuego
                {
                    Dia = Math.Max(1, obs.Dia),
                    Hora = Math.Clamp(obs.Hora, HoraJuego.InicioDia, HoraJuego.FinDia),
                }
                : null;

            return Consolidacion.ProyectarInstantaneaMemoria(
                this.Estado,
                this.Perfil.Id,
                this.Epoca,
                ahora,
                this.Perfil.ObtenerConfigHumor());
        }

        private (Pulso Pulso, EscenaGenerada Escena) EmitirPulsoDesdeDisparo(
            Observacion obs,
            Disparo disparo,
            IReadOnlyList<EntidadEnBanda> bandas,
            HorarioDia? horario,
            AgendaDia? agendaDia,
            EstadoAtencion? estadoAtencion,
            IReadOnlyDictionary<string, double>? pesosIntencion,
            string? intencionActiva,
            string? obraEnCurso,
            string origenTelemetria)
        {
            var diaPulso = Math.Max(1, disparo.Dia);
            var horaPulso = Math.Clamp(disparo.Hora, HoraJuego.InicioDia, HoraJuego.FinDia);

            var contextoEscena = this.CrearContextoEscena(estadoAtencion, pesosIntencion, intencionActiva, obraEnCurso);
            var escena = GeneradorEscena.Generar(obs, bandas, this.LugaresOfrecidos, this.Plantillas, contextoEscena);
            this.UltimaEscena = escena;

            // Volcar entradas nuevas del búfer al diario sin duplicar entradas retenidas entre pulsos.
            var creados = Consolidacion.RegistrarDesdeBufer(this.Estado, this.pendientesDiario);
            this.pendientesDiario.Limpiar();
            if (creados.Count == 0 && !string.IsNullOrWhiteSpace(escena.Escena))
            {
                var entidadesMencionadas = bandas
                    .Where(b => b.Banda >= Banda.Mencion)
                    .Select(b => b.Ref)
                    .ToList();
                var salienciaEpisodio = bandas
                    .Where(b => b.Banda >= Banda.Mencion)
                    .Select(b => b.Saliencia)
                    .DefaultIfEmpty(0.5)
                    .Max();
                this.Estado.AgregarEpisodio(diaPulso, horaPulso, escena.Escena, salienciaEpisodio, entidadesMencionadas);
            }

            var motivoPulso = NormalizarMotivoPulso(disparo);
            var pulso = escena.CrearPulsoBase(id: 0, this.Epoca, this.Perfil.Id, motivoPulso);
            pulso.TiempoJuego = new TiempoJuego { Dia = diaPulso, Hora = horaPulso };
            pulso.TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O");
            pulso.Bufer = this.Bufer.Consolidar(diaPulso, horaPulso).ToList();
            pulso.Huella = this.Huella.EmitirPulso(diaPulso, this.Plantillas);
            pulso.Agenda = this.ConstruirAgenda(disparo, horario, agendaDia);

            if (!string.IsNullOrWhiteSpace(intencionActiva))
            {
                pulso.IntencionActiva = new IntencionActiva { Id = intencionActiva, Objetivo = intencionActiva };
            }

            var interlocutorRef = motivoPulso == Motivos.Interlocucion
                ? (string.IsNullOrWhiteSpace(disparo.Ref) ? "farmer:0" : disparo.Ref)
                : null;
            var compromisosActivos = GestorCompromisos.ProyectarParaPulso(
                this.Estado,
                obs.Entidades.Select(e => e.Ref),
                interlocutorRef,
                diaPulso);
            if (compromisosActivos.Count > 0)
            {
                pulso.CompromisosProximos = compromisosActivos;
            }

            var humorVigente = GestorHumor.ObtenerVigente(this.Estado, pulso.TiempoJuego, this.Perfil.ObtenerConfigHumor());
            if (humorVigente is not null)
            {
                pulso.Humor = humorVigente;
            }

            if (motivoPulso == Motivos.Hito)
            {
                var hito = disparo.Hito ?? HitosRutina.DelMotor(HitosRutina.Despertar, this.Perfil.Rutina);
                pulso.Hito = new HitoPulso
                {
                    Id = hito.Id,
                    Origen = hito.Origen,
                    Forma = hito.Forma,
                };

                if (hito.Id == HitosRutina.Acostarse)
                {
                    Consolidacion.AplicarEpisodiosAlPulso(pulso, this.Estado, diaPulso);
                    Consolidacion.Consolidar(this.Estado, diaPulso, this.tamanoMaxMemoriaKB);
                }
                else if (hito.Id == HitosRutina.Despertar)
                {
                    var recuerdos = this.Estado.Recuerdos
                        .Select(r => r.Texto)
                        .Where(t => !string.IsNullOrWhiteSpace(t))
                        .ToList();
                    if (recuerdos.Count > 0)
                    {
                        pulso.Recuerdos = recuerdos;
                    }
                }
            }
            else if (motivoPulso == Motivos.Interlocucion)
            {
                pulso.Interlocutor = new Interlocutor
                {
                    Ref = string.IsNullOrWhiteSpace(disparo.Ref) ? "farmer:0" : disparo.Ref,
                    Texto = disparo.Detalle ?? string.Empty,
                };
                pulso.CaducaEnMs = this.tiempoMaxRespuestaMs;
            }

            if (this.publicarPulso is not null)
            {
                pulso = this.publicarPulso(pulso);
            }
            else
            {
                pulso.Id = this.siguientePulsoId++;
            }

            this.telemetria?.RegistrarEscena(
                this.Perfil.Id,
                escena,
                pulso.Epoca,
                obs.Ubicacion,
                origen: origenTelemetria,
                pulsoId: pulso.Id);
            this.telemetria?.RegistrarPulso(pulso);

            return (pulso, escena);
        }

        private void AlimentarBufer(Observacion obs, IReadOnlyList<EntidadEnBanda> bandas)
        {
            if (bandas.Count == 0 || obs.Entidades.Count == 0)
            {
                this.bandasRegistradasBufer.Clear();
                return;
            }

            var observadas = obs.Entidades.ToDictionary(e => e.Ref, StringComparer.Ordinal);
            var activas = new HashSet<string>(StringComparer.Ordinal);

            foreach (var enBanda in bandas)
            {
                if (enBanda.Banda < Banda.Mencion || !observadas.TryGetValue(enBanda.Ref, out var entidad))
                {
                    continue;
                }

                activas.Add(enBanda.Ref);
                if (this.bandasRegistradasBufer.TryGetValue(enBanda.Ref, out var previa) && enBanda.Banda <= previa)
                {
                    continue;
                }

                var prosa = GeneradorAccion.GenerarPercepcion(entidad, enBanda.Banda, obs.Dia, this.Plantillas);
                if (string.IsNullOrWhiteSpace(prosa))
                {
                    continue;
                }

                if (this.Bufer.RegistrarPercepcion(obs.Dia, obs.Hora, enBanda.Banda, enBanda.Saliencia, prosa, entidad.Ref))
                {
                    this.pendientesDiario.RegistrarPercepcion(obs.Dia, obs.Hora, enBanda.Banda, enBanda.Saliencia, prosa, entidad.Ref);
                    this.bandasRegistradasBufer[enBanda.Ref] = enBanda.Banda;
                }
            }

            foreach (var ausente in this.bandasRegistradasBufer.Keys.Where(k => !activas.Contains(k)).ToList())
            {
                this.bandasRegistradasBufer.Remove(ausente);
            }
        }

        private void SincronizarAgenda(int dia, HorarioDia? horario, AgendaDia? agendaExplicita)
        {
            if (this.ultimoDiaAgenda is int diaAnterior && diaAnterior != dia)
            {
                this.ContextoRutina.AvanzarDia(dia);
                if (agendaExplicita is null)
                {
                    this.AgendaActual = null;
                }
            }

            this.ultimoDiaAgenda = dia;

            if (agendaExplicita is not null)
            {
                this.AgendaActual = agendaExplicita;
                return;
            }

            if (horario is not null && (this.AgendaActual is null || !string.Equals(this.AgendaActual.ClaveHorario, horario.Clave, StringComparison.Ordinal)))
            {
                this.AgendaActual = AgendaDia.DesdeHorario(horario, this.Perfil);
            }
        }

        private List<EntradaAgenda> ConstruirAgenda(Disparo disparo, HorarioDia? horario, AgendaDia? agendaDia)
        {
            if (agendaDia is not null)
            {
                return agendaDia.Proyectar();
            }

            if (disparo.Agenda is not null && disparo.Agenda.Count > 0)
            {
                var fuente = this.AgendaActual ?? (horario is not null ? AgendaDia.DesdeHorario(horario, this.Perfil) : null);
                var mapa = fuente?.Entradas.ToDictionary(e => e.Hito, e => e, StringComparer.Ordinal);

                return disparo.Agenda.Select(p =>
                {
                    if (mapa is not null && mapa.TryGetValue(p.Hito, out var entrada))
                    {
                        return entrada.AEntradaContrato();
                    }

                    return new EntradaAgenda
                    {
                        Hito = p.Hito,
                        Prosa = p.Hito,
                        Estado = EstadoHito.Pendiente,
                    };
                }).ToList();
            }

            if (this.AgendaActual is not null)
            {
                return this.AgendaActual.Proyectar();
            }

            if (horario is not null)
            {
                return AgendaDia.DesdeHorario(horario, this.Perfil).Proyectar();
            }

            return new List<EntradaAgenda>();
        }

        private HashSet<string> RecopilarPlanesPermitidos(Pulso pulso, IntencionPersonaje? intencion)
        {
            var permitidos = new HashSet<string>(StringComparer.Ordinal);
            if (intencion is not null && !string.IsNullOrWhiteSpace(intencion.Id))
            {
                permitidos.Add(intencion.Id);
            }

            if (!string.IsNullOrWhiteSpace(pulso.IntencionActiva?.Id))
            {
                permitidos.Add(pulso.IntencionActiva.Id);
            }

            if (pulso.CompromisosProximos is not null)
            {
                foreach (var c in pulso.CompromisosProximos)
                {
                    if (!string.IsNullOrWhiteSpace(c.Id))
                    {
                        permitidos.Add(c.Id);
                    }
                }
            }

            foreach (var c in this.Estado.Compromisos)
            {
                if (!string.IsNullOrWhiteSpace(c.Id))
                {
                    permitidos.Add(c.Id);
                }
            }

            return permitidos;
        }

        private static void ValidarEstadosAlteradosEnAgendaDespertar(
            IReadOnlyList<EstadoAgenda> agendaPropuesta,
            AgendaDia agendaDelDia,
            IReadOnlyList<CambioRutina>? cambios,
            List<MotivoRechazo> motivos)
        {
            var estadoPorCambio = (cambios ?? Array.Empty<CambioRutina>())
                .Where(c => !string.IsNullOrWhiteSpace(c.Hito))
                .GroupBy(c => c.Hito, StringComparer.Ordinal)
                .ToDictionary(
                    g => g.Key,
                    g => g.Last().Cambio switch
                    {
                        TiposCambioRutina.Saltar => EstadoHito.Saltado,
                        TiposCambioRutina.Mover => EstadoHito.Movido,
                        TiposCambioRutina.Sustituir => EstadoHito.Sustituido,
                        _ => EstadoHito.Pendiente,
                    },
                    StringComparer.Ordinal);

            for (var i = 0; i < agendaPropuesta.Count; i++)
            {
                var item = agendaPropuesta[i];
                if (item is null || string.IsNullOrWhiteSpace(item.Hito))
                {
                    continue;
                }

                var entradaDia = agendaDelDia.Buscar(item.Hito);
                if (entradaDia is null)
                {
                    continue;
                }

                if (item.Estado is EstadoHito.Saltado or EstadoHito.Movido or EstadoHito.Sustituido
                    && entradaDia.Estado != item.Estado
                    && (!estadoPorCambio.TryGetValue(item.Hito, out var esperado) || esperado != item.Estado))
                {
                    motivos.Add(new MotivoRechazo(
                        "cambio_no_autorizado",
                        $"agenda[{i}].estado",
                        $"no puedes marcar «{item.Hito}» como «{item.Estado}» sin un cambio de rutina válido"));
                }
            }
        }

        private static Observacion CrearObservacionDesdePulso(Pulso pulso)
        {
            var dia = Math.Max(1, pulso.TiempoJuego?.Dia ?? 1);
            var hora = Math.Clamp(pulso.TiempoJuego?.Hora ?? HoraJuego.InicioDia, HoraJuego.InicioDia, HoraJuego.FinDia);
            var entidades = new List<EntidadObservada>();
            var refs = new HashSet<string>(StringComparer.Ordinal);

            if (pulso.Hechos is not null
                && pulso.Hechos.TryGetValue("cerca", out var cercaElem)
                && cercaElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in cercaElem.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (!item.TryGetProperty("ref", out var refProp) || refProp.ValueKind != JsonValueKind.String)
                    {
                        continue;
                    }

                    var entRef = refProp.GetString();
                    if (string.IsNullOrWhiteSpace(entRef) || !refs.Add(entRef))
                    {
                        continue;
                    }

                    var nombre = item.TryGetProperty("nombre", out var nomProp) && nomProp.ValueKind == JsonValueKind.String
                        ? (nomProp.GetString() ?? entRef)
                        : entRef;
                    int? corazones = item.TryGetProperty("corazones", out var corProp) && corProp.TryGetInt32(out var corVal)
                        ? corVal
                        : null;
                    var categoria = entRef.StartsWith("farmer:", StringComparison.Ordinal)
                        ? CategoriaEntidad.Granjero
                        : entRef.StartsWith("npc:", StringComparison.Ordinal)
                            ? CategoriaEntidad.Npc
                            : CategoriaEntidad.Objeto;

                    entidades.Add(new EntidadObservada(entRef, categoria, nombre, string.Empty, 0, 0, Array.Empty<string>(), corazones));
                }
            }

            if (!string.IsNullOrWhiteSpace(pulso.Interlocutor?.Ref) && refs.Add(pulso.Interlocutor.Ref))
            {
                var interRef = pulso.Interlocutor.Ref;
                var categoria = interRef.StartsWith("farmer:", StringComparison.Ordinal)
                    ? CategoriaEntidad.Granjero
                    : CategoriaEntidad.Npc;
                entidades.Add(new EntidadObservada(interRef, categoria, interRef, string.Empty, 0, 0, Array.Empty<string>()));
            }

            return new Observacion(dia, hora, string.Empty, 0, 0, 1.0, "despejado", entidades);
        }

        private ContextoEscena CrearContextoEscena(
            EstadoAtencion? estadoAtencion,
            IReadOnlyDictionary<string, double>? pesosIntencion,
            string? intencionActiva,
            string? obraEnCurso)
            => new()
            {
                Estado = estadoAtencion ?? EstadoAtencion.Abierta,
                AccionesDisponibles = this.Perfil.Acciones,
                ObraEnCurso = obraEnCurso,
                IntencionActiva = intencionActiva,
                PesosIntencion = pesosIntencion,
            };

        private static string NormalizarMotivoPulso(Disparo disparo) => disparo.Motivo switch
        {
            Motivos.Pico or Motivos.Interlocucion or Motivos.Deriva or Motivos.Hito
                or Motivos.FinIntencion or Motivos.Compromiso or Motivos.Transicion => disparo.Motivo,
            _ => Motivos.Deriva,
        };

        private static Observacion NormalizarObservacion(Observacion observacion)
        {
            var dia = Math.Max(1, observacion.Dia);
            var hora = Math.Clamp(observacion.Hora, HoraJuego.InicioDia, HoraJuego.FinDia);
            return dia == observacion.Dia && hora == observacion.Hora
                ? observacion
                : observacion with { Dia = dia, Hora = hora };
        }
    }
}
