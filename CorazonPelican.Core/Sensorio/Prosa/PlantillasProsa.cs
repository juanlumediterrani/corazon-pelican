using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Sensorio.Prosa
{
    /// <summary>
    /// Plantillas deterministas de un perfil para construir escenas, prosa de acción y huella de
    /// atención en segunda persona y presente (GSS §3.4, §4.4; ADR-0002). Se cargan desde el
    /// fichero <c>plantillas</c> del personaje sin codificar voz propia en el motor.
    /// </summary>
    [DataContract]
    public sealed class PlantillasProsa
    {
        public const int MinimoVariantesFoco = 3;

        public static readonly IReadOnlyList<string> EstacionesRequeridas = new[]
        {
            "primavera", "verano", "otono", "invierno",
        };

        public static readonly IReadOnlyList<string> FranjasRequeridas = new[]
        {
            "madrugada", "manana", "mediodia", "tarde", "atardecer", "noche",
        };

        public static readonly IReadOnlyList<string> NivelesLuzRequeridos = new[]
        {
            "clara", "penumbra", "oscura",
        };

        public static readonly IReadOnlyList<string> CategoriasRequeridas = new[]
        {
            "granjero", "npc", "animal", "objeto", "terreno", "suceso",
        };

        [DataMember(Name = "estaciones")]
        [JsonPropertyName("estaciones")]
        public Dictionary<string, string> Estaciones { get; set; } = new();

        [DataMember(Name = "franjas_horarias")]
        [JsonPropertyName("franjas_horarias")]
        public Dictionary<string, List<string>> FranjasHorarias { get; set; } = new();

        [DataMember(Name = "climas")]
        [JsonPropertyName("climas")]
        public Dictionary<string, List<string>> Climas { get; set; } = new();

        [DataMember(Name = "luz")]
        [JsonPropertyName("luz")]
        public Dictionary<string, List<string>> Luz { get; set; } = new();

        [DataMember(Name = "lugar")]
        [JsonPropertyName("lugar")]
        public PlantillasLugar Lugar { get; set; } = new();

        [DataMember(Name = "energia")]
        [JsonPropertyName("energia")]
        public Dictionary<string, List<string>> Energia { get; set; } = new();

        [DataMember(Name = "obra_en_curso")]
        [JsonPropertyName("obra_en_curso")]
        public List<string> ObraEnCurso { get; set; } = new();

        [DataMember(Name = "intencion_activa")]
        [JsonPropertyName("intencion_activa")]
        public List<string> IntencionActiva { get; set; } = new();

        [DataMember(Name = "entidades")]
        [JsonPropertyName("entidades")]
        public Dictionary<string, PlantillasCategoria> Entidades { get; set; } = new();

        [DataMember(Name = "acciones")]
        [JsonPropertyName("acciones")]
        public Dictionary<string, List<string>> Acciones { get; set; } = new();

        [DataMember(Name = "percepcion_bufer")]
        [JsonPropertyName("percepcion_bufer")]
        public Dictionary<string, List<string>> PercepcionBufer { get; set; } = new();

        [DataMember(Name = "huella")]
        [JsonPropertyName("huella")]
        public PlantillasHuella Huella { get; set; } = new();

        public static PlantillasProsa DesdeJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("El JSON de plantillas está vacío.", nameof(json));
            }

            var plantillas = JsonSerializer.Deserialize<PlantillasProsa>(json);
            return plantillas ?? throw new InvalidOperationException("No se pudieron deserializar las plantillas de prosa.");
        }

        /// <summary>
        /// Valida que el juego de plantillas cubra categorías, bandas (≥3 variantes en foco), franjas,
        /// clima, luz, acciones y huella sin entradas vacías.
        /// </summary>
        public static ResultadoValidacionPerfil Validar(PlantillasProsa plantillas)
        {
            var r = new ResultadoValidacionPerfil();
            ValidarDiccionarioTextos("estaciones", plantillas.Estaciones, EstacionesRequeridas, r);
            ValidarGrupos("franjas_horarias", plantillas.FranjasHorarias, FranjasRequeridas, minimoPorGrupo: 1, r);
            ValidarClimas(plantillas.Climas, r);
            ValidarGrupos("luz", plantillas.Luz, NivelesLuzRequeridos, minimoPorGrupo: 1, r);
            ValidarVariantes("lugar.actual", plantillas.Lugar.Actual, minimo: 1, r);
            ValidarEntidades(plantillas.Entidades, r);
            ValidarGrupos("acciones", plantillas.Acciones, Intencion.Acciones.VocabularioV1, minimoPorGrupo: 1, r);
            ValidarGrupos("percepcion_bufer", plantillas.PercepcionBufer, CategoriasRequeridas, minimoPorGrupo: 1, r);
            ValidarHuella(plantillas.Huella, r);
            return r;
        }

        /// <summary>
        /// Selección determinista (R-01): combina una semilla estable sobre <paramref name="clave"/> con
        /// el <paramref name="dia"/> para evitar aleatoriedad y garantizar rotación en días distintos.
        /// </summary>
        public static string ElegirVariante(IReadOnlyList<string> variantes, int dia, string clave)
        {
            if (variantes.Count == 0)
            {
                throw new InvalidOperationException($"No hay variantes disponibles para «{clave}».");
            }

            if (variantes.Count == 1)
            {
                return variantes[0];
            }

            var desplazamiento = HashEstable(clave);
            var diaNormalizado = (uint)Math.Max(0, dia);
            var indice = (int)((desplazamiento + diaNormalizado) % (uint)variantes.Count);
            return variantes[indice];
        }

        public static string ClaveCategoria(CategoriaEntidad categoria) => categoria switch
        {
            CategoriaEntidad.Granjero => "granjero",
            CategoriaEntidad.Npc => "npc",
            CategoriaEntidad.Animal => "animal",
            CategoriaEntidad.Objeto => "objeto",
            CategoriaEntidad.Terreno => "terreno",
            CategoriaEntidad.Suceso => "suceso",
            _ => throw new ArgumentOutOfRangeException(nameof(categoria), categoria, null),
        };

        public static string ClaveFranja(int hora) => hora switch
        {
            < 600 or >= 2400 => "madrugada",
            < 1200 => "manana",
            < 1500 => "mediodia",
            < 1730 => "tarde",
            < 2000 => "atardecer",
            _ => "noche",
        };

        public static string ClaveLuz(double luz)
        {
            if (luz >= 0.75)
            {
                return "clara";
            }

            return luz >= 0.35 ? "penumbra" : "oscura";
        }

        public static (string Clave, int DiaEstacion) EstacionDe(int dia)
        {
            var diaPositivo = Math.Max(1, dia);
            var indiceEstacion = ((diaPositivo - 1) / 28) % 4;
            var diaEstacion = ((diaPositivo - 1) % 28) + 1;
            var clave = EstacionesRequeridas[indiceEstacion];
            return (clave, diaEstacion);
        }

        internal string NombreEstacion(string clave)
            => this.Estaciones.TryGetValue(clave, out var nombre) && !string.IsNullOrWhiteSpace(nombre) ? nombre : clave;

        internal IReadOnlyList<string> VariantesClima(string clima)
        {
            if (!string.IsNullOrWhiteSpace(clima) && this.Climas.TryGetValue(clima, out var especificas) && especificas.Count > 0)
            {
                return especificas;
            }

            if (this.Climas.TryGetValue("por_defecto", out var porDefecto) && porDefecto.Count > 0)
            {
                return porDefecto;
            }

            return Array.Empty<string>();
        }

        /// <summary>FNV-1a de 32 bits: estable entre procesos, plataformas y reinicios (nunca <c>string.GetHashCode</c>).</summary>
        private static uint HashEstable(string clave)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (var ch in clave)
                {
                    hash ^= ch;
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static void ValidarDiccionarioTextos(
            string campo,
            IReadOnlyDictionary<string, string> mapa,
            IEnumerable<string> requeridas,
            ResultadoValidacionPerfil r)
        {
            foreach (var clave in requeridas)
            {
                if (!mapa.TryGetValue(clave, out var valor) || string.IsNullOrWhiteSpace(valor))
                {
                    r.Errores.Add(new($"{campo}.{clave}", $"falta el nombre de «{clave}»."));
                }
            }
        }

        private static void ValidarClimas(IReadOnlyDictionary<string, List<string>> climas, ResultadoValidacionPerfil r)
        {
            if (climas.Count == 0)
            {
                r.Errores.Add(new("climas", "faltan las plantillas de clima."));
                return;
            }

            if (!climas.ContainsKey("por_defecto"))
            {
                r.Errores.Add(new("climas.por_defecto", "falta la plantilla «por_defecto» para climas no listados."));
            }

            foreach (var (clave, variantes) in climas)
            {
                ValidarVariantes($"climas.{clave}", variantes, minimo: 1, r);
            }
        }

        private static void ValidarGrupos(
            string campo,
            IReadOnlyDictionary<string, List<string>> grupos,
            IEnumerable<string> requeridas,
            int minimoPorGrupo,
            ResultadoValidacionPerfil r)
        {
            foreach (var clave in requeridas)
            {
                if (!grupos.TryGetValue(clave, out var variantes))
                {
                    r.Errores.Add(new($"{campo}.{clave}", $"falta la entrada «{clave}»."));
                    continue;
                }

                ValidarVariantes($"{campo}.{clave}", variantes, minimoPorGrupo, r);
            }
        }

        private static void ValidarEntidades(IReadOnlyDictionary<string, PlantillasCategoria> entidades, ResultadoValidacionPerfil r)
        {
            foreach (var categoria in CategoriasRequeridas)
            {
                if (!entidades.TryGetValue(categoria, out var grupo))
                {
                    r.Errores.Add(new($"entidades.{categoria}", $"faltan las plantillas de la categoría «{categoria}»."));
                    continue;
                }

                ValidarVariantes($"entidades.{categoria}.foco", grupo.Foco, MinimoVariantesFoco, r);
                foreach (var variante in grupo.Foco)
                {
                    if (!variante.Contains("{nombre}", StringComparison.Ordinal))
                    {
                        r.Errores.Add(new($"entidades.{categoria}.foco", "cada variante de foco debe incluir {nombre} para coincidir con el apéndice (R-02)."));
                    }
                }

                ValidarVariantes($"entidades.{categoria}.mencion", grupo.Mencion, minimo: 1, r);
                foreach (var variante in grupo.Mencion)
                {
                    if (!variante.Contains("{nombre}", StringComparison.Ordinal))
                    {
                        r.Errores.Add(new($"entidades.{categoria}.mencion", "cada variante de mención debe incluir {nombre} para coincidir con el apéndice (R-02)."));
                    }
                }

                if (string.IsNullOrWhiteSpace(grupo.Periferia.Referencia))
                {
                    r.Errores.Add(new($"entidades.{categoria}.periferia.referencia", "falta la referencia vaga de periferia."));
                }

                ValidarVariantes($"entidades.{categoria}.periferia.variantes", grupo.Periferia.Variantes, minimo: 1, r);
                foreach (var variante in grupo.Periferia.Variantes)
                {
                    if (variante.Contains("{nombre}", StringComparison.Ordinal))
                    {
                        r.Errores.Add(new($"entidades.{categoria}.periferia.variantes", "la periferia no puede usar {nombre}: debe mantener vaguedad (R-03)."));
                    }

                    if (!variante.Contains("{referencia}", StringComparison.Ordinal))
                    {
                        r.Errores.Add(new($"entidades.{categoria}.periferia.variantes", "cada variante de periferia debe incluir {referencia} para coincidir con el apéndice (R-02)."));
                    }
                }
            }
        }

        private static void ValidarHuella(PlantillasHuella huella, ResultadoValidacionPerfil r)
        {
            ValidarVariantes("huella.una_entidad", huella.UnaEntidad, minimo: 1, r);
            ValidarVariantes("huella.varias_entidades", huella.VariasEntidades, minimo: 1, r);
            ValidarVariantes("huella.sin_entidades", huella.SinEntidades, minimo: 1, r);
        }

        private static void ValidarVariantes(string campo, IReadOnlyList<string> variantes, int minimo, ResultadoValidacionPerfil r)
        {
            if (variantes.Count < minimo)
            {
                r.Errores.Add(new(campo, $"necesita al menos {minimo} variante(s); tiene {variantes.Count}."));
            }

            for (var i = 0; i < variantes.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(variantes[i]))
                {
                    r.Errores.Add(new($"{campo}[{i}]", "la variante no puede estar vacía."));
                }
            }
        }
    }

    [DataContract]
    public sealed class PlantillasLugar
    {
        [DataMember(Name = "actual")]
        [JsonPropertyName("actual")]
        public List<string> Actual { get; set; } = new();

        [DataMember(Name = "sin_lugar")]
        [JsonPropertyName("sin_lugar")]
        public List<string> SinLugar { get; set; } = new();
    }

    [DataContract]
    public sealed class PlantillasCategoria
    {
        [DataMember(Name = "foco")]
        [JsonPropertyName("foco")]
        public List<string> Foco { get; set; } = new();

        [DataMember(Name = "mencion")]
        [JsonPropertyName("mencion")]
        public List<string> Mencion { get; set; } = new();

        [DataMember(Name = "periferia")]
        [JsonPropertyName("periferia")]
        public PlantillaPeriferia Periferia { get; set; } = new();
    }

    [DataContract]
    public sealed class PlantillaPeriferia
    {
        [DataMember(Name = "referencia")]
        [JsonPropertyName("referencia")]
        public string Referencia { get; set; } = "";

        [DataMember(Name = "variantes")]
        [JsonPropertyName("variantes")]
        public List<string> Variantes { get; set; } = new();
    }

    [DataContract]
    public sealed class PlantillasHuella
    {
        [DataMember(Name = "una_entidad")]
        [JsonPropertyName("una_entidad")]
        public List<string> UnaEntidad { get; set; } = new();

        [DataMember(Name = "varias_entidades")]
        [JsonPropertyName("varias_entidades")]
        public List<string> VariasEntidades { get; set; } = new();

        [DataMember(Name = "sin_entidades")]
        [JsonPropertyName("sin_entidades")]
        public List<string> SinEntidades { get; set; } = new();
    }
}
