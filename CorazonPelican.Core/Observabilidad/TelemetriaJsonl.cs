using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;

namespace CorazonPelican.Core.Observabilidad
{
    /// <summary>Entrada de escena registrada en <c>escenas.jsonl</c>.</summary>
    public sealed class RegistroEscenaTelemetria
    {
        [JsonPropertyName("tiempo_real_utc")] public string TiempoRealUtc { get; set; } = string.Empty;
        [JsonPropertyName("epoca")] public string? Epoca { get; set; }
        [JsonPropertyName("personaje")] public string Personaje { get; set; } = string.Empty;
        [JsonPropertyName("origen")] public string Origen { get; set; } = string.Empty;
        [JsonPropertyName("pulso_id")] public long? PulsoId { get; set; }
        [JsonPropertyName("tiempo_juego")] public TiempoJuego TiempoJuego { get; set; } = new();
        [JsonPropertyName("ubicacion")] public string? Ubicacion { get; set; }
        [JsonPropertyName("escena")] public string Escena { get; set; } = string.Empty;
        [JsonPropertyName("texto_completo")] public string TextoCompleto { get; set; } = string.Empty;
        [JsonPropertyName("hechos")] public Dictionary<string, JsonElement>? Hechos { get; set; }
    }

    /// <summary>Entrada de respuesta registrada en <c>respuestas.jsonl</c>.</summary>
    public sealed class RegistroRespuestaTelemetria
    {
        [JsonPropertyName("tiempo_real_utc")] public string TiempoRealUtc { get; set; } = string.Empty;
        [JsonPropertyName("personaje")] public string? Personaje { get; set; }
        [JsonPropertyName("respuesta")] public Respuesta Respuesta { get; set; } = new();
    }

    /// <summary>Entrada de rechazo registrada en <c>rechazos.jsonl</c>.</summary>
    public sealed class RegistroRechazoTelemetria
    {
        [JsonPropertyName("tiempo_real_utc")] public string TiempoRealUtc { get; set; } = string.Empty;
        [JsonPropertyName("epoca")] public string? Epoca { get; set; }
        [JsonPropertyName("personaje")] public string? Personaje { get; set; }
        [JsonPropertyName("basada_en")] public long? BasadaEn { get; set; }
        [JsonPropertyName("motivos")] public List<MotivoRechazo> Motivos { get; set; } = new();
    }

    /// <summary>Entrada de acto registrada en <c>actos.jsonl</c>.</summary>
    public sealed class RegistroActoTelemetria
    {
        [JsonPropertyName("tiempo_real_utc")] public string TiempoRealUtc { get; set; } = string.Empty;
        [JsonPropertyName("personaje")] public string Personaje { get; set; } = string.Empty;
        [JsonPropertyName("tiempo_juego")] public TiempoJuego TiempoJuego { get; set; } = new();
        [JsonPropertyName("aceptado")] public bool Aceptado { get; set; }
        [JsonPropertyName("detalle")] public string? Detalle { get; set; }
        [JsonPropertyName("acto")] public Acto Acto { get; set; } = new();
    }

    /// <summary>Instantánea de costes del bucle registrada en <c>costes.jsonl</c>.</summary>
    public sealed class RegistroCostesTelemetria
    {
        [JsonPropertyName("tiempo_real_utc")] public string TiempoRealUtc { get; set; } = string.Empty;
        [JsonPropertyName("epoca")] public string? Epoca { get; set; }
        [JsonPropertyName("tiempo_juego")] public TiempoJuego TiempoJuego { get; set; } = new();
        [JsonPropertyName("pasos")] public List<MedicionPaso> Pasos { get; set; } = new();
    }

    /// <summary>
    /// Escritor append-only en UTF-8 (sin BOM) de telemetría JSONL bajo <c>{base}/{saveId}/</c> (ADS §12.2).
    /// Es solo registro para análisis y aceptación: ningún componente de memoria del personaje lee de ella.
    /// Cuando <see cref="Habilitada"/> es <c>false</c>, no crea directorios ni escribe ficheros en disco.
    /// </summary>
    public sealed class TelemetriaJsonl
    {
        public const string ArchivoEscenas = "escenas.jsonl";
        public const string ArchivoPulsos = "pulsos.jsonl";
        public const string ArchivoRespuestas = "respuestas.jsonl";
        public const string ArchivoRechazos = "rechazos.jsonl";
        public const string ArchivoActos = "actos.jsonl";
        public const string ArchivoCostes = "costes.jsonl";

        private static readonly Encoding Utf8SinBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        private static readonly JsonSerializerOptions JsonOpciones = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        private readonly object sync = new();

