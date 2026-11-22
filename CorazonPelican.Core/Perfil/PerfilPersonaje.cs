using System.Collections.Generic;
using System.Runtime.Serialization;

namespace CorazonPelican.Core.Perfil
{
    // Los perfiles los lee SMAPI (Newtonsoft.Json) y los edita Content Patcher, que respetan
    // [DataContract]/[DataMember]: así los nombres en JSON son los del esquema (snake_case) sin que
    // Core dependa de ninguna biblioteca. Espejo de schemas/perfil.schema.json (ADS §3.1).

    /// <summary>Datos que parametrizan el motor para un personaje (GSS §1.2). Sustituible sin tocar código.</summary>
    [DataContract]
    public sealed class PerfilPersonaje
    {
        [DataMember(Name = "id")] public string Id { get; set; } = "";
        [DataMember(Name = "npc")] public string Npc { get; set; } = "";
        [DataMember(Name = "rasgos")] public Dictionary<string, double> Rasgos { get; set; } = new();
        [DataMember(Name = "umbrales")] public Umbrales Umbrales { get; set; } = new();
        [DataMember(Name = "refractario")] public Refractario Refractario { get; set; } = new();
        [DataMember(Name = "presupuesto")] public Presupuesto Presupuesto { get; set; } = new();
        [DataMember(Name = "lexico")] public Lexico Lexico { get; set; } = new();
        [DataMember(Name = "plantillas")] public string Plantillas { get; set; } = "";
        [DataMember(Name = "apertura")] public List<TramoApertura> Apertura { get; set; } = new();
        [DataMember(Name = "voz")] public string Voz { get; set; } = "";
        [DataMember(Name = "rubrica_caracter", EmitDefaultValue = false)] public string? RubricaCaracter { get; set; }
        [DataMember(Name = "acciones")] public List<string> Acciones { get; set; } = new();
        [DataMember(Name = "formatos_obra")] public List<string> FormatosObra { get; set; } = new();
        [DataMember(Name = "rutina")] public Rutina Rutina { get; set; } = new();
        [DataMember(Name = "humor", EmitDefaultValue = false)] public ConfigHumor? Humor { get; set; }
        [DataMember(Name = "atencion", EmitDefaultValue = false)] public ConfigAtencion? Atencion { get; set; }
        [DataMember(Name = "reflejos", EmitDefaultValue = false)] public List<ReglaReflejo>? Reflejos { get; set; }

        /// <summary>Devuelve los parámetros de humor del perfil o sus valores calibrados por defecto (CP-210).</summary>
        public ConfigHumor ObtenerConfigHumor() => this.Humor ?? new ConfigHumor();

        /// <summary>Devuelve los parámetros de consolidación del búfer y fuga de la huella del perfil o los valores por defecto del motor (P-10, CP-111).</summary>
        public ConfigAtencion ObtenerConfigAtencion() => this.Atencion ?? new ConfigAtencion();

        /// <summary>
        /// Devuelve las reglas de reflejos del perfil o las reglas por defecto derivadas de su tabla de
        /// <see cref="Apertura"/> (GSS §2.1, §6.4; CP-304).
        /// </summary>
        public IReadOnlyList<ReglaReflejo> ObtenerReglasReflejo()
        {
            if (this.Reflejos is { Count: > 0 })
            {
                return this.Reflejos;
            }

            return new List<ReglaReflejo>
            {
                new()
                {
                    Id = "guardar_cuaderno_apertura",
                    Si = new CondicionReflejo
                    {
                        BandaMinima = "foco",
                        Categoria = "granjero",
                        RequiereEnsenar = false,
                        ActividadContinua = "dibujar",
                    },
                    Accion = "guardar_cuaderno",
                    EnfriamientoS = 30.0,
                },
                new()
                {
                    Id = "levantar_vista_dibujando",
                    Si = new CondicionReflejo
                    {
                        BandaMinima = "foco",
                        Categoria = "granjero",
                        RequiereEnsenar = true,
                        ActividadContinua = "dibujar",
                    },
                    Accion = "levantar_la_vista",
                    EnfriamientoS = 30.0,
                },
                new()
                {
                    Id = "saludar_amistad",
                    Si = new CondicionReflejo
                    {
                        BandaMinima = "foco",
                        Categoria = "granjero",
                        DesdeCorazones = 3,
                        ActividadContinua = "ninguna",
                    },
                    Accion = "gesto",
                    Tipo = "saludo",
                    EnfriamientoS = 60.0,
                },
            };
        }
    }

