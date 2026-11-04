using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace CorazonPelican.Core.Contrato
{
    /// <summary>
    /// Respuesta (mente → motor), ADS §3.4. Espejo de <c>schemas/respuesta.schema.json</c>.
    /// Pensar en prosa, decidir en léxico (P-11): <see cref="Pensamiento"/> y los demás campos de
    /// prosa nunca se ejecutan; solo los campos efectivos llegan al cuerpo.
    /// </summary>
    public sealed class Respuesta
    {
        [JsonPropertyName("pensamiento")] public string Pensamiento { get; set; } = "";
        [JsonPropertyName("epoca")] public string Epoca { get; set; } = "";
        [JsonPropertyName("basada_en")] public long BasadaEn { get; set; }
        [JsonPropertyName("intencion")] public IntencionPropuesta? Intencion { get; set; }
        [JsonPropertyName("frase")] public string? Frase { get; set; }
        [JsonPropertyName("actos")] public List<Acto>? Actos { get; set; }
        [JsonPropertyName("memoria")] public MemoriaPropuesta? Memoria { get; set; }
        [JsonPropertyName("obra")] public ObraPropuesta? Obra { get; set; }
        [JsonPropertyName("agenda")] public List<EstadoAgenda>? Agenda { get; set; }
    }

    public sealed class IntencionPropuesta
    {
        [JsonPropertyName("objetivo")] public string Objetivo { get; set; } = "";
        [JsonPropertyName("porque")] public string Porque { get; set; } = "";
        [JsonPropertyName("plazo_juego")] public TiempoJuego? PlazoJuego { get; set; }
        [JsonPropertyName("pesos_atencion")] public Dictionary<string, double>? PesosAtencion { get; set; }
        [JsonPropertyName("plan")] public List<PasoPlan> Plan { get; set; } = new();
        [JsonPropertyName("cambios_rutina")] public List<CambioRutina>? CambiosRutina { get; set; }
    }

    /// <summary>Cambio sobre una entrada del horario de hoy (GSS §5.3, R-16).</summary>
    public sealed class CambioRutina
    {
        [JsonPropertyName("hito")] public string Hito { get; set; } = "";
        [JsonPropertyName("cambio")] public string Cambio { get; set; } = "";
        [JsonPropertyName("hora")] public int? Hora { get; set; }
        [JsonPropertyName("plan")] public List<PasoPlan>? Plan { get; set; }
    }

    /// <summary>
    /// Acto de habla (GSS §8.2). Un solo tipo con los campos de todos los actos; qué campos exige
    /// cada uno lo fija el esquema y lo comprueba el validador de actos (CP-501).
    /// </summary>
    public sealed class Acto
    {
        [JsonPropertyName("acto")] public string Tipo { get; set; } = "";
        [JsonPropertyName("obra")] public string? Obra { get; set; }
        [JsonPropertyName("obra_id")] public string? ObraId { get; set; }
        [JsonPropertyName("tema")] public string? Tema { get; set; }
        [JsonPropertyName("para")] public string? Para { get; set; }
        [JsonPropertyName("lugar")] public string? Lugar { get; set; }
        [JsonPropertyName("cuando")] public TiempoJuego? Cuando { get; set; }
        [JsonPropertyName("motivo")] public string? Motivo { get; set; }
    }

    public sealed class MemoriaPropuesta
    {
        [JsonPropertyName("recordar")] public List<string> Recordar { get; set; } = new();
    }

    public sealed class ObraPropuesta
    {
        [JsonPropertyName("formato")] public string Formato { get; set; } = "";
        [JsonPropertyName("decision")] public DecisionObra Decision { get; set; } = new();
    }

    public sealed class EstadoAgenda
    {
        [JsonPropertyName("hito")] public string Hito { get; set; } = "";
        [JsonPropertyName("estado")] public string Estado { get; set; } = "";
    }
}
