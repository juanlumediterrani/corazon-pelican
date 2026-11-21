using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Estado persistido por personaje junto con la partida (ADS §3.2, P-6).</summary>
    [DataContract]
    public sealed class EstadoPersonaje
    {
        public const int VersionActual = 2;

        [JsonPropertyName("version_esquema")]
        [DataMember(Name = "version_esquema")]
        public int VersionEsquema { get; set; } = VersionActual;

        [JsonPropertyName("siguiente_id_episodio")]
        [DataMember(Name = "siguiente_id_episodio")]
        public int SiguienteIdEpisodio { get; set; } = 1;

        [JsonPropertyName("diario")]
        [DataMember(Name = "diario")]
        public List<EpisodioDiario> Diario { get; set; } = new();

        [JsonPropertyName("recuerdos")]
        [DataMember(Name = "recuerdos")]
        public List<Recuerdo> Recuerdos { get; set; } = new();

        [JsonPropertyName("relaciones")]
        [DataMember(Name = "relaciones")]
        public Dictionary<string, RelacionEstado> Relaciones { get; set; } = new();

        [JsonPropertyName("portafolio")]
        [DataMember(Name = "portafolio")]
        public List<ObraPortafolio> Portafolio { get; set; } = new();

        [JsonPropertyName("compromisos")]
        [DataMember(Name = "compromisos")]
        public List<CompromisoEstado> Compromisos { get; set; } = new();

        [JsonPropertyName("contadores")]
        [DataMember(Name = "contadores")]
        public ContadoresPulsos? Contadores { get; set; }

        [JsonPropertyName("intencion_activa")]
        [DataMember(Name = "intencion_activa")]
        public IntencionActiva? IntencionActiva { get; set; }

        [JsonPropertyName("humor")]
        [DataMember(Name = "humor")]
        public EstadoHumor? Humor { get; set; }

        /// <summary>
        /// Registra un nuevo episodio en el diario con identificador estable único para este personaje
        /// (referenciable en <c>episodios_diario</c> de CP-211 y persistido en CP-107).
        /// </summary>
        public EpisodioDiario AgregarEpisodio(
            int diaJuego,
            int hora,
            string resumenProsa,
            double saliencia,
            IEnumerable<string>? entidades = null)
        {
            if (diaJuego < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(diaJuego), "El día de juego debe ser ≥ 1.");
            }

            if (hora is < HoraJuego.InicioDia or > HoraJuego.FinDia)
            {
                throw new ArgumentOutOfRangeException(nameof(hora), $"La hora debe estar entre {HoraJuego.InicioDia} y {HoraJuego.FinDia}.");
            }

            if (string.IsNullOrWhiteSpace(resumenProsa))
            {
                throw new ArgumentException("El resumen del episodio no puede estar vacío.", nameof(resumenProsa));
            }

            if (saliencia < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(saliencia), "La saliencia no puede ser negativa.");
            }

            var existentes = new HashSet<string>(this.Diario.Select(e => e.Id), StringComparer.Ordinal);
            var numero = Math.Max(1, this.SiguienteIdEpisodio);
            string id;
            do
            {
                id = $"ep-{numero}";
                numero++;
            }
            while (!existentes.Add(id));

            this.SiguienteIdEpisodio = numero;

            var entidadesLista = entidades is null
                ? new List<string>()
                : entidades.Where(e => !string.IsNullOrWhiteSpace(e)).Distinct(StringComparer.Ordinal).ToList();

            var episodio = new EpisodioDiario
            {
                Id = id,
                DiaJuego = diaJuego,
                Hora = hora,
                ResumenProsa = resumenProsa,
                Saliencia = saliencia,
                Entidades = entidadesLista,
            };
            this.Diario.Add(episodio);
            return episodio;
        }

        /// <summary>
        /// Registra un nuevo recuerdo consolidado en el estado del personaje con identificador único
        /// y la lista deduplicada de episodios de origen (<c>origen_diario</c>).
        /// </summary>
        public Recuerdo AgregarRecuerdo(
            string texto,
            double importancia = 0.5,
            IEnumerable<string>? origenDiario = null)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                throw new ArgumentException("El texto del recuerdo no puede estar vacío.", nameof(texto));
            }

            if (importancia < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(importancia), "La importancia no puede ser negativa.");
            }

            var existentes = new HashSet<string>(this.Recuerdos.Select(r => r.Id), StringComparer.Ordinal);
            var numero = Math.Max(1, this.Recuerdos.Count + 1);
            foreach (var r in this.Recuerdos)
            {
                if (r.Id.StartsWith("rec-", StringComparison.Ordinal)
                    && int.TryParse(r.Id.AsSpan(4), out var n)
                    && n >= numero)
                {
                    numero = n + 1;
                }
            }

            string id;
            do
            {
                id = $"rec-{numero}";
                numero++;
            }
            while (!existentes.Add(id));

            var origenLista = origenDiario is null
                ? new List<string>()
                : origenDiario.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct(StringComparer.Ordinal).ToList();

            var recuerdo = new Recuerdo
            {
                Id = id,
                Texto = texto,
                Importancia = Math.Clamp(importancia, 0.0, 1.0),
                OrigenDiario = origenLista,
            };
            this.Recuerdos.Add(recuerdo);
            return recuerdo;
        }
    }

    [DataContract]
    public sealed class EpisodioDiario
    {
        [JsonPropertyName("id")]
        [DataMember(Name = "id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("dia_juego")]
        [DataMember(Name = "dia_juego")]
        public int DiaJuego { get; set; }

        [JsonPropertyName("hora")]
        [DataMember(Name = "hora")]
        public int Hora { get; set; }

        [JsonPropertyName("resumen_prosa")]
        [DataMember(Name = "resumen_prosa")]
        public string ResumenProsa { get; set; } = "";

        [JsonPropertyName("saliencia")]
        [DataMember(Name = "saliencia")]
        public double Saliencia { get; set; }

        [JsonPropertyName("entidades")]
        [DataMember(Name = "entidades")]
        public List<string> Entidades { get; set; } = new();
    }

    [DataContract]
    public sealed class Recuerdo
    {
        [JsonPropertyName("id")]
        [DataMember(Name = "id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("texto")]
        [DataMember(Name = "texto")]
        public string Texto { get; set; } = "";

        [JsonPropertyName("importancia")]
        [DataMember(Name = "importancia")]
        public double Importancia { get; set; }

        [JsonPropertyName("origen_diario")]
        [DataMember(Name = "origen_diario")]
        public List<string> OrigenDiario { get; set; } = new();
    }

    [DataContract]
    public sealed class RelacionEstado
    {
        [JsonPropertyName("impresion")]
        [DataMember(Name = "impresion")]
        public string Impresion { get; set; } = "";

        [JsonPropertyName("ultima_vez")]
        [DataMember(Name = "ultima_vez")]
        public TiempoJuego? UltimaVez { get; set; }
    }

    [DataContract]
    public sealed class ObraPortafolio
    {
        [JsonPropertyName("obra_id")]
        [DataMember(Name = "obra_id")]
        public string ObraId { get; set; } = "";

        [JsonPropertyName("estado")]
        [DataMember(Name = "estado")]
        public string Estado { get; set; } = "";

        [JsonPropertyName("decision")]
        [DataMember(Name = "decision")]
        public DecisionObra? Decision { get; set; }

        [JsonPropertyName("nota")]
        [DataMember(Name = "nota")]
        public string Nota { get; set; } = "";

        [JsonPropertyName("escena_origen")]
        [DataMember(Name = "escena_origen")]
        public string EscenaOrigen { get; set; } = "";

        [JsonPropertyName("png_base64")]
        [DataMember(Name = "png_base64")]
        public string? PngBase64 { get; set; }

        [JsonPropertyName("destinatario")]
        [DataMember(Name = "destinatario")]
        public string? Destinatario { get; set; }
    }

    [DataContract]
    public sealed class CompromisoEstado
    {
        [JsonPropertyName("id")]
        [DataMember(Name = "id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("tipo")]
        [DataMember(Name = "tipo")]
        public string Tipo { get; set; } = "";

        [JsonPropertyName("destinatario")]
        [DataMember(Name = "destinatario")]
        public string Destinatario { get; set; } = "";

        [JsonPropertyName("objeto")]
        [DataMember(Name = "objeto")]
        public string Objeto { get; set; } = "";

        [JsonPropertyName("plazo_vigente")]
        [DataMember(Name = "plazo_vigente")]
        public TiempoJuego PlazoVigente { get; set; } = new();

        [JsonPropertyName("estado")]
        [DataMember(Name = "estado")]
        public string Estado { get; set; } = "";

        [JsonPropertyName("plazo_propuesto")]
        [DataMember(Name = "plazo_propuesto")]
        public TiempoJuego? PlazoPropuesto { get; set; }
    }

    /// <summary>Estado persistido de la impresión emocional vigente del personaje (P-9, CP-210).</summary>
    [DataContract]
    public sealed class EstadoHumor
    {
        [JsonPropertyName("resumen")]
        [DataMember(Name = "resumen")]
        public string Resumen { get; set; } = "";

        [JsonPropertyName("intensidad_inicial")]
        [DataMember(Name = "intensidad_inicial")]
        public double IntensidadInicial { get; set; }

        [JsonPropertyName("origen_diario")]
        [DataMember(Name = "origen_diario")]
        public List<string> OrigenDiario { get; set; } = new();

        [JsonPropertyName("creado_en")]
        [DataMember(Name = "creado_en")]
        public TiempoJuego CreadoEn { get; set; } = new();

        [JsonPropertyName("epoca")]
        [DataMember(Name = "epoca")]
        public string? Epoca { get; set; }

        [JsonPropertyName("basada_en_pulso")]
        [DataMember(Name = "basada_en_pulso")]
        public long? BasadaEnPulso { get; set; }
    }
}