    /// <summary>Parámetros por personaje de consolidación del búfer y fuga de la huella de atención (P-10, CP-111).</summary>
    [DataContract]
    public sealed class ConfigAtencion
    {
        public const int BuferMaxEntradasPorDefecto = 12;
        public const int BuferVentanaRecienteMinPorDefecto = 60;
        public const double BuferSemividaAntiguasMinPorDefecto = 60.0;
        public const double HuellaSemividaMinPorDefecto = 30.0;

        [DataMember(Name = "bufer_max_entradas")] public int BuferMaxEntradas { get; set; } = BuferMaxEntradasPorDefecto;
        [DataMember(Name = "bufer_ventana_reciente_min")] public int BuferVentanaRecienteMin { get; set; } = BuferVentanaRecienteMinPorDefecto;
        [DataMember(Name = "bufer_semivida_antiguas_min")] public double BuferSemividaAntiguasMin { get; set; } = BuferSemividaAntiguasMinPorDefecto;
        [DataMember(Name = "huella_semivida_min")] public double HuellaSemividaMin { get; set; } = HuellaSemividaMinPorDefecto;
    }

    /// <summary>Parámetros de conservación, atenuación y sustitución del humor entre días (P-9, CP-210).</summary>
    [DataContract]
    public sealed class ConfigHumor
    {
        public const double SemividaDiasPorDefecto = 2.5;
        public const double MaxIntensidadPorDefecto = 0.85;
        public const double UmbralDisipacionPorDefecto = 0.15;
        public const double UmbralSustitucionPorDefecto = 0.65;

        [DataMember(Name = "semivida_dias")] public double SemividaDias { get; set; } = SemividaDiasPorDefecto;
        [DataMember(Name = "max_intensidad")] public double MaxIntensidad { get; set; } = MaxIntensidadPorDefecto;
        [DataMember(Name = "umbral_disipacion")] public double UmbralDisipacion { get; set; } = UmbralDisipacionPorDefecto;
        [DataMember(Name = "umbral_sustitucion")] public double UmbralSustitucion { get; set; } = UmbralSustitucionPorDefecto;
    }

    [DataContract]
    public sealed class Umbrales
    {
        [DataMember(Name = "foco")] public double Foco { get; set; }
        [DataMember(Name = "mencion")] public double Mencion { get; set; }
        [DataMember(Name = "periferia")] public double Periferia { get; set; }
        [DataMember(Name = "pico")] public double Pico { get; set; }
        [DataMember(Name = "deriva")] public double Deriva { get; set; }

        /// <summary>Semivida, en minutos de juego, de la fuga del integrador de la deriva (GSS §4.2).</summary>
        [DataMember(Name = "deriva_semivida_min")] public double DerivaSemividaMin { get; set; } = 10;

        /// <summary>Margen para cambiar de banda (GSS §3.3); 0 = sin histéresis.</summary>
        [DataMember(Name = "histeresis")] public double Histeresis { get; set; }
    }

    [DataContract]
    public sealed class Refractario
    {
        [DataMember(Name = "subida")] public double Subida { get; set; }
        [DataMember(Name = "semivida_s")] public double SemividaS { get; set; }
    }

    /// <summary>Presupuesto de pulsos por día de juego, con un valor por modo de reloj (GSS §4.2, §7).</summary>
    [DataContract]
    public sealed class Presupuesto
    {
        [DataMember(Name = "vanilla")] public PresupuestoModo Vanilla { get; set; } = new();
        [DataMember(Name = "pelican")] public PresupuestoModo Pelican { get; set; } = new();
    }

    [DataContract]
    public sealed class PresupuestoModo
    {
        [DataMember(Name = "pulsos_por_dia_juego")] public int PulsosPorDiaJuego { get; set; }
        [DataMember(Name = "reservados_cadenas")] public int ReservadosCadenas { get; set; }

        /// <summary>Plazas diarias adicionales exclusivas de compromisos. Por defecto una (P-12).</summary>
        [DataMember(Name = "extra_compromisos")] public int ExtraCompromisos { get; set; } = 1;
    }

    /// <summary>Lugar del léxico (GSS §3.5). <c>area</c> es <c>[x, y, ancho, alto]</c> en casillas; <c>[0,0,0,0]</c> = pendiente de medir.</summary>
    [DataContract]
    public sealed class Lugar
    {
        [DataMember(Name = "id")] public string Id { get; set; } = "";
        [DataMember(Name = "ubicacion")] public string Ubicacion { get; set; } = "";
        [DataMember(Name = "area")] public List<int> Area { get; set; } = new();
        [DataMember(Name = "prosa")] public string Prosa { get; set; } = "";
        [DataMember(Name = "sentable")] public bool Sentable { get; set; }
    }

