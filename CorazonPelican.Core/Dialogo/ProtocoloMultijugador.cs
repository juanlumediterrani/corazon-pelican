using System;
using System.Collections.Generic;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>
    /// Mensajes e identificadores del puente multijugador cliente ↔ anfitrión para diálogo y cartas de prórroga (CP-606).
    /// </summary>
    public static class ProtocoloMultijugador
    {
        public const string MsgDialogoIniciar = "CorazonPelican.Dialogo.Iniciar";
        public const string MsgDialogoResultado = "CorazonPelican.Dialogo.Resultado";
        public const string MsgProrrogaSincronizarMail = "CorazonPelican.Prorroga.SincronizarMail";
        public const string MsgProrrogaAbrirCarta = "CorazonPelican.Prorroga.AbrirCarta";
        public const string MsgProrrogaMostrarPregunta = "CorazonPelican.Prorroga.MostrarPregunta";
        public const string MsgProrrogaResponderCarta = "CorazonPelican.Prorroga.ResponderCarta";
        public const string MsgProrrogaInvalidarMail = "CorazonPelican.Prorroga.InvalidarMail";

        public static string ConstruirReferenciaGranjero(long playerId)
            => IdentidadesObservacion.Granjero(playerId);

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
}
