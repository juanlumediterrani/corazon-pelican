using System.Text.Json.Serialization;

namespace CorazonPelican.Core.Contrato
{
    /// <summary>Obra del portafolio, ADS §3.5. Espejo de <c>schemas/obra.schema.json</c>.</summary>
    public sealed class Obra
    {
        [JsonPropertyName("obra_id")] public string ObraId { get; set; } = "";
        [JsonPropertyName("formato")] public string Formato { get; set; } = "";
        [JsonPropertyName("decision")] public DecisionObra Decision { get; set; } = new();
        [JsonPropertyName("estado")] public string Estado { get; set; } = "";
        [JsonPropertyName("intentos")] public int Intentos { get; set; }
    }
}
