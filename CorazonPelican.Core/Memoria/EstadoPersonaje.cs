using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Estado persistido por personaje junto con la partida (ADS §3.2, P-6).</summary>
    public sealed class EstadoPersonaje
    {
        public const int VersionActual = 1;

        [JsonPropertyName("version_esquema")]
        public int VersionEsquema { get; set; } = VersionActual;

        [JsonPropertyName("siguiente_id_episodio")]
        public int SiguienteIdEpisodio { get; set; } = 1;

        [JsonPropertyName("diario")]
        public List<EpisodioDiario> Diario { get; set; } = new();

        [JsonPropertyName("recuerdos")]
        public List<Recuerdo> Recuerdos { get; set; } = new();

        [JsonPropertyName("relaciones")]
        public Dictionary<string, RelacionEstado> Relaciones { get; set; } = new();

        [JsonPropertyName("portafolio")]
        public List<ObraPortafolio> Portafolio { get; set; } = new();

        [JsonPropertyName("compromisos")]
        public List<CompromisoEstado> Compromisos { get; set; } = new();

        [JsonPropertyName("contadores")]
        public ContadoresPulsos? Contadores { get; set; }

        [JsonPropertyName("intencion_activa")]
        public IntencionActiva? IntencionActiva { get; set; }

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
    }

    public sealed class EpisodioDiario
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("dia_juego")]
        public int DiaJuego { get; set; }

        [JsonPropertyName("hora")]
        public int Hora { get; set; }

        [JsonPropertyName("resumen_prosa")]
        public string ResumenProsa { get; set; } = "";

        [JsonPropertyName("saliencia")]
        public double Saliencia { get; set; }

        [JsonPropertyName("entidades")]
        public List<string> Entidades { get; set; } = new();
    }

    public sealed class Recuerdo
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("texto")]
        public string Texto { get; set; } = "";

        [JsonPropertyName("importancia")]
        public double Importancia { get; set; }

        [JsonPropertyName("origen_diario")]
        public List<string> OrigenDiario { get; set; } = new();
    }

    public sealed class RelacionEstado
    {
        [JsonPropertyName("impresion")]
        public string Impresion { get; set; } = "";

        [JsonPropertyName("ultima_vez")]
        public TiempoJuego? UltimaVez { get; set; }
    }

    public sealed class ObraPortafolio
    {
        [JsonPropertyName("obra_id")]
        public string ObraId { get; set; } = "";

        [JsonPropertyName("estado")]
        public string Estado { get; set; } = "";

        [JsonPropertyName("decision")]
        public DecisionObra? Decision { get; set; }

        [JsonPropertyName("nota")]
        public string Nota { get; set; } = "";

        [JsonPropertyName("escena_origen")]
        public string EscenaOrigen { get; set; } = "";

        [JsonPropertyName("png_base64")]
        public string? PngBase64 { get; set; }

        [JsonPropertyName("destinatario")]
        public string? Destinatario { get; set; }
    }

    public sealed class CompromisoEstado
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("tipo")]
        public string Tipo { get; set; } = "";

        [JsonPropertyName("destinatario")]
        public string Destinatario { get; set; } = "";

        [JsonPropertyName("objeto")]
        public string Objeto { get; set; } = "";

        [JsonPropertyName("plazo_vigente")]
        public TiempoJuego PlazoVigente { get; set; } = new();

        [JsonPropertyName("estado")]
        public string Estado { get; set; } = "";

        [JsonPropertyName("plazo_propuesto")]
        public TiempoJuego? PlazoPropuesto { get; set; }
    }
}
