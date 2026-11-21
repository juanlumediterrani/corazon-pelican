using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CorazonPelican.Core.Contrato
{
    /// <summary>Pulso (motor → mente), ADS §3.3. Espejo de <c>schemas/pulso.schema.json</c>.</summary>
    public sealed class Pulso
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("epoca")] public string Epoca { get; set; } = "";
        [JsonPropertyName("personaje")] public string Personaje { get; set; } = "";
        [JsonPropertyName("motivo")] public string Motivo { get; set; } = "";
        [JsonPropertyName("anticipado")] public bool? Anticipado { get; set; }
        [JsonPropertyName("tiempo_juego")] public TiempoJuego TiempoJuego { get; set; } = new();
        [JsonPropertyName("tiempo_real_utc")] public string? TiempoRealUtc { get; set; }
        [JsonPropertyName("hito")] public HitoPulso? Hito { get; set; }
        [JsonPropertyName("escena")] public string Escena { get; set; } = "";

        /// <summary>Hechos duros del apéndice; abierto a hechos de contexto de cada hito.</summary>
        [JsonPropertyName("hechos")] public Dictionary<string, JsonElement> Hechos { get; set; } = new();

        [JsonPropertyName("acciones_disponibles")] public List<string> AccionesDisponibles { get; set; } = new();
        [JsonPropertyName("lugares")] public List<ParIdProsa> Lugares { get; set; } = new();
        [JsonPropertyName("obras")] public List<ObraCitable>? Obras { get; set; }
        [JsonPropertyName("bufer")] public List<EntradaBufer> Bufer { get; set; } = new();
        [JsonPropertyName("huella")] public Huella Huella { get; set; } = new();
        [JsonPropertyName("intencion_activa")] public IntencionActiva? IntencionActiva { get; set; }
        [JsonPropertyName("agenda")] public List<EntradaAgenda> Agenda { get; set; } = new();
        [JsonPropertyName("compromisos_proximos")] public List<CompromisoProximo>? CompromisosProximos { get; set; }
        [JsonPropertyName("episodios_diario")] public Dictionary<string, string>? EpisodiosDiario { get; set; }
        [JsonPropertyName("diario_dia")] public List<string>? DiarioDia { get; set; }
        [JsonPropertyName("recuerdos")] public List<string>? Recuerdos { get; set; }
        [JsonPropertyName("humor")] public HumorVigente? Humor { get; set; }
        [JsonPropertyName("interlocutor")] public Interlocutor? Interlocutor { get; set; }
        [JsonPropertyName("caduca_en_ms")] public int? CaducaEnMs { get; set; }
    }

    public sealed class HitoPulso
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("origen")] public string Origen { get; set; } = "";
        [JsonPropertyName("forma")] public string Forma { get; set; } = "";
    }

    public sealed class ObraCitable
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
        [JsonPropertyName("estado")] public string Estado { get; set; } = "";
    }

    public sealed class EntradaBufer
    {
        [JsonPropertyName("hora")] public int Hora { get; set; }
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
        [JsonPropertyName("saliencia")] public double Saliencia { get; set; }
    }

    /// <summary>Atención acumulada desde el último pensamiento (GSS §4.4, P-8).</summary>
    public sealed class Huella
    {
        [JsonPropertyName("desde_ultimo_pensamiento_min_juego")] public int DesdeUltimoPensamientoMinJuego { get; set; }
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
        [JsonPropertyName("entidades")] public List<AtencionEntidad> Entidades { get; set; } = new();
    }

    public sealed class AtencionEntidad
    {
        [JsonPropertyName("ref")] public string Ref { get; set; } = "";
        [JsonPropertyName("atencion")] public double Atencion { get; set; }
    }

    public sealed class IntencionActiva
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("objetivo")] public string Objetivo { get; set; } = "";
    }

    public sealed class EntradaAgenda
    {
        [JsonPropertyName("hito")] public string Hito { get; set; } = "";
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
        [JsonPropertyName("estado")] public string Estado { get; set; } = "";
    }

    public sealed class CompromisoProximo
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("con")] public string Con { get; set; } = "";
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
        [JsonPropertyName("plazo_juego")] public TiempoJuego PlazoJuego { get; set; } = new();
    }

    public sealed class Interlocutor
    {
        [JsonPropertyName("ref")] public string Ref { get; set; } = "";
        [JsonPropertyName("texto")] public string Texto { get; set; } = "";
    }
}
