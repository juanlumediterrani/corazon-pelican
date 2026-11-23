using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>
    /// Mensajes e identificadores del puente multijugador cliente ↔ anfitrión para diálogo y cartas de prórroga (CP-606, CP-612).
    /// </summary>
    public static class ProtocoloMultijugador
    {
        public const string ClaveDialogoPreparadoModData = "CorazonPelican.DialogoPreparado";
        public const string PrefijoDialogoUltimoModData = "CorazonPelican.DialogoUltimo.";
        public const string MsgDialogoIniciar = "CorazonPelican.Dialogo.Iniciar";
        public const string MsgDialogoResultado = "CorazonPelican.Dialogo.Resultado";
        public const string MsgDialogoConsumido = "CorazonPelican.Dialogo.Consumido";
        public const string MsgDialogoEntregarIniciativa = "CorazonPelican.Dialogo.EntregarIniciativa";
        public const string MsgProrrogaSincronizarMail = "CorazonPelican.Prorroga.SincronizarMail";
        public const string MsgProrrogaAbrirCarta = "CorazonPelican.Prorroga.AbrirCarta";
        public const string MsgProrrogaMostrarPregunta = "CorazonPelican.Prorroga.MostrarPregunta";
        public const string MsgProrrogaResponderCarta = "CorazonPelican.Prorroga.ResponderCarta";
        public const string MsgProrrogaInvalidarMail = "CorazonPelican.Prorroga.InvalidarMail";

        private static readonly JsonSerializerOptions JsonOpciones = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public static string ConstruirReferenciaGranjero(long playerId)
            => IdentidadesObservacion.Granjero(playerId);

        public static string ClaveUltimoDialogoJugador(long playerId)
            => $"{PrefijoDialogoUltimoModData}{playerId}";

        public static string SerializarPreparado(int dia, DialogoPreparado dialogo)
            => JsonSerializer.Serialize(new DialogoPreparadoInstantanea
            {
                Dia = Math.Max(1, dia),
                Para = string.IsNullOrWhiteSpace(dialogo.Para) ? null : dialogo.Para.Trim(),
                Tipo = dialogo.Tipo,
                Texto = dialogo.Texto,
                Opciones = dialogo.Opciones?.Select(o => new OpcionDialogoPreparado
                {
                    Id = o.Id,
                    Texto = o.Texto,
                    Respuesta = o.Respuesta,
                }).ToList(),
            }, JsonOpciones);

        public static DialogoPreparadoInstantanea? DeserializarPreparado(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var snap = JsonSerializer.Deserialize<DialogoPreparadoInstantanea>(json, JsonOpciones);
                if (snap is null || snap.Dia < 1 || string.IsNullOrWhiteSpace(snap.Texto))
                {
                    return null;
                }

                var normalizado = GestorDialogo.NormalizarDialogoPreparado(new DialogoPreparado
                {
                    Para = snap.Para,
                    Tipo = snap.Tipo,
                    Texto = snap.Texto,
                    Opciones = snap.Opciones,
                });
                if (normalizado is null)
                {
                    return null;
                }

                return new DialogoPreparadoInstantanea
                {
                    Dia = snap.Dia,
                    Para = normalizado.Para,
                    Tipo = normalizado.Tipo,
                    Texto = normalizado.Texto,
                    Opciones = normalizado.Opciones,
                };
            }
            catch
            {
                return null;
            }
        }

        public static string SerializarUltimoMensaje(int dia, string mensaje)
            => JsonSerializer.Serialize(new DialogoUltimoInstantanea
            {
                Dia = Math.Max(1, dia),
                Mensaje = mensaje.Trim(),
            }, JsonOpciones);

        public static DialogoUltimoInstantanea? DeserializarUltimoMensaje(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            try
            {
                var snap = JsonSerializer.Deserialize<DialogoUltimoInstantanea>(json, JsonOpciones);
                return snap is not null && snap.Dia >= 1 && !string.IsNullOrWhiteSpace(snap.Mensaje) ? snap : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Resuelve qué mostrar al pulsar hablar sobre el NPC sin esperar al anfitrión ni al LLM (CP-612).</summary>
        public static DialogoInstantaneo ResolverDialogoDesdeInstantaneas(
            int diaActual,
            long playerId,
            string? jsonUltimo,
            string? jsonPreparado)
        {
            var diaValido = Math.Max(1, diaActual);
            var ultimo = DeserializarUltimoMensaje(jsonUltimo);
            if (ultimo is not null && ultimo.Dia == diaValido)
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.RepetirUltimaVuelta, ultimo.Mensaje);
            }

            var preparado = DeserializarPreparado(jsonPreparado);
            if (preparado is null || preparado.Dia != diaValido)
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.Ninguno, string.Empty);
            }

            var farmerRef = ConstruirReferenciaGranjero(playerId);
            if (!string.IsNullOrWhiteSpace(preparado.Para)
                && !string.Equals(preparado.Para, farmerRef, StringComparison.Ordinal))
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.Ninguno, string.Empty);
            }

            if (string.Equals(preparado.Tipo, "pregunta", StringComparison.Ordinal)
                && preparado.Opciones is { Count: >= 2 })
            {
                return new DialogoInstantaneo(ModoDialogoInstantaneo.Pregunta, preparado.Texto, preparado.Opciones);
            }

            return new DialogoInstantaneo(ModoDialogoInstantaneo.Afirmacion, preparado.Texto);
        }

        public static bool EsPeticionDialogoValida(DialogoSolicitudRemota? solicitud)
            => solicitud is not null && !string.IsNullOrWhiteSpace(solicitud.NpcName);

        public static bool EsCartaProrroga(string? mailTitle)
            => !string.IsNullOrWhiteSpace(mailTitle)
                && mailTitle.StartsWith(GestorProrrogaCartas.PrefijoCartaId, StringComparison.Ordinal);
    }

    public sealed class DialogoSolicitudRemota
    {
        public string NpcName { get; set; } = string.Empty;

        public string FarmerName { get; set; } = string.Empty;
    }

    public sealed class DialogoResultadoRemoto
    {
        public string NpcName { get; set; } = string.Empty;

        public string Tipo { get; set; } = string.Empty;

        public string Frase { get; set; } = string.Empty;
    }

    public sealed class ProrrogaMailSyncRemoto
    {
        public string CartaId { get; set; } = string.Empty;

        public string ContenidoMail { get; set; } = string.Empty;
    }

    public sealed class ProrrogaAbrirCartaRemoto
    {
        public string CartaId { get; set; } = string.Empty;
    }

    public sealed class ProrrogaPreguntaRemota
    {
        public string CartaId { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string Pregunta { get; set; } = string.Empty;

        public string TextoAceptar { get; set; } = string.Empty;

        public string TextoRechazar { get; set; } = string.Empty;
    }

    public sealed class ProrrogaRespuestaRemota
    {
        public string CartaId { get; set; } = string.Empty;

        public bool Aceptar { get; set; }
    }

    public sealed class DialogoPreparadoInstantanea
    {
        [JsonPropertyName("dia")] public int Dia { get; set; }
        [JsonPropertyName("para")] public string? Para { get; set; }
        [JsonPropertyName("tipo")] public string Tipo { get; set; } = "afirmacion";
        [JsonPropertyName("texto")] public string Texto { get; set; } = string.Empty;
        [JsonPropertyName("opciones")] public List<OpcionDialogoPreparado>? Opciones { get; set; }
    }

    public sealed class DialogoUltimoInstantanea
    {
        [JsonPropertyName("dia")] public int Dia { get; set; }
        [JsonPropertyName("mensaje")] public string Mensaje { get; set; } = string.Empty;
    }

    public sealed class DialogoConsumidoRemoto
    {
        public string NpcName { get; set; } = string.Empty;
        public string FarmerName { get; set; } = string.Empty;
        public int Dia { get; set; }
        public string? OpcionId { get; set; }
        public string? UltimoMensaje { get; set; }
    }

    public sealed class DialogoIniciativaRemota
    {
        public string NpcName { get; set; } = string.Empty;
        public int Dia { get; set; }
        public string InstantaneaJson { get; set; } = string.Empty;
    }
}
