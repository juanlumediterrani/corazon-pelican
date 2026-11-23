using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>Estado de ciclo de vida de una acción individual del cuerpo (CP-302).</summary>
    public enum EstadoEjecucionAccion
    {
        Pendiente,
        EnCurso,
        Cumplida,
        Bloqueada,
    }

    /// <summary>Estado observable del controlador de desplazamiento nativo o simulado.</summary>
    public enum EstadoRutaCuerpo
    {
        Inactiva,
        EnCurso,
        Completada,
        Bloqueada,
    }

    /// <summary>Estado del seguimiento espacial al acercarse al jugador para iniciar conversación (CP-613).</summary>
    public enum EstadoAcercamientoJugador
    {
        Inactivo,
        EnCurso,
        EnRango,
        CanceladoFueraDeAlcance,
    }

    /// <summary>Tipo de suceso emitido por el ejecutor del cuerpo durante un plan.</summary>
    public enum TipoSucesoCuerpo
    {
        AccionIniciada,
        AccionTerminada,
        AccionBloqueada,
        PlanTerminado,
    }

    /// <summary>Suceso registrado por el cuerpo al iniciar, completar o bloquear una acción o plan.</summary>
    public sealed record SucesoCuerpoEjecucion(
        long MilisegundosReales,
        TipoSucesoCuerpo Tipo,
        string Accion,
        string? Detalle = null);

    /// <summary>
    /// Abstracción pura de los medios físicos del cuerpo (locomoción, postura, mirada, dibujo, gestos,
    /// murmullo y retorno al horario). La implementa el adaptador SMAPI en vivo (<c>CuerpoNpc</c>)
    /// y el cuerpo simulado en <c>FakeWorld</c>.
    /// </summary>
    public interface IMediosCuerpo
    {
        string UbicacionActual { get; }

        int CasillaX { get; }

        int CasillaY { get; }

        /// <summary>Indica si el personaje está reclamado fuera de su horario nativo (<c>CorazonPelican.Control</c>, CP-303).</summary>
        bool EstaReclamado { get; }

        /// <summary>Indica si el personaje está reproduciendo una animación de actividad (p. ej. dibujo).</summary>
        bool EstaEnAnimacionActividad { get; }

        /// <summary>Estado actual del desplazamiento en curso.</summary>
        EstadoRutaCuerpo EstadoRutaActual { get; }

        /// <summary>Motivo de bloqueo de la ruta si <see cref="EstadoRutaActual"/> es <see cref="EstadoRutaCuerpo.Bloqueada"/>.</summary>
        string? MotivoBloqueoRuta { get; }

        /// <summary>Duración de asentamiento al sentarse en milisegundos reales (0 si es instantáneo al llegar).</summary>
        long DuracionAsentamientoSentarseMs { get; }

        void ReclamarControl(string motivo);

        bool IniciarRutaHacia(Lugar destino, int orientacionFinal);

        void DetenerMovimiento();

        bool AdoptarPosturaSentada(Lugar destino, int orientacion);

        bool IntentarMirarObjetivo(string objetivoRef);

        bool PuedeDibujar(out string? motivoBloqueo);

        bool IniciarDibujo(string? obraId);

        void DetenerAnimacionActividad();

        bool HacerGesto(string tipo);

        bool Murmurar(string texto);

        bool VolverARutina();

        bool AplicarCambioHorario(EntradaAgendaDia entrada) => true;

        void RestaurarHorarioCanonico() { }

        /// <summary>
        /// Libera cualquier reclamación y controlador residual para permitir una reubicación técnica inmediata
        /// (p. ej. salto horario administrativo off-role de Pelican) sin iniciar una ruta hacia el tramo previo (CP-611).
        /// </summary>
        void LiberarControlParaReubicacion()
        {
            this.DetenerAnimacionActividad();
            this.DetenerMovimiento();
            this.RestaurarHorarioCanonico();
        }

        /// <summary>Inicia el desplazamiento hacia un jugador objetivo en el mismo mapa para entregar diálogo (CP-613).</summary>
        bool IniciarAcercamientoAJugador(string objetivoRef) => false;

        /// <summary>Evalúa si el personaje alcanzó el rango de conversación o debe abortar el acercamiento (CP-613).</summary>
        EstadoAcercamientoJugador EvaluarAcercamientoAJugador(string objetivoRef, int maxDistanciaCasillas)
            => EstadoAcercamientoJugador.CanceladoFueraDeAlcance;
    }

    /// <summary>
    /// Contexto compartido por las acciones de un plan durante su ejecución (estado de atención,
    /// foco del sensorio, postura y acceso a los medios del cuerpo y al léxico de lugares).
    /// </summary>
    public sealed class ContextoEjecucionAccion
    {
        public ContextoEjecucionAccion(
            IMediosCuerpo medios,
            LugaresOfrecidos lugares,
            ModoReloj modoReloj = ModoReloj.Vanilla)
        {
            this.Medios = medios ?? throw new ArgumentNullException(nameof(medios));
            this.Lugares = lugares ?? throw new ArgumentNullException(nameof(lugares));
            this.ModoReloj = modoReloj;
        }

        public IMediosCuerpo Medios { get; }

        public LugaresOfrecidos Lugares { get; }

        public ModoReloj ModoReloj { get; }

        public EstadoAtencion EstadoAtencion { get; set; } = EstadoAtencion.Abierta;

        /// <summary>Referencia de entidad enfocada por <c>mirar</c>, o <c>null</c> tras <c>levantar_la_vista</c> (GSS §3.6).</summary>
        public string? FocoObjetivoRef { get; set; }

        /// <summary>Acción continua activa (p. ej. <c>"dibujar"</c>), o <c>null</c> si no hay ninguna.</summary>
        public string? AccionContinua { get; set; }

        /// <summary>Identificador de obra activa durante <c>dibujar</c>, o <c>null</c>.</summary>
        public string? ObraEnCurso { get; set; }

        /// <summary>Indica si el personaje adoptó la postura de sentado en el lugar actual.</summary>
        public bool Sentada { get; set; }
    }

    /// <summary>
    /// Clase base para cada acción del vocabulario v1 (CP-302): gestiona inicio, avance por tick,
    /// terminación, causa de bloqueo y guarda de duración máxima para que ninguna acción quede colgada.
    /// </summary>
    public abstract class AccionCuerpo
    {
        protected AccionCuerpo(PasoPlan paso)
        {
            this.Paso = paso ?? throw new ArgumentNullException(nameof(paso));
        }

        public PasoPlan Paso { get; }

        public abstract string Nombre { get; }

        public EstadoEjecucionAccion Estado { get; private set; } = EstadoEjecucionAccion.Pendiente;

        public string? MotivoBloqueo { get; private set; }

        public bool EsContinua { get; protected set; }

        public long InicioMs { get; private set; }

        public abstract long DuracionMaximaMs(ContextoEjecucionAccion contexto);

        public void Iniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            this.InicioMs = ahoraMs;
            this.Estado = EstadoEjecucionAccion.EnCurso;
            this.AlIniciar(contexto, ahoraMs);
        }

        public void AvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            if (this.Estado != EstadoEjecucionAccion.EnCurso)
            {
                return;
            }

            var transcurrido = Math.Max(0, ahoraMs - this.InicioMs);
            var tope = this.DuracionMaximaMs(contexto);
            if (tope >= 0 && transcurrido > tope)
            {
                contexto.Medios.DetenerMovimiento();
                this.Bloquear($"tiempo_agotado:{this.Nombre} ({transcurrido} ms > {tope} ms)");
                return;
            }

            this.AlAvanzarTick(contexto, ahoraMs);
        }

        public static AccionCuerpo Crear(PasoPlan paso)
        {
            if (paso is null)
            {
                throw new ArgumentNullException(nameof(paso));
            }

            return paso.Accion switch
            {
                "ir_a" => new AccionIrA(paso),
                "sentarse" => new AccionSentarse(paso),
                "mirar" => new AccionMirar(paso),
                "levantar_la_vista" => new AccionLevantarLaVista(paso),
                "dibujar" => new AccionDibujar(paso),
                "guardar_cuaderno" => new AccionGuardarCuaderno(paso),
                "gesto" => new AccionGesto(paso),
                "murmurar" => new AccionMurmurar(paso),
                "esperar" => new AccionEsperar(paso),
                "volver_a_rutina" => new AccionVolverARutina(paso),
                "acercarse_a" => new AccionAcercarseA(paso),
                _ => new AccionDesconocida(paso),
            };
        }

        protected abstract void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs);

        protected abstract void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs);

        protected void Cumplir()
        {
            this.Estado = EstadoEjecucionAccion.Cumplida;
            this.MotivoBloqueo = null;
        }

        protected void Bloquear(string motivo)
        {
            this.Estado = EstadoEjecucionAccion.Bloqueada;
            this.MotivoBloqueo = string.IsNullOrWhiteSpace(motivo) ? "bloqueada" : motivo;
        }
    }

    /// <summary>Acción <c>ir_a(lugar)</c>: camina por rutas nativas a un lugar del léxico (GSS §5.2).</summary>
    public sealed class AccionIrA : AccionCuerpo
    {
        public const long MaxDuracionRutaMs = 180_000;
        public const long MaxEsperaSalidaAnimacionMs = 2_000;
        public const int OrientacionPorDefecto = 2;

        private Lugar? destino;
        private bool esperandoSalidaAnimacion;
        private bool rutaIniciada;

        public AccionIrA(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "ir_a";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionRutaMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            this.destino = contexto.Lugares.DestinoDe(this.Paso);
            if (this.destino is null)
            {
                this.Bloquear($"lugar desconocido: {this.Paso.Lugar}");
                return;
            }

            if (PerfilValidator.AreaPendiente(this.destino))
            {
                this.Bloquear($"área pendiente: {this.Paso.Lugar}");
                return;
            }

            contexto.Medios.ReclamarControl("ir_a");
            contexto.AccionContinua = null;
            contexto.Sentada = false;

            if (contexto.Medios.EstaEnAnimacionActividad)
            {
                contexto.Medios.DetenerAnimacionActividad();
                this.esperandoSalidaAnimacion = true;
                return;
            }

            this.LanzarRuta(contexto);
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (this.esperandoSalidaAnimacion)
            {
                if (contexto.Medios.EstaEnAnimacionActividad && ahoraMs - this.InicioMs < MaxEsperaSalidaAnimacionMs)
                {
                    return;
                }

                this.esperandoSalidaAnimacion = false;
                this.LanzarRuta(contexto);
                return;
            }

            if (!this.rutaIniciada)
            {
                return;
            }

            switch (contexto.Medios.EstadoRutaActual)
            {
                case EstadoRutaCuerpo.Completada:
                    this.Cumplir();
                    break;
                case EstadoRutaCuerpo.Bloqueada:
                    this.Bloquear(contexto.Medios.MotivoBloqueoRuta ?? $"ruta bloqueada hacia {this.Paso.Lugar}");
                    break;
            }
        }

        private void LanzarRuta(ContextoEjecucionAccion contexto)
        {
            if (this.destino is null)
            {
                return;
            }

            if (!contexto.Medios.IniciarRutaHacia(this.destino, OrientacionPorDefecto))
            {
                this.Bloquear(contexto.Medios.MotivoBloqueoRuta ?? $"ruta inaccesible hacia {this.Paso.Lugar}");
                return;
            }

            this.rutaIniciada = true;
            if (contexto.Medios.EstadoRutaActual == EstadoRutaCuerpo.Completada)
            {
                this.Cumplir();
            }
        }
    }

    /// <summary>
    /// Acción <c>sentarse(lugar)</c>: se desplaza al punto sentable del lugar si aún no está en él,
    /// detiene la marcha y adopta la orientación del lugar (fallback v1 según CP-012).
    /// </summary>
    public sealed class AccionSentarse : AccionCuerpo
    {
        public const long MaxDuracionSentarseMs = 180_000;
        public const int OrientacionSentarse = 2;

        private Lugar? destino;
        private bool esperandoSalidaAnimacion;
        private bool enDesplazamiento;
        private bool asentando;
        private long finAsentamientoMs;

        public AccionSentarse(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "sentarse";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionSentarseMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (!string.IsNullOrWhiteSpace(this.Paso.Lugar))
            {
                this.destino = contexto.Lugares.DestinoDe(this.Paso);
                if (this.destino is null)
                {
                    this.Bloquear($"lugar desconocido: {this.Paso.Lugar}");
                    return;
                }

                if (PerfilValidator.AreaPendiente(this.destino))
                {
                    this.Bloquear($"área pendiente: {this.Paso.Lugar}");
                    return;
                }

                if (!this.destino.Sentable)
                {
                    this.Bloquear($"lugar no sentable: {this.Paso.Lugar}");
                    return;
                }
            }

            contexto.Medios.ReclamarControl("sentarse");
            contexto.AccionContinua = null;

            if (contexto.Medios.EstaEnAnimacionActividad)
            {
                contexto.Medios.DetenerAnimacionActividad();
                this.esperandoSalidaAnimacion = true;
                return;
            }

            this.IniciarDesplazamientoOAsentamiento(contexto, ahoraMs);
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (this.esperandoSalidaAnimacion)
            {
                if (contexto.Medios.EstaEnAnimacionActividad)
                {
                    return;
                }

                this.esperandoSalidaAnimacion = false;
                this.IniciarDesplazamientoOAsentamiento(contexto, ahoraMs);
                return;
            }

            if (this.enDesplazamiento)
            {
                switch (contexto.Medios.EstadoRutaActual)
                {
                    case EstadoRutaCuerpo.Completada:
                        this.enDesplazamiento = false;
                        this.IniciarAsentamiento(contexto, ahoraMs);
                        break;
                    case EstadoRutaCuerpo.Bloqueada:
                        this.Bloquear(contexto.Medios.MotivoBloqueoRuta ?? $"ruta bloqueada al sentarse en {this.Paso.Lugar}");
                        break;
                }

                return;
            }

            if (this.asentando && ahoraMs >= this.finAsentamientoMs)
            {
                contexto.Sentada = true;
                this.Cumplir();
            }
        }

        private void IniciarDesplazamientoOAsentamiento(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (this.destino is not null && !EstaDentroDeLugar(contexto.Medios, this.destino))
            {
                if (!contexto.Medios.IniciarRutaHacia(this.destino, OrientacionSentarse))
                {
                    this.Bloquear(contexto.Medios.MotivoBloqueoRuta ?? $"ruta inaccesible al sentarse en {this.Paso.Lugar}");
                    return;
                }

                if (contexto.Medios.EstadoRutaActual != EstadoRutaCuerpo.Completada)
                {
                    this.enDesplazamiento = true;
                    return;
                }
            }

            this.IniciarAsentamiento(contexto, ahoraMs);
        }

        private void IniciarAsentamiento(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            var lugarPostura = this.destino ?? new Lugar
            {
                Id = this.Paso.Lugar ?? "actual",
                Ubicacion = contexto.Medios.UbicacionActual,
                Area = new List<int> { contexto.Medios.CasillaX, contexto.Medios.CasillaY, 1, 1 },
                Sentable = true,
            };

            if (!contexto.Medios.AdoptarPosturaSentada(lugarPostura, OrientacionSentarse))
            {
                this.Bloquear($"no se pudo adoptar postura sentada en {lugarPostura.Id}");
                return;
            }

            var espera = Math.Max(0, contexto.Medios.DuracionAsentamientoSentarseMs);
            if (espera == 0)
            {
                contexto.Sentada = true;
                this.Cumplir();
                return;
            }

            this.asentando = true;
            this.finAsentamientoMs = ahoraMs + espera;
        }

        private static bool EstaDentroDeLugar(IMediosCuerpo medios, Lugar lugar)
        {
            if (!string.Equals(medios.UbicacionActual, lugar.Ubicacion, StringComparison.Ordinal) || lugar.Area.Count < 4)
            {
                return false;
            }

            var x0 = lugar.Area[0];
            var y0 = lugar.Area[1];
            var ancho = Math.Max(1, lugar.Area[2]);
            var alto = Math.Max(1, lugar.Area[3]);
            return medios.CasillaX >= x0 && medios.CasillaX < x0 + ancho
                && medios.CasillaY >= y0 && medios.CasillaY < y0 + alto;
        }
    }

    /// <summary>Acción <c>mirar(objetivo)</c>: orienta al personaje y cambia el foco del sensorio (GSS §3.6).</summary>
    public sealed class AccionMirar : AccionCuerpo
    {
        public const long MaxDuracionMirarMs = 2_000;

        public AccionMirar(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "mirar";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionMirarMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (string.IsNullOrWhiteSpace(this.Paso.Objetivo) || !Acciones.EsRefValida(this.Paso.Objetivo))
            {
                this.Bloquear($"objetivo inválido en mirar: {this.Paso.Objetivo}");
                return;
            }

            if (!contexto.Medios.IntentarMirarObjetivo(this.Paso.Objetivo))
            {
                this.Bloquear($"objetivo no encontrado para mirar: {this.Paso.Objetivo}");
                return;
            }

            contexto.FocoObjetivoRef = this.Paso.Objetivo;
            contexto.EstadoAtencion = EstadoAtencion.Abierta;
            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
        }
    }

    /// <summary>
    /// Acción <c>levantar_la_vista</c>: sale o pausa la concentración estrecha y restaura el foco
    /// abierto del sensorio (GSS §3.6, CP-012).
    /// </summary>
    public sealed class AccionLevantarLaVista : AccionCuerpo
    {
        public const long MaxDuracionLevantarVistaMs = 2_000;

        public AccionLevantarLaVista(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "levantar_la_vista";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionLevantarVistaMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            contexto.FocoObjetivoRef = null;
            contexto.EstadoAtencion = EstadoAtencion.Abierta;
            contexto.AccionContinua = null;

            if (contexto.Medios.EstaEnAnimacionActividad)
            {
                contexto.Medios.DetenerAnimacionActividad();
                if (contexto.Medios.EstaEnAnimacionActividad)
                {
                    return;
                }
            }

            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (!contexto.Medios.EstaEnAnimacionActividad)
            {
                this.Cumplir();
            }
        }
    }

    /// <summary>Acción <c>dibujar(obra)</c>: inicia la animación continua de dibujo y estrecha la atención (CP-012).</summary>
    public sealed class AccionDibujar : AccionCuerpo
    {
        public const long MaxDuracionArranqueDibujoMs = 2_000;

        public AccionDibujar(PasoPlan paso)
            : base(paso)
        {
            this.EsContinua = true;
        }

        public override string Nombre => "dibujar";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionArranqueDibujoMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            var obra = string.IsNullOrWhiteSpace(this.Paso.Obra) ? "nueva" : this.Paso.Obra;
            if (obra != "nueva" && !Acciones.EsIdObraValido(obra))
            {
                this.Bloquear($"identificador de obra inválido: {obra}");
                return;
            }

            if (!contexto.Medios.PuedeDibujar(out var motivo))
            {
                this.Bloquear(motivo ?? "no puede dibujar en el estado actual");
                return;
            }

            contexto.Medios.ReclamarControl("dibujar");
            if (!contexto.Medios.IniciarDibujo(obra))
            {
                this.Bloquear("no se pudo iniciar la animación de dibujo");
                return;
            }

            contexto.AccionContinua = "dibujar";
            contexto.ObraEnCurso = obra;
            contexto.EstadoAtencion = EstadoAtencion.Estrecha;
            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
        }
    }

    /// <summary>
    /// Acción <c>guardar_cuaderno</c>: detiene la animación de dibujo con <c>EndActivityRouteEndBehavior</c>
    /// y espera a que concluya el frame de cierre para liberar <c>freezeMotion</c> (CP-012).
    /// </summary>
    public sealed class AccionGuardarCuaderno : AccionCuerpo
    {
        public const long MaxDuracionGuardarCuadernoMs = 2_000;

        public AccionGuardarCuaderno(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "guardar_cuaderno";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionGuardarCuadernoMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            contexto.AccionContinua = null;
            contexto.EstadoAtencion = EstadoAtencion.Abierta;

            if (contexto.Medios.EstaEnAnimacionActividad)
            {
                contexto.Medios.DetenerAnimacionActividad();
                if (contexto.Medios.EstaEnAnimacionActividad)
                {
                    return;
                }
            }

            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (!contexto.Medios.EstaEnAnimacionActividad)
            {
                this.Cumplir();
            }
        }
    }

    /// <summary>Acción <c>gesto(tipo)</c>: muestra un emote nativo sobre la cabeza del personaje (CP-012 §4.1).</summary>
    public sealed class AccionGesto : AccionCuerpo
    {
        public const long MaxDuracionGestoMs = 1_000;

        public AccionGesto(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "gesto";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionGestoMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (string.IsNullOrWhiteSpace(this.Paso.Tipo) || !Acciones.TiposGestoV1.Contains(this.Paso.Tipo))
            {
                this.Bloquear($"tipo de gesto inválido: {this.Paso.Tipo}");
                return;
            }

            if (!contexto.Medios.HacerGesto(this.Paso.Tipo))
            {
                this.Bloquear($"no se pudo ejecutar el gesto: {this.Paso.Tipo}");
                return;
            }

            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
        }
    }

    /// <summary>Acción <c>murmurar(texto)</c>: muestra una burbuja breve sobre el personaje (<c>showTextAboveHead</c>).</summary>
    public sealed class AccionMurmurar : AccionCuerpo
    {
        public const long MaxDuracionMurmurarMs = 1_000;

        public AccionMurmurar(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "murmurar";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionMurmurarMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (string.IsNullOrWhiteSpace(this.Paso.Texto))
            {
                this.Bloquear("texto vacío en murmurar");
                return;
            }

            if (!contexto.Medios.Murmurar(this.Paso.Texto))
            {
                this.Bloquear("no se pudo mostrar el murmullo");
                return;
            }

            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
        }
    }

    /// <summary>Acción <c>esperar(duracion_min)</c>: mantiene la postura actual durante los minutos de juego indicados.</summary>
    public sealed class AccionEsperar : AccionCuerpo
    {
        public const int MaxMinutosJuegoEspera = 240;

        private long duracionMs;

        public AccionEsperar(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "esperar";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto)
            => Math.Max(1_000, this.duracionMs + 1_000);

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            var minutos = this.Paso.DuracionMin ?? 10;
            if (minutos <= 0 || minutos > MaxMinutosJuegoEspera)
            {
                this.Bloquear($"duración de espera fuera de rango: {minutos}");
                return;
            }

            this.duracionMs = (minutos / 10) * HoraJuego.MilisegundosPorDiezMinutos(contexto.ModoReloj);
            if (this.duracionMs <= 0)
            {
                this.Cumplir();
            }
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (ahoraMs - this.InicioMs >= this.duracionMs)
            {
                this.Cumplir();
            }
        }
    }

    /// <summary>Acción <c>volver_a_rutina</c>: cierra animaciones activas y devuelve al personaje a su tramo horario (CP-011).</summary>
    public sealed class AccionVolverARutina : AccionCuerpo
    {
        public const long MaxDuracionVolverMs = 2_000;

        public AccionVolverARutina(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "volver_a_rutina";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionVolverMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            contexto.AccionContinua = null;
            contexto.Sentada = false;
            contexto.FocoObjetivoRef = null;
            contexto.EstadoAtencion = EstadoAtencion.Abierta;

            if (contexto.Medios.EstaEnAnimacionActividad)
            {
                contexto.Medios.DetenerAnimacionActividad();
                if (contexto.Medios.EstaEnAnimacionActividad)
                {
                    return;
                }
            }

            if (!contexto.Medios.VolverARutina())
            {
                this.Bloquear("no se pudo reincorporar a la rutina");
                return;
            }

            this.Cumplir();
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (!contexto.Medios.EstaEnAnimacionActividad)
            {
                if (!contexto.Medios.VolverARutina())
                {
                    this.Bloquear("no se pudo reincorporar a la rutina");
                    return;
                }

                this.Cumplir();
            }
        }
    }

    /// <summary>Acción de respaldo para nombres de acción fuera del vocabulario v1.</summary>
    public sealed class AccionDesconocida : AccionCuerpo
    {
        public AccionDesconocida(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => this.Paso.Accion ?? "desconocida";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => 0;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
            => this.Bloquear($"acción desconocida: {this.Paso.Accion}");

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
        }
    }

    /// <summary>
    /// Orquestador determinista de planes del cuerpo (CP-302): encola las acciones <see cref="AccionCuerpo"/>,
    /// las avanza tick a tick, registra la prosa de cada acción hacia el búfer y emite el evento
    /// <see cref="TipoSucesoCuerpo.PlanTerminado"/> al concluir la última acción con duración o iniciar una continua.
    /// </summary>
    public sealed class EjecutorCuerpo
    {
        private readonly Queue<AccionCuerpo> cola = new();
        private AccionCuerpo? actual;
        private bool planEnCurso;
        private string? idPlanActual;

        public EjecutorCuerpo(ContextoEjecucionAccion contexto)
        {
            this.Contexto = contexto ?? throw new ArgumentNullException(nameof(contexto));
        }

        public ContextoEjecucionAccion Contexto { get; }

        public List<SucesoCuerpoEjecucion> Sucesos { get; } = new();

        public string? IdPlanActual => this.idPlanActual;

        public AccionCuerpo? AccionActual => this.actual;

        public bool Ocupado => this.actual is not null || this.cola.Count > 0;

        public string? Continua => this.Contexto.AccionContinua;

        public bool Sentada => this.Contexto.Sentada;

        /// <summary>Notificado cada vez que una acción inicia válidamente (para volcar su prosa al búfer).</summary>
        public event Action<PasoPlan, long>? AccionIniciada;

        /// <summary>Notificado cuando una acción no continua termina correctamente (CP-613).</summary>
        public event Action<PasoPlan, long>? AccionTerminada;

        /// <summary>Notificado cuando una acción se bloquea con un motivo.</summary>
        public event Action<PasoPlan, string, long>? AccionBloqueada;

        /// <summary>Notificado al terminar la última acción con duración del plan (alimenta el hito de intención, CP-201).</summary>
        public event Action<string?, long>? PlanTerminado;

        public void EjecutarPlan(IEnumerable<PasoPlan> pasos, long ahoraMs, string? idPlan = null)
        {
            if (pasos is null)
            {
                throw new ArgumentNullException(nameof(pasos));
            }

            this.cola.Clear();
            this.actual = null;
            this.idPlanActual = idPlan;
            this.planEnCurso = true;

            foreach (var paso in pasos)
            {
                if (paso is not null)
                {
                    this.cola.Enqueue(AccionCuerpo.Crear(paso));
                }
            }

            this.AvanzarCola(ahoraMs);
        }

        public void AvanzarTick(long ahoraMs)
        {
            if (this.actual is not null)
            {
                this.actual.AvanzarTick(this.Contexto, ahoraMs);
                if (this.actual.Estado == EstadoEjecucionAccion.EnCurso)
                {
                    return;
                }

                this.ProcesarFinDeAccion(this.actual, ahoraMs);
                this.actual = null;
            }

            if (this.planEnCurso)
            {
                this.AvanzarCola(ahoraMs);
            }
        }

        public void CancelarPlan()
        {
            this.cola.Clear();
            this.actual = null;
            this.planEnCurso = false;
            this.idPlanActual = null;
        }

        /// <summary>
        /// Ejecuta una acción breve de reflejo en el acto sin abrir un plan de intención ni emitir
        /// <see cref="TipoSucesoCuerpo.PlanTerminado"/> (CP-304).
        /// </summary>
        public bool EjecutarReflejo(PasoPlan paso, long ahoraMs)
        {
            if (paso is null || this.Ocupado)
            {
                return false;
            }

            var accion = AccionCuerpo.Crear(paso);
            var detalle = paso.Lugar ?? paso.Objetivo ?? paso.Obra ?? paso.Tipo ?? paso.Texto;
            this.Sucesos.Add(new SucesoCuerpoEjecucion(ahoraMs, TipoSucesoCuerpo.AccionIniciada, accion.Nombre, detalle));

            accion.Iniciar(this.Contexto, ahoraMs);
            if (accion.Estado == EstadoEjecucionAccion.Bloqueada)
            {
                var motivo = accion.MotivoBloqueo ?? "bloqueada";
                this.Sucesos.Add(new SucesoCuerpoEjecucion(ahoraMs, TipoSucesoCuerpo.AccionBloqueada, accion.Nombre, motivo));
                this.AccionBloqueada?.Invoke(accion.Paso, motivo, ahoraMs);
                return false;
            }

            this.AccionIniciada?.Invoke(accion.Paso, ahoraMs);

            if (accion.Estado == EstadoEjecucionAccion.EnCurso)
            {
                this.actual = accion;
                return true;
            }

            this.Sucesos.Add(new SucesoCuerpoEjecucion(
                ahoraMs,
                TipoSucesoCuerpo.AccionTerminada,
                accion.Nombre,
                accion.EsContinua ? "continua" : null));
            return true;
        }

        private void AvanzarCola(long ahoraMs)
        {
            while (this.actual is null && this.cola.Count > 0)
            {
                var accion = this.cola.Dequeue();
                var detalle = accion.Paso.Lugar ?? accion.Paso.Objetivo ?? accion.Paso.Obra ?? accion.Paso.Tipo ?? accion.Paso.Texto;
                this.Sucesos.Add(new SucesoCuerpoEjecucion(ahoraMs, TipoSucesoCuerpo.AccionIniciada, accion.Nombre, detalle));

                accion.Iniciar(this.Contexto, ahoraMs);
                if (accion.Estado != EstadoEjecucionAccion.Bloqueada)
                {
                    this.AccionIniciada?.Invoke(accion.Paso, ahoraMs);
                }

                if (accion.Estado == EstadoEjecucionAccion.EnCurso)
                {
                    this.actual = accion;
                    return;
                }

                this.ProcesarFinDeAccion(accion, ahoraMs);
                if (!this.planEnCurso)
                {
                    return;
                }
            }

            if (this.planEnCurso && this.actual is null && this.cola.Count == 0)
            {
                var idTerminado = this.idPlanActual;
                this.planEnCurso = false;
                this.idPlanActual = null;
                this.Sucesos.Add(new SucesoCuerpoEjecucion(ahoraMs, TipoSucesoCuerpo.PlanTerminado, "plan", idTerminado));
                this.PlanTerminado?.Invoke(idTerminado, ahoraMs);
            }
        }

        private void ProcesarFinDeAccion(AccionCuerpo accion, long ahoraMs)
        {
            if (accion.Estado == EstadoEjecucionAccion.Bloqueada)
            {
                var motivo = accion.MotivoBloqueo ?? "bloqueada";
                this.Sucesos.Add(new SucesoCuerpoEjecucion(ahoraMs, TipoSucesoCuerpo.AccionBloqueada, accion.Nombre, motivo));
                this.cola.Clear();
                this.planEnCurso = false;
                this.idPlanActual = null;
                this.AccionBloqueada?.Invoke(accion.Paso, motivo, ahoraMs);
                return;
            }

            if (accion.Estado == EstadoEjecucionAccion.Cumplida)
            {
                this.Sucesos.Add(new SucesoCuerpoEjecucion(
                    ahoraMs,
                    TipoSucesoCuerpo.AccionTerminada,
                    accion.Nombre,
                    accion.EsContinua ? "continua" : null));
                this.AccionTerminada?.Invoke(accion.Paso, ahoraMs);
            }
        }
    }

    /// <summary>Acción <c>acercarse_a(objetivo)</c>: camina hacia el jugador objetivo para iniciar diálogo (CP-613).</summary>
    public sealed class AccionAcercarseA : AccionCuerpo
    {
        public const long MaxDuracionMs = 20_000;
        public const int MaxDistanciaCasillasPorDefecto = 20;

        private string objetivo = "";

        public AccionAcercarseA(PasoPlan paso)
            : base(paso)
        {
        }

        public override string Nombre => "acercarse_a";

        public override long DuracionMaximaMs(ContextoEjecucionAccion contexto) => MaxDuracionMs;

        protected override void AlIniciar(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            if (string.IsNullOrWhiteSpace(this.Paso.Objetivo))
            {
                this.Bloquear("objetivo_vacio");
                return;
            }

            this.objetivo = this.Paso.Objetivo.Trim();
            contexto.Medios.ReclamarControl("acercarse_a");
            if (contexto.Medios.EstaEnAnimacionActividad || contexto.AccionContinua is not null)
            {
                contexto.Medios.DetenerAnimacionActividad();
                contexto.AccionContinua = null;
                contexto.ObraEnCurso = null;
            }

            contexto.Sentada = false;
            contexto.EstadoAtencion = EstadoAtencion.Abierta;
            contexto.FocoObjetivoRef = this.objetivo;

            if (!contexto.Medios.IniciarAcercamientoAJugador(this.objetivo))
            {
                contexto.Medios.DetenerMovimiento();
                this.Bloquear("interlocutor_alejado");
                return;
            }

            var estado = contexto.Medios.EvaluarAcercamientoAJugador(this.objetivo, MaxDistanciaCasillasPorDefecto);
            if (estado == EstadoAcercamientoJugador.EnRango)
            {
                contexto.Medios.DetenerMovimiento();
                this.Cumplir();
            }
            else if (estado == EstadoAcercamientoJugador.CanceladoFueraDeAlcance)
            {
                contexto.Medios.DetenerMovimiento();
                this.Bloquear("interlocutor_alejado");
            }
        }

        protected override void AlAvanzarTick(ContextoEjecucionAccion contexto, long ahoraMs)
        {
            var estado = contexto.Medios.EvaluarAcercamientoAJugador(this.objetivo, MaxDistanciaCasillasPorDefecto);
            switch (estado)
            {
                case EstadoAcercamientoJugador.EnRango:
                    contexto.Medios.DetenerMovimiento();
                    this.Cumplir();
                    break;

                case EstadoAcercamientoJugador.CanceladoFueraDeAlcance:
                    contexto.Medios.DetenerMovimiento();
                    this.Bloquear("interlocutor_alejado");
                    break;
            }
        }
    }
}
