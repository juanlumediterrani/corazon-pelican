using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CorazonPelican.Core.Contrato
{
    /// <summary>Momento del juego: día desde el inicio de la partida y hora (600 = 6:00, 2600 = 2:00).</summary>
    public sealed class TiempoJuego
    {
        [JsonPropertyName("dia")] public int Dia { get; set; }
        [JsonPropertyName("hora")] public int Hora { get; set; }
    }

    /// <summary>Par del apéndice: el id que usa la respuesta y la prosa que usa la escena (P-11).</summary>
    public sealed class ParIdProsa
    {
        [JsonPropertyName("id")] public string Id { get; set; } = "";
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
    }

    /// <summary>
    /// Una acción del vocabulario v1 con argumentos del léxico. No tiene coordenadas, casillas ni
    /// rutas (R-13): el cuerpo decide el cómo.
    /// </summary>
    public sealed class PasoPlan
    {
        [JsonPropertyName("accion")] public string Accion { get; set; } = "";
        [JsonPropertyName("lugar")] public string? Lugar { get; set; }
        [JsonPropertyName("objetivo")] public string? Objetivo { get; set; }
        [JsonPropertyName("obra")] public string? Obra { get; set; }
        [JsonPropertyName("tipo")] public string? Tipo { get; set; }
        [JsonPropertyName("texto")] public string? Texto { get; set; }
        [JsonPropertyName("duracion_min")] public int? DuracionMin { get; set; }

        /// <summary>Captura propiedades no permitidas por el esquema (p. ej. coordenadas) para poder rechazarlas en Core (R-13).</summary>
        [JsonExtensionData] public Dictionary<string, JsonElement>? CamposExtra { get; set; }
    }

    /// <summary>Decisión de una obra (GSS §6.1): todo prosa, el taller la convierte en imagen.</summary>
    public sealed class DecisionObra
    {
        [JsonPropertyName("titulo")] public string Titulo { get; set; } = "";
        [JsonPropertyName("tema")] public string Tema { get; set; } = "";
        [JsonPropertyName("composicion")] public string Composicion { get; set; } = "";
        [JsonPropertyName("paleta")] public string Paleta { get; set; } = "";
        [JsonPropertyName("nota")] public string Nota { get; set; } = "";
    }

    /// <summary>Motivo de rechazo en dos formas: código para el arnés, prosa para la mente (ADS §4.1).</summary>
    public sealed class MotivoRechazo
    {
        public MotivoRechazo() { }

        public MotivoRechazo(string codigo, string campo, string prosa)
        {
            this.Codigo = codigo;
            this.Campo = campo;
            this.Prosa = prosa;
        }

        [JsonPropertyName("codigo")] public string Codigo { get; set; } = "";
        [JsonPropertyName("campo")] public string Campo { get; set; } = "";
        [JsonPropertyName("prosa")] public string Prosa { get; set; } = "";
    }

    /// <summary>Impresión emocional vigente custodiada por el host (P-9, CP-210).</summary>
    public sealed class HumorVigente
    {
        [JsonPropertyName("resumen")] public string Resumen { get; set; } = "";
        [JsonPropertyName("intensidad")] public double Intensidad { get; set; }
        [JsonPropertyName("intensidad_inicial")] public double? IntensidadInicial { get; set; }
        [JsonPropertyName("origen_diario")] public List<string> OrigenDiario { get; set; } = new();
        [JsonPropertyName("creado_en")] public TiempoJuego? CreadoEn { get; set; }
    }

    /// <summary>Impresión emocional propuesta por la mente vinculada a episodios vividos (P-9, CP-210).</summary>
    public sealed class HumorPropuesto
    {
        [JsonPropertyName("resumen")] public string Resumen { get; set; } = "";
        [JsonPropertyName("intensidad")] public double Intensidad { get; set; }
        [JsonPropertyName("origen_diario")] public List<string> OrigenDiario { get; set; } = new();
        [JsonPropertyName("significativa")] public bool? Significativa { get; set; }
    }
}
