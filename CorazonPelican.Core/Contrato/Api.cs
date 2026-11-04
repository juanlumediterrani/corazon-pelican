using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CorazonPelican.Core.Contrato
{
    /// <summary>Resultado de <c>POST respuestas</c> (ADS §5). Espejo de <c>schemas/resultado.schema.json</c>.</summary>
    public sealed class Resultado
    {
        public const string Aceptada = "aceptada";
        public const string Rechazada = "rechazada";

        [JsonPropertyName("estado")] public string Estado { get; set; } = Aceptada;
        [JsonPropertyName("motivos")] public List<MotivoRechazo>? Motivos { get; set; }

        public static Resultado Rechazar(params MotivoRechazo[] motivos)
            => new() { Estado = Rechazada, Motivos = new List<MotivoRechazo>(motivos) };
    }

    /// <summary><c>GET salud</c> (ADS §5). Espejo de <c>schemas/salud.schema.json</c>.</summary>
    public sealed class Salud
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("contrato")] public string Contrato { get; set; } = CorazonCore.VersionContrato;
        [JsonPropertyName("mundo_cargado")] public bool MundoCargado { get; set; }
        [JsonPropertyName("epoca")] public string? Epoca { get; set; }
        [JsonPropertyName("personajes")] public List<string> Personajes { get; set; } = new();
    }
}
