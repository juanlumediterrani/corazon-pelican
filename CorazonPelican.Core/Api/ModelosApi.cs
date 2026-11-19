using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Api
{
    /// <summary>Respuesta de <c>GET /api/v1/pulsos</c> (ADS §5).</summary>
    public sealed class RespuestaPulsos
    {
        [JsonPropertyName("epoca")] public string Epoca { get; set; } = string.Empty;
        [JsonPropertyName("pulsos")] public List<Pulso> Pulsos { get; set; } = new();
    }

    /// <summary>Entrada de solo añadir del registro de sucesos del motor (ADS §5).</summary>
    public sealed class EventoMotor
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("epoca")] public string Epoca { get; set; } = string.Empty;
        [JsonPropertyName("tipo")] public string Tipo { get; set; } = string.Empty;
        [JsonPropertyName("personaje")] public string? Personaje { get; set; }
        [JsonPropertyName("tiempo_real_utc")] public string TiempoRealUtc { get; set; } = string.Empty;
        [JsonPropertyName("resumen")] public string Resumen { get; set; } = string.Empty;
        [JsonPropertyName("datos")] public Dictionary<string, object?>? Datos { get; set; }
    }

    /// <summary>Respuesta paginada por cursor de <c>GET /api/v1/eventos?since=N</c> (ADS §5).</summary>
    public sealed class RespuestaEventos
    {
        [JsonPropertyName("epoca")] public string? Epoca { get; set; }
        [JsonPropertyName("since")] public long Since { get; set; }
        [JsonPropertyName("ultimo_id")] public long UltimoId { get; set; }
        [JsonPropertyName("eventos")] public List<EventoMotor> Eventos { get; set; } = new();
    }

    /// <summary>Instantánea de memoria de trabajo para <c>GET /api/v1/memoria/{personaje}</c> (ADS §5).</summary>
    public sealed class InstantaneaMemoria
    {
        [JsonPropertyName("epoca")] public string Epoca { get; set; } = string.Empty;
        [JsonPropertyName("personaje")] public string Personaje { get; set; } = string.Empty;
        [JsonPropertyName("diario")] public List<EntradaDiarioMemoria> Diario { get; set; } = new();
        [JsonPropertyName("recuerdos")] public List<RecuerdoMemoria> Recuerdos { get; set; } = new();
        [JsonPropertyName("relaciones")] public Dictionary<string, RelacionMemoria> Relaciones { get; set; } = new();
        [JsonPropertyName("compromisos")] public List<CompromisoProximo> Compromisos { get; set; } = new();
        [JsonPropertyName("portafolio")] public List<Obra> Portafolio { get; set; } = new();
    }

    public sealed class EntradaDiarioMemoria
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("dia_juego")] public int DiaJuego { get; set; }
        [JsonPropertyName("hora")] public int Hora { get; set; }
        [JsonPropertyName("resumen_prosa")] public string ResumenProsa { get; set; } = string.Empty;
        [JsonPropertyName("saliencia")] public double Saliencia { get; set; }
        [JsonPropertyName("entidades")] public List<string> Entidades { get; set; } = new();
    }

    public sealed class RecuerdoMemoria
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("texto")] public string Texto { get; set; } = string.Empty;
        [JsonPropertyName("importancia")] public double Importancia { get; set; }
        [JsonPropertyName("origen_diario")] public List<string> OrigenDiario { get; set; } = new();
    }

    public sealed class RelacionMemoria
    {
        [JsonPropertyName("impresion")] public string Impresion { get; set; } = string.Empty;
        [JsonPropertyName("ultima_vez")] public TiempoJuego? UltimaVez { get; set; }
    }

    /// <summary>Respuesta de depuración de <c>GET /api/v1/escena/{personaje}</c> (ADS §5).</summary>
    public sealed class EscenaActual
    {
        [JsonPropertyName("epoca")] public string Epoca { get; set; } = string.Empty;
        [JsonPropertyName("personaje")] public string Personaje { get; set; } = string.Empty;
        [JsonPropertyName("observacion_disponible")] public bool ObservacionDisponible { get; set; }
        [JsonPropertyName("tiempo_juego")] public TiempoJuego? TiempoJuego { get; set; }
        [JsonPropertyName("ubicacion")] public string? Ubicacion { get; set; }
        [JsonPropertyName("escena")] public string Escena { get; set; } = string.Empty;
        [JsonPropertyName("hechos")] public Dictionary<string, JsonElement>? Hechos { get; set; }
    }

    /// <summary>Carga útil estándar de error HTTP (p. ej. 401, 403, 404, 409).</summary>
    public sealed class ErrorApi
    {
        [JsonPropertyName("error")] public string Error { get; set; } = string.Empty;
        [JsonPropertyName("epoca")] public string? Epoca { get; set; }
        [JsonPropertyName("mundo_cargado")] public bool? MundoCargado { get; set; }
    }
}