    /// <summary>Comportamiento desde cierto número de corazones (GSS §6.4).</summary>
    [DataContract]
    public sealed class TramoApertura
    {
        [DataMember(Name = "desde_corazones")] public int DesdeCorazones { get; set; }
        [DataMember(Name = "ensenar")] public bool Ensenar { get; set; }
        [DataMember(Name = "encargos")] public bool Encargos { get; set; }
        [DataMember(Name = "regalar")] public bool Regalar { get; set; }
    }

    /// <summary>
    /// Anotaciones sobre la rutina (GSS §4.1, §5.3). Los hitos no se listan: salen del horario que
    /// el juego elige cada día; aquí solo se dice qué significan las entradas que coinciden.
    /// </summary>
    [DataContract]
    public sealed class Rutina
    {
        [DataMember(Name = "por_defecto")] public AnotacionHito PorDefecto { get; set; } = new();
        [DataMember(Name = "anotaciones")] public List<AnotacionHito> Anotaciones { get; set; } = new();
    }

    [DataContract]
    public sealed class AnotacionHito
    {
        /// <summary>Vacío en <c>por_defecto</c>.</summary>
        [DataMember(Name = "id")] public string? Id { get; set; }
        [DataMember(Name = "si")] public PatronEntrada? Si { get; set; }
        [DataMember(Name = "forma")] public string? Forma { get; set; }
        [DataMember(Name = "contexto")] public List<string>? Contexto { get; set; }
        [DataMember(Name = "omitir_si_ya_penso")] public bool? OmitirSiYaPenso { get; set; }
        [DataMember(Name = "cambios")] public List<string>? Cambios { get; set; }
        [DataMember(Name = "limites")] public LimitesCambio? Limites { get; set; }
    }

    /// <summary>Patrón sobre una entrada del horario de hoy: destino, animación final o franja horaria.</summary>
    [DataContract]
    public sealed class PatronEntrada
    {
        [DataMember(Name = "ubicacion")] public string? Ubicacion { get; set; }
        [DataMember(Name = "animacion")] public string? Animacion { get; set; }
        [DataMember(Name = "desde_hora")] public int? DesdeHora { get; set; }
        [DataMember(Name = "hasta_hora")] public int? HastaHora { get; set; }
    }

    [DataContract]
    public sealed class LimitesCambio
    {
        [DataMember(Name = "max_por_semana")] public int? MaxPorSemana { get; set; }
        [DataMember(Name = "nunca")] public List<string>? Nunca { get; set; }
    }

    /// <summary>Regla declarativa de reflejo inmediato sin pasar por la mente (GSS §2.1, §6.4; CP-304).</summary>
    [DataContract]
    public sealed class ReglaReflejo
    {
        public const double EnfriamientoPorDefectoSegundos = 30.0;

        [DataMember(Name = "id")] public string Id { get; set; } = "";
        [DataMember(Name = "si")] public CondicionReflejo Si { get; set; } = new();
        [DataMember(Name = "accion")] public string Accion { get; set; } = "";
        [DataMember(Name = "tipo", EmitDefaultValue = false)] public string? Tipo { get; set; }
        [DataMember(Name = "texto", EmitDefaultValue = false)] public string? Texto { get; set; }
        [DataMember(Name = "enfriamiento_s")] public double EnfriamientoS { get; set; } = EnfriamientoPorDefectoSegundos;
    }

    /// <summary>Condición sobre bandas, relación, apertura y estado del cuerpo que dispara un reflejo.</summary>
    [DataContract]
    public sealed class CondicionReflejo
    {
        [DataMember(Name = "banda_minima", EmitDefaultValue = false)] public string? BandaMinima { get; set; } = "foco";
        [DataMember(Name = "categoria", EmitDefaultValue = false)] public string? Categoria { get; set; }
        [DataMember(Name = "desde_corazones", EmitDefaultValue = false)] public int? DesdeCorazones { get; set; }
        [DataMember(Name = "hasta_corazones", EmitDefaultValue = false)] public int? HastaCorazones { get; set; }
        [DataMember(Name = "requiere_ensenar", EmitDefaultValue = false)] public bool? RequiereEnsenar { get; set; }
        [DataMember(Name = "actividad_continua", EmitDefaultValue = false)] public string? ActividadContinua { get; set; }
    }
}