        public TelemetriaJsonl(string directorioBase, string saveId, bool habilitada = true)
        {
            if (habilitada)
            {
                if (string.IsNullOrWhiteSpace(directorioBase))
                {
                    throw new ArgumentException("El directorio base de telemetría no puede estar vacío.", nameof(directorioBase));
                }

                if (string.IsNullOrWhiteSpace(saveId))
                {
                    throw new ArgumentException("El identificador de partida (saveId) no puede estar vacío.", nameof(saveId));
                }
            }

            this.DirectorioBase = directorioBase ?? string.Empty;
            this.SaveId = saveId ?? string.Empty;
            this.Habilitada = habilitada;
            this.DirectorioPartida = string.IsNullOrWhiteSpace(this.DirectorioBase) || string.IsNullOrWhiteSpace(this.SaveId)
                ? string.Empty
                : Path.Combine(this.DirectorioBase, LimpiarSegmento(this.SaveId));
        }

        public string DirectorioBase { get; }

        public string SaveId { get; }

        public bool Habilitada { get; }

        public string DirectorioPartida { get; }

        public string RutaArchivo(string nombreArchivo)
            => string.IsNullOrEmpty(this.DirectorioPartida)
                ? nombreArchivo
                : Path.Combine(this.DirectorioPartida, nombreArchivo);

        public void RegistrarEscena(
            string personaje,
            EscenaGenerada escena,
            string? epoca = null,
            string? ubicacion = null,
            string origen = "pulso",
            long? pulsoId = null)
        {
            if (!this.Habilitada)
            {
                return;
            }

            if (escena is null)
            {
                throw new ArgumentNullException(nameof(escena));
            }

            var registro = new RegistroEscenaTelemetria
            {
                TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O"),
                Epoca = epoca,
                Personaje = personaje,
                Origen = origen,
                PulsoId = pulsoId,
                TiempoJuego = new TiempoJuego { Dia = escena.Apendice.Dia, Hora = escena.Apendice.Hora },
                Ubicacion = ubicacion,
                Escena = escena.Escena,
                TextoCompleto = escena.TextoCompleto,
                Hechos = escena.ConstruirHechosPulso(),
            };
            this.EscribirObjeto(ArchivoEscenas, registro);
        }

        public void RegistrarPulso(Pulso pulso)
        {
            if (!this.Habilitada)
            {
                return;
            }

            if (pulso is null)
            {
                throw new ArgumentNullException(nameof(pulso));
            }

            this.EscribirObjeto(ArchivoPulsos, pulso);
        }

        public void RegistrarRespuesta(Respuesta respuesta, string? personaje = null)
        {
            if (!this.Habilitada)
            {
                return;
            }

            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            var registro = new RegistroRespuestaTelemetria
            {
                TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O"),
                Personaje = personaje,
                Respuesta = respuesta,
            };
            this.EscribirObjeto(ArchivoRespuestas, registro);
        }

        public void RegistrarRechazo(string? personaje, long? basadaEn, Resultado resultado, string? epoca = null)
        {
            if (!this.Habilitada)
            {
                return;
            }

            if (resultado is null)
            {
                throw new ArgumentNullException(nameof(resultado));
            }

            var registro = new RegistroRechazoTelemetria
            {
                TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O"),
                Epoca = epoca,
                Personaje = personaje,
                BasadaEn = basadaEn,
                Motivos = resultado.Motivos is null ? new List<MotivoRechazo>() : new List<MotivoRechazo>(resultado.Motivos),
            };
            this.EscribirObjeto(ArchivoRechazos, registro);
        }

        public void RegistrarActo(string personaje, Acto acto, int dia, int hora, bool aceptado, string? detalle = null)
        {
            if (!this.Habilitada)
            {
                return;
            }

            if (acto is null)
            {
                throw new ArgumentNullException(nameof(acto));
            }

            var registro = new RegistroActoTelemetria
            {
                TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O"),
                Personaje = personaje,
                TiempoJuego = new TiempoJuego { Dia = dia, Hora = hora },
                Aceptado = aceptado,
                Detalle = detalle,
                Acto = acto,
            };
            this.EscribirObjeto(ArchivoActos, registro);
        }

        public void RegistrarCostes(IReadOnlyList<MedicionPaso> mediciones, int dia, int hora, string? epoca = null)
        {
            if (!this.Habilitada || mediciones is null || mediciones.Count == 0)
            {
                return;
            }

            var registro = new RegistroCostesTelemetria
            {
                TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O"),
                Epoca = epoca,
                TiempoJuego = new TiempoJuego { Dia = dia, Hora = hora },
                Pasos = new List<MedicionPaso>(mediciones),
            };
            this.EscribirObjeto(ArchivoCostes, registro);
        }

        private void EscribirObjeto<T>(string nombreArchivo, T valor)
        {
            if (!this.Habilitada)
            {
                return;
            }

            var linea = JsonSerializer.Serialize(valor, JsonOpciones);
            var ruta = Path.Combine(this.DirectorioPartida, nombreArchivo);
            lock (this.sync)
            {
                Directory.CreateDirectory(this.DirectorioPartida);
                File.AppendAllText(ruta, linea + "\n", Utf8SinBom);
            }
        }

        private static string LimpiarSegmento(string saveId)
        {
            var invalidos = Path.GetInvalidFileNameChars();
            var limpio = saveId.Trim();
            foreach (var c in invalidos)
            {
                limpio = limpio.Replace(c, '_');
            }

            return limpio;
        }
    }
}
