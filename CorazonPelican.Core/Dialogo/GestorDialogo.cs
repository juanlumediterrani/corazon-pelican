using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>Contexto previo para decidir si el motor intercepta la conversación o cede a vanilla (GSS §8.1, CP-503).</summary>
    public sealed record ContextoIntercepcionDialogo(
        bool MenteHabilitada,
        bool MenteCaida,
        bool EnEvento,
        bool ValleyTalkActivo);

    /// <summary>Resolución de intercepción antes de tocar el diálogo vanilla del juego.</summary>
    public sealed record DecisionIntercepcionDialogo(
        bool Interceptar,
        string Motivo)
    {
        public static DecisionIntercepcionDialogo Tomar() => new(true, "interceptada");
        public static DecisionIntercepcionDialogo CaerAVanilla(string motivo) => new(false, motivo);
    }

    /// <summary>Reacción inmediata del reflejo de diálogo (&lt; 300 ms): emote pensativo, frase breve y pulso asociado.</summary>
    public sealed record ReaccionInmediataDialogo(
        int EmoteCodigo,
        string LineaBreve,
        Pulso? Pulso,
        bool ReutilizoAnticipado,
        int CaducaEnMs,
        string? FraseAnticipada = null);

    /// <summary>Tipo de desenlace de un turno de diálogo abierto.</summary>
    public enum DesenlaceTurnoDialogo
    {
        Ninguno = 0,
        MostrarFrasePensada = 1,
        CaducadoCaerAVanilla = 2,
        GuardadaLoQueIbaADecir = 3,
    }

    /// <summary>Resultado emitido al llegar una respuesta de la mente o avanzar el tick del turno.</summary>
    public sealed record DesenlaceDialogoTurno(
        DesenlaceTurnoDialogo Desenlace,
        string? Frase = null,
        int EmoteCodigo = 0,
        string? RecuerdoGuardado = null);

    public enum ModoDialogoInstantaneo
    {
        Ninguno = 0,
        Afirmacion = 1,
        Pregunta = 2,
        RepetirUltimaVuelta = 3,
    }

    public sealed record DialogoInstantaneo(
        ModoDialogoInstantaneo Modo,
        string Texto,
        IReadOnlyList<OpcionDialogoPreparado>? Opciones = null);

    public sealed record RegistroEleccionDialogo(
        bool Aceptada,
        string PreguntaTexto,
        string OpcionTexto,
        string Vuelta);

    /// <summary>
    /// Máquina de estados pura para el ciclo de conversación interceptada (GSS §8.2, ADS §8.2, CP-503):
    /// reflejo inmediato (&lt; 300 ms) → pulso de interlocución (o reutilización de anticipado) →
    /// frase pensada, caducidad a diálogo vanilla («…») o recuerdo «lo que iba a decir».
    /// </summary>
    public sealed class GestorDialogo
    {
        public const int EmotePensando = 40;
        public const int EmoteCaducado = 40;
        public const string TextoCaducado = "…";
        public const string ReflejoPorDefecto = "Un segundo... estaba pensando en algo.";

        private Pulso? pulsoAnticipado;
        private string? respuestaAnticipada;
        private string? ubicacionAnticipada;
        private string? climaAnticipado;
        private long pulsoAnticipadoMs;
        private TurnoDialogoActivo? turnoActivo;
        private readonly Dictionary<(int Dia, string ParaRef), DialogoPreparado> preparadosPorDia = new();
        private readonly Dictionary<(int Dia, string InterlocutorRef), string> ultimosMensajesPorDia = new();

        public Pulso? PulsoAnticipadoActual => this.pulsoAnticipado;

        public bool TieneRespuestaAnticipada => !string.IsNullOrWhiteSpace(this.respuestaAnticipada);

        private sealed class TurnoDialogoActivo
        {
            public string InterlocutorRef { get; init; } = "";
            public string NombreInterlocutor { get; init; } = "";
            public long PulsoId { get; init; }
            public long InicioMs { get; init; }
            public int CaducaEnMs { get; init; }
            public bool JugadorAlejado { get; set; }
        }

        public bool HayTurnoActivo => this.turnoActivo is not null;

        public long? PulsoTurnoActivoId => this.turnoActivo?.PulsoId;

        /// <summary>Evalúa si procede interceptar la conversación ordinaria o dejar el diálogo vanilla (ADS §8.1).</summary>
        public static DecisionIntercepcionDialogo EvaluarIntercepcion(ContextoIntercepcionDialogo contexto)
        {
            if (contexto.EnEvento)
            {
                return DecisionIntercepcionDialogo.CaerAVanilla("evento_en_curso");
            }

            if (contexto.ValleyTalkActivo)
            {
                return DecisionIntercepcionDialogo.CaerAVanilla("valleytalk_activo");
            }

            if (!contexto.MenteHabilitada)
            {
                return DecisionIntercepcionDialogo.CaerAVanilla("mente_deshabilitada");
            }

            if (contexto.MenteCaida)
            {
                return DecisionIntercepcionDialogo.CaerAVanilla("mente_caida");
            }

            return DecisionIntercepcionDialogo.Tomar();
        }

        public static string ElegirLineaBreve(PlantillasProsa plantillas, int dia, string interlocutorRef)
        {
            if (plantillas?.ReflejoDialogo is { Count: > 0 } variantes)
            {
                return PlantillasProsa.ElegirVariante(variantes, dia, $"reflejo_dialogo:{interlocutorRef}");
            }

            return ReflejoPorDefecto;
        }

        /// <summary>Registra un pulso anticipado cuando un granjero se aproxima al NPC.</summary>
        public void RegistrarPulsoAnticipado(Pulso pulso, string? ubicacion = null, string? clima = null, long ahoraMs = 0)
        {
            if (pulso is null || pulso.Id <= 0)
            {
                return;
            }

            this.pulsoAnticipado = pulso;
            this.respuestaAnticipada = null;
            this.ubicacionAnticipada = ubicacion;
            this.climaAnticipado = clima;
            this.pulsoAnticipadoMs = ahoraMs;
        }

        /// <summary>Evalúa si un pulso anticipado pendiente debe cancelarse porque el granjero pasó de largo o cambió la escena.</summary>
        public string? EvaluarCancelacionAnticipado(
            Observacion obs,
            IReadOnlyList<EntidadEnBanda> bandas,
            double umbralPeriferia,
            long ahoraMs,
            int caducaEnMs)
        {
            if (this.pulsoAnticipado is null || this.turnoActivo is not null)
            {
                return null;
            }

            if (this.pulsoAnticipado.TiempoJuego?.Dia != obs.Dia
                || (!string.IsNullOrWhiteSpace(this.ubicacionAnticipada)
                    && !string.Equals(this.ubicacionAnticipada, obs.Ubicacion, StringComparison.Ordinal))
                || (!string.IsNullOrWhiteSpace(this.climaAnticipado)
                    && !string.Equals(this.climaAnticipado, obs.Clima, StringComparison.Ordinal)))
            {
                return "escena_cambiada";
            }

            if (this.pulsoAnticipadoMs > 0 && ahoraMs - this.pulsoAnticipadoMs >= Math.Max(100, caducaEnMs))
            {
                return "caducado";
            }

            var interlocutorRef = this.pulsoAnticipado.Interlocutor?.Ref;
            if (!string.IsNullOrWhiteSpace(interlocutorRef))
            {
                var presente = obs.Entidades.Any(e => string.Equals(e.Ref, interlocutorRef, StringComparison.Ordinal));
                if (!presente)
                {
                    return "interlocutor_alejado";
                }

                var banda = bandas.FirstOrDefault(b => string.Equals(b.Ref, interlocutorRef, StringComparison.Ordinal));
                if (banda is null || banda.Saliencia < umbralPeriferia)
                {
                    return "interlocutor_alejado";
                }
            }

            return null;
        }

        /// <summary>Cancela y descarta el pulso anticipado y cualquier respuesta anticipada pendiente.</summary>
        public Pulso? CancelarAnticipado()
        {
            var cancelado = this.pulsoAnticipado;
            this.pulsoAnticipado = null;
            this.respuestaAnticipada = null;
            this.ubicacionAnticipada = null;
            this.climaAnticipado = null;
            this.pulsoAnticipadoMs = 0;
            return cancelado;
        }

        /// <summary>Abre un nuevo turno de conversación con reacción inmediata y reuso opcional del pulso anticipado.</summary>
        public ReaccionInmediataDialogo IniciarTurno(
            string interlocutorRef,
            string nombreInterlocutor,
            int dia,
            PlantillasProsa plantillas,
            long ahoraMs,
            int caducaEnMs,
            Func<string, Pulso> emitirPulsoInterlocucion,
            string? ubicacion = null,
            string? clima = null)
        {
            var lineaBreve = ElegirLineaBreve(plantillas, dia, interlocutorRef);
            var caducidadEfectiva = Math.Max(100, caducaEnMs);

            // Si hay un pulso anticipado pendiente para la misma epoca/escena sin cambios relevantes, reutilizarlo.
            Pulso pulso;
            bool reutilizoAnticipado = false;
            var mismaUbicacion = string.IsNullOrWhiteSpace(this.ubicacionAnticipada)
                || string.IsNullOrWhiteSpace(ubicacion)
                || string.Equals(this.ubicacionAnticipada, ubicacion, StringComparison.Ordinal);
            var mismoClima = string.IsNullOrWhiteSpace(this.climaAnticipado)
                || string.IsNullOrWhiteSpace(clima)
                || string.Equals(this.climaAnticipado, clima, StringComparison.Ordinal);
            var noCaducado = this.pulsoAnticipadoMs <= 0 || (ahoraMs - this.pulsoAnticipadoMs) < caducidadEfectiva;

            if (this.pulsoAnticipado is not null
                && this.pulsoAnticipado.TiempoJuego?.Dia == dia
                && this.pulsoAnticipado.Interlocutor?.Ref == interlocutorRef
                && mismaUbicacion
                && mismoClima
                && noCaducado)
            {
                pulso = this.pulsoAnticipado;
                reutilizoAnticipado = true;
            }
            else
            {
                this.CancelarAnticipado();
                pulso = emitirPulsoInterlocucion(interlocutorRef);
            }

            pulso.CaducaEnMs ??= caducidadEfectiva;

            this.turnoActivo = new TurnoDialogoActivo
            {
                InterlocutorRef = interlocutorRef,
                NombreInterlocutor = string.IsNullOrWhiteSpace(nombreInterlocutor) ? interlocutorRef : nombreInterlocutor,
                PulsoId = pulso.Id,
                InicioMs = ahoraMs,
                CaducaEnMs = pulso.CaducaEnMs.Value,
            };

            return new ReaccionInmediataDialogo(
                EmotePensando,
                lineaBreve,
                pulso,
                reutilizoAnticipado,
                this.turnoActivo.CaducaEnMs);
        }

        /// <summary>Consume si ya existía una frase pensada anticipadamente para entregarla de inmediato.</summary>
        public DesenlaceDialogoTurno? ConsumirRespuestaAnticipadaSiLista(bool interlocutorPresente, EstadoPersonaje estado)
        {
            if (this.turnoActivo is null || this.pulsoAnticipado is null || string.IsNullOrWhiteSpace(this.respuestaAnticipada))
            {
                return null;
            }

            var frase = this.respuestaAnticipada;
            this.pulsoAnticipado = null;
            this.respuestaAnticipada = null;
            return this.EntregarFraseAlTurno(frase, interlocutorPresente, estado);
        }

        /// <summary>Procesa una respuesta de la mente verificando si corresponde al turno abierto o al anticipado.</summary>
        public DesenlaceDialogoTurno ProcesarRespuesta(
            Pulso pulso,
            Respuesta respuesta,
            bool interlocutorPresente,
            EstadoPersonaje estado,
            long ahoraMs)
        {
            if (pulso is null || respuesta is null || string.IsNullOrWhiteSpace(respuesta.Frase))
            {
                return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno);
            }

            if (this.pulsoAnticipado is not null && this.pulsoAnticipado.Id == pulso.Id && this.turnoActivo is null)
            {
                this.respuestaAnticipada = respuesta.Frase.Trim();
                return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno);
            }

            if (this.turnoActivo is null || this.turnoActivo.PulsoId != pulso.Id)
            {
                return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno);
            }

            if (ahoraMs - this.turnoActivo.InicioMs >= this.turnoActivo.CaducaEnMs)
            {
                this.turnoActivo = null;
                return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno);
            }

            return this.EntregarFraseAlTurno(respuesta.Frase.Trim(), interlocutorPresente, estado);
        }

        /// <summary>Evalúa alejamiento del jugador y caducidad de la espera en el turno abierto.</summary>
        public DesenlaceDialogoTurno EvaluarTick(long ahoraMs, bool interlocutorPresente)
        {
            if (this.turnoActivo is null)
            {
                return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno);
            }

            if (!interlocutorPresente)
            {
                this.turnoActivo.JugadorAlejado = true;
            }

            if (ahoraMs - this.turnoActivo.InicioMs >= this.turnoActivo.CaducaEnMs)
            {
                var alejado = this.turnoActivo.JugadorAlejado;
                this.turnoActivo = null;
                this.pulsoAnticipado = null;
                this.respuestaAnticipada = null;

                return alejado
                    ? new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno)
                    : new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.CaducadoCaerAVanilla, TextoCaducado, EmoteCaducado);
            }

            return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.Ninguno);
        }

        public void Cancelar()
        {
            this.turnoActivo = null;
            this.pulsoAnticipado = null;
            this.respuestaAnticipada = null;
            this.preparadosPorDia.Clear();
            this.ultimosMensajesPorDia.Clear();
        }

        /// <summary>Normaliza y almacena un diálogo precalculado (afirmación o pregunta con opciones y vuelta) para hoy (CP-612).</summary>
        public DialogoPreparado? AlmacenarPreparado(int dia, DialogoPreparado? dialogo, string? paraRef = null)
        {
            var diaValido = Math.Max(1, dia);
            var normalizado = NormalizarDialogoPreparado(dialogo);
            if (normalizado is null)
            {
                return null;
            }

            foreach (var claveVieja in this.preparadosPorDia.Keys.Where(k => k.Dia < diaValido).ToList())
            {
                this.preparadosPorDia.Remove(claveVieja);
            }

            foreach (var claveVieja in this.ultimosMensajesPorDia.Keys.Where(k => k.Dia < diaValido).ToList())
            {
                this.ultimosMensajesPorDia.Remove(claveVieja);
            }

            var destino = !string.IsNullOrWhiteSpace(normalizado.Para)
                ? normalizado.Para!.Trim()
                : (paraRef?.Trim() ?? string.Empty);

            normalizado.Para = string.IsNullOrWhiteSpace(destino) ? null : destino;
            this.preparadosPorDia[(diaValido, destino)] = normalizado;
            return normalizado;
        }

        public static DialogoPreparado? NormalizarDialogoPreparado(DialogoPreparado? dialogo)
        {
            if (dialogo is null || string.IsNullOrWhiteSpace(dialogo.Texto))
            {
                return null;
            }

            var tipo = string.Equals(dialogo.Tipo, "pregunta", StringComparison.OrdinalIgnoreCase)
                ? "pregunta"
                : "afirmacion";
            var texto = dialogo.Texto.Trim();

            if (tipo == "pregunta")
            {
                var opcionesValidas = (dialogo.Opciones ?? new List<OpcionDialogoPreparado>())
                    .Where(o => o is not null
                        && !string.IsNullOrWhiteSpace(o.Id)
                        && !string.IsNullOrWhiteSpace(o.Texto)
                        && !string.IsNullOrWhiteSpace(o.Respuesta))
                    .Select(o => new OpcionDialogoPreparado
                    {
                        Id = o.Id.Trim(),
                        Texto = o.Texto.Trim(),
                        Respuesta = o.Respuesta.Trim(),
                    })
                    .Take(4)
                    .ToList();

                if (opcionesValidas.Count >= 2)
                {
                    return new DialogoPreparado
                    {
                        Para = string.IsNullOrWhiteSpace(dialogo.Para) ? null : dialogo.Para.Trim(),
                        Tipo = "pregunta",
                        Texto = texto,
                        Opciones = opcionesValidas,
                    };
                }
            }

            return new DialogoPreparado
            {
                Para = string.IsNullOrWhiteSpace(dialogo.Para) ? null : dialogo.Para.Trim(),
                Tipo = "afirmacion",
                Texto = texto,
            };
        }

        public bool TieneDialogoListoOConsumido(int dia, string interlocutorRef)
        {
            if (string.IsNullOrWhiteSpace(interlocutorRef))
            {
                return false;
            }

            var diaValido = Math.Max(1, dia);
            var refLimpia = interlocutorRef.Trim();
            return this.ultimosMensajesPorDia.ContainsKey((diaValido, refLimpia))
                || this.preparadosPorDia.ContainsKey((diaValido, refLimpia))
                || this.preparadosPorDia.ContainsKey((diaValido, string.Empty));
        }

        public bool YaConsumioHoy(int dia, string interlocutorRef)
        {
            if (string.IsNullOrWhiteSpace(interlocutorRef))
            {
                return false;
            }

            return this.ultimosMensajesPorDia.ContainsKey((Math.Max(1, dia), interlocutorRef.Trim()));
        }

        public DialogoPreparado? ObtenerPreparadoVigente(int dia, string? interlocutorRef = null)
        {
            var diaValido = Math.Max(1, dia);
            if (!string.IsNullOrWhiteSpace(interlocutorRef)
                && this.preparadosPorDia.TryGetValue((diaValido, interlocutorRef.Trim()), out var especifico))
            {
                return especifico;
            }

            return this.preparadosPorDia.TryGetValue((diaValido, string.Empty), out var general) ? general : null;
        }

        /// <summary>
        /// Resuelve al instante (0 ms, sin pantalla de espera) qué mostrar al granjero en <paramref name="dia"/>:
        /// la última vuelta ya entregada hoy si repite, el diálogo precalculado si está listo, o <see cref="ModoDialogoInstantaneo.Ninguno"/>.
        /// </summary>
        public DialogoInstantaneo ConsultarParaInterlocutor(int dia, string interlocutorRef, bool marcarSiEsAfirmacion = true)
        {
            var diaValido = Math.Max(1, dia);
            var refLimpia = interlocutorRef?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(refLimpia)
                && this.ultimosMensajesPorDia.TryGetValue((diaValido, refLimpia), out var ultimo)
                && !string.IsNullOrWhiteSpace(ultimo))
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.RepetirUltimaVuelta, ultimo);
            }

            var preparado = this.ObtenerPreparadoVigente(diaValido, refLimpia);
            if (preparado is null)
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.Ninguno, string.Empty);
            }

            if (string.Equals(preparado.Tipo, "pregunta", StringComparison.Ordinal)
                && preparado.Opciones is { Count: >= 2 })
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.Pregunta, preparado.Texto, preparado.Opciones);
            }

            if (marcarSiEsAfirmacion && !string.IsNullOrEmpty(refLimpia))
            {
                this.ultimosMensajesPorDia[(diaValido, refLimpia)] = preparado.Texto;
            }

            return new DialogoInstantaneo(ModoDialogoInstantaneo.Afirmacion, preparado.Texto);
        }

        public RegistroEleccionDialogo RegistrarEleccion(int dia, string interlocutorRef, string opcionId)
        {
            var diaValido = Math.Max(1, dia);
            var refLimpia = interlocutorRef?.Trim() ?? string.Empty;
            var preparado = this.ObtenerPreparadoVigente(diaValido, refLimpia);
            if (preparado?.Opciones is null || preparado.Opciones.Count == 0)
            {
                return new RegistroEleccionDialogo(false, string.Empty, string.Empty, string.Empty);
            }

            var opcion = preparado.Opciones.FirstOrDefault(o =>
                string.Equals(o.Id, opcionId?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (opcion is null)
            {
                return new RegistroEleccionDialogo(false, preparado.Texto, string.Empty, string.Empty);
            }

            if (!string.IsNullOrEmpty(refLimpia))
            {
                this.ultimosMensajesPorDia[(diaValido, refLimpia)] = opcion.Respuesta;
            }

            return new RegistroEleccionDialogo(true, preparado.Texto, opcion.Texto, opcion.Respuesta);
        }

        public void RegistrarUltimoMensaje(int dia, string interlocutorRef, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(interlocutorRef) || string.IsNullOrWhiteSpace(mensaje))
            {
                return;
            }

            this.ultimosMensajesPorDia[(Math.Max(1, dia), interlocutorRef.Trim())] = mensaje.Trim();
        }

        private DesenlaceDialogoTurno EntregarFraseAlTurno(string frase, bool interlocutorPresente, EstadoPersonaje estado)
        {
            var turno = this.turnoActivo!;
            this.turnoActivo = null;
            this.pulsoAnticipado = null;
            this.respuestaAnticipada = null;

            if (!interlocutorPresente || turno.JugadorAlejado)
            {
                var recuerdoTexto = $"Lo que iba a decirle a {turno.NombreInterlocutor}: «{frase}»";
                if (!estado.Recuerdos.Any(r => string.Equals(r.Texto, recuerdoTexto, StringComparison.Ordinal)))
                {
                    estado.Recuerdos.Add(new Recuerdo
                    {
                        Id = $"rec-dlg-{turno.PulsoId}",
                        Texto = recuerdoTexto,
                        Importancia = 0.6,
                    });
                }

                return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.GuardadaLoQueIbaADecir, frase, RecuerdoGuardado: recuerdoTexto);
            }

            return new DesenlaceDialogoTurno(DesenlaceTurnoDialogo.MostrarFrasePensada, frase);
        }
    }
}
