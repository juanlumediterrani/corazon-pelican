using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
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
        int CaducaEnMs);

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
        private const string ReflejoPorDefecto = "Un segundo... estaba pensando en algo.";

        private Pulso? pulsoAnticipado;
        private string? respuestaAnticipada;
        private TurnoDialogoActivo? turnoActivo;

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
        public void RegistrarPulsoAnticipado(Pulso pulso)
        {
            if (pulso is null || pulso.Id <= 0)
            {
                return;
            }

            this.pulsoAnticipado = pulso;
            this.respuestaAnticipada = null;
        }

        /// <summary>Abre un nuevo turno de conversación con reacción inmediata y reuso opcional del pulso anticipado.</summary>
        public ReaccionInmediataDialogo IniciarTurno(
            string interlocutorRef,
            string nombreInterlocutor,
            int dia,
            PlantillasProsa plantillas,
            long ahoraMs,
            int caducaEnMs,
            Func<string, Pulso> emitirPulsoInterlocucion)
        {
            var lineaBreve = ElegirLineaBreve(plantillas, dia, interlocutorRef);
            var caducidadEfectiva = Math.Max(100, caducaEnMs);

            // Si hay un pulso anticipado pendiente para la misma epoca/momento sin haber recibido respuesta todavía
            // o ya con respuesta lista, podemos reutilizarlo.
            Pulso pulso;
            bool reutilizoAnticipado = false;
            if (this.pulsoAnticipado is not null
                && this.pulsoAnticipado.TiempoJuego?.Dia == dia
                && this.pulsoAnticipado.Interlocutor?.Ref == interlocutorRef)
            {
                pulso = this.pulsoAnticipado;
                reutilizoAnticipado = true;
            }
            else
            {
                this.pulsoAnticipado = null;
                this.respuestaAnticipada = null;
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
