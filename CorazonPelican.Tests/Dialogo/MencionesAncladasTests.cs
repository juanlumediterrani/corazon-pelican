using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Observabilidad;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

/// <summary>
/// Guardas del validador de menciones ancladas a escena y memoria (R-15, CP-506).
/// </summary>
public class MencionesAncladasTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static Pulso CrearPulsoBase(int dia = 12) => new()
    {
        Id = 50,
        Epoca = "ep-menc",
        Personaje = "leah",
        Motivo = Motivos.Deriva,
        TiempoJuego = new TiempoJuego { Dia = dia, Hora = 1700 },
        Escena = "Estás junto a la pasarela de madera mirando el agua tranquila del lago.",
        Lugares = new List<ParIdProsa>
        {
            new() { Id = "pasarela_lago", Prosa = "la pasarela de madera del lago" },
            new() { Id = "tu_casa", Prosa = "tu casa" },
        },
        Hechos = new Dictionary<string, JsonElement>
        {
            ["cerca"] = JsonDocument.Parse("""[{"ref":"farmer:1","nombre":"Ana","corazones":6}]""").RootElement,
        },
        Huella = new Huella
        {
            DesdeUltimoPensamientoMinJuego = 30,
            Prosa = "",
            Entidades = new List<AtencionEntidad>(),
        },
        Agenda = new List<EntradaAgenda>(),
        AccionesDisponibles = new List<string> { "esperar", "dibujar" },
    };

    [Fact]
    public void Garza_del_muelle_sin_garza_ni_muelle_en_escena_ni_memoria_es_detectada_y_rechazada()
    {
        var perfil = CargarPerfil();
        var estado = new EstadoPersonaje();
        var pulso = CrearPulsoBase();
        var resp = new Respuesta
        {
            Pensamiento = "Observo alrededor.",
            Epoca = pulso.Epoca,
            BasadaEn = pulso.Id,
            Frase = "Mira la garza del muelle.",
        };

        var res = ValidadorMenciones.Validar(pulso, resp, estado, perfil);
        Assert.False(res.EsValido);
        Assert.Contains("garza", res.MencionesNoAncladas, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("muelle", res.MencionesNoAncladas, StringComparer.OrdinalIgnoreCase);
        Assert.All(res.Motivos, m => Assert.Equal("mencion_no_anclada", m.Codigo));

        // Si en cambio ambos existen en la memoria del personaje, quedan anclados y pasan.
        estado.AgregarRecuerdo("Ayer vimos posarse una garza junto al muelle viejo.", 0.8);
        var resConMemoria = ValidadorMenciones.Validar(pulso, resp, estado, perfil);
        Assert.True(resConMemoria.EsValido);
    }

    [Fact]
    public void Ana_presente_es_aceptada_Bruno_ausente_rechazado_y_genericos_ignorados()
    {
        var perfil = CargarPerfil();
        var estado = new EstadoPersonaje();
        var pulso = CrearPulsoBase();

        var conAnaYGenericos = new Respuesta
        {
            Pensamiento = "Saludo sin inventar nada.",
            Epoca = pulso.Epoca,
            BasadaEn = pulso.Id,
            Frase = "Ana, el viento trae algo de calma hoy; alguien caminaba por la pasarela.",
        };
        var ok = ValidadorMenciones.Validar(pulso, conAnaYGenericos, estado, perfil);
        Assert.True(ok.EsValido);

        var conBrunoAusente = new Respuesta
        {
            Pensamiento = "Hablo de alguien inexistente.",
            Epoca = pulso.Epoca,
            BasadaEn = pulso.Id,
            Frase = "Estuve esperando a Bruno toda la tarde.",
        };
        var fail = ValidadorMenciones.Validar(pulso, conBrunoAusente, estado, perfil);
        Assert.False(fail.EsValido);
        Assert.Contains("Bruno", fail.MencionesNoAncladas);

        // También valida intención (objetivo/porque) y obra.decision (tema/titulo/nota).
        var enIntencionYObra = new Respuesta
        {
            Pensamiento = "Decido.",
            Epoca = pulso.Epoca,
            BasadaEn = pulso.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "buscar a Bruno cerca del faro",
                Porque = "me pareció ver una señal",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 20 } },
            },
            Obra = new ObraPropuesta
            {
                Formato = "boceto",
                Decision = new DecisionObra
                {
                    Titulo = "El faro",
                    Tema = "torre junto a Bruno",
                    Composicion = "encuadre abierto",
                    Paleta = "grises",
                    Nota = "estudio rápido",
                },
            },
        };
        var failObra = ValidadorMenciones.Validar(pulso, enIntencionYObra, estado, perfil);
        Assert.False(failObra.EsValido);
        Assert.Contains(failObra.Motivos, m => m.Campo.StartsWith("intencion.", StringComparison.Ordinal));
        Assert.Contains(failObra.Motivos, m => m.Campo.StartsWith("obra.decision.", StringComparison.Ordinal));
    }

    [Fact]
    public void Telemetria_registra_tasa_diaria_en_rechazos_jsonl_y_soporta_modo_aviso_y_rechazar()
    {
        var dirTemp = Path.Combine(Path.GetTempPath(), "cp-menciones-" + Guid.NewGuid().ToString("N"));
        try
        {
            var perfil = CargarPerfil();
            var plantillas = CargarPlantillas();
            var estado = new EstadoPersonaje();
            var tel = new TelemetriaJsonl(dirTemp, "dev_menciones", habilitada: true);

            // 1. Modo aviso: no rechaza la respuesta, pero sí registra tasa_no_ancladas por día en rechazos.jsonl.
            var cfgAviso = new CorazonConfig
            {
                Personajes = new List<string> { "leah" },
                ModoValidacionMenciones = CorazonConfig.ValidacionMencionesAviso,
            };
            Assert.Empty(cfgAviso.Validate());

            var motorAviso = new MotorHabitante(
                perfil,
                plantillas,
                estado,
                ModoReloj.Vanilla,
                tel,
                modoValidacionMenciones: cfgAviso.ModoValidacionMenciones)
            {
                Epoca = "ep-aviso",
            };
            var pulso1 = motorAviso.ForzarPulso(
                new Observacion(12, 1700, "Mountain", 40, 30, 1.0, "soleado", new[]
                {
                    new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 31, new[] { "persona" }, 6),
                }),
                ahoraMs: 1_000).Pulso!;

            var resAviso = motorAviso.AplicarRespuesta(pulso1, new Respuesta
            {
                Pensamiento = "Nombro a Ana y una garza ausente.",
                Epoca = "ep-aviso",
                BasadaEn = pulso1.Id,
                Frase = "Ana, mira la garza del muelle.",
            });
            Assert.Equal(Resultado.Aceptada, resAviso.Estado);

            // 2. Modo rechazar (por defecto): rechaza cuando hay mención no anclada.
            var motorRechazar = new MotorHabitante(
                perfil,
                plantillas,
                new EstadoPersonaje(),
                ModoReloj.Vanilla,
                tel)
            {
                Epoca = "ep-rechazo",
            };
            var pulso2 = motorRechazar.ForzarPulso(
                new Observacion(13, 1700, "Mountain", 40, 30, 1.0, "soleado", Array.Empty<EntidadObservada>()),
                ahoraMs: 2_000).Pulso!;

            var resRechazar = motorRechazar.AplicarRespuesta(pulso2, new Respuesta
            {
                Pensamiento = "Nombro una garza inexistente.",
                Epoca = "ep-rechazo",
                BasadaEn = pulso2.Id,
                Frase = "Vi la garza del muelle.",
            });
            Assert.Equal(Resultado.Rechazada, resRechazar.Estado);
            Assert.Contains(resRechazar.Motivos!, m => m.Codigo == "mencion_no_anclada");

            var rutaRechazos = tel.RutaArchivo(TelemetriaJsonl.ArchivoRechazos);
            Assert.True(File.Exists(rutaRechazos));
            var lineas = File.ReadAllLines(rutaRechazos)
                .Select(l => JsonDocument.Parse(l).RootElement)
                .ToList();

            var tasas = lineas.Where(e => e.TryGetProperty("tipo", out var t) && t.GetString() == "tasa_menciones").ToList();
            Assert.Equal(2, tasas.Count);
            Assert.Equal(12, tasas[0].GetProperty("dia_juego").GetInt32());
            Assert.True(tasas[0].GetProperty("tasa_no_ancladas").GetDouble() > 0.0);
            Assert.Equal(13, tasas[1].GetProperty("dia_juego").GetInt32());
            Assert.Equal(1.0, tasas[1].GetProperty("tasa_no_ancladas").GetDouble());
        }
        finally
        {
            if (Directory.Exists(dirTemp))
            {
                Directory.Delete(dirTemp, recursive: true);
            }
        }
    }

    [Fact]
    public void Palabras_comunes_al_inicio_de_oracion_no_se_rechazan_pero_entidades_de_catalogo_si()
    {
        var perfil = CargarPerfil();
        var estado = new EstadoPersonaje();
        var pulso = CrearPulsoBase();

        var natural = new Respuesta
        {
            Epoca = pulso.Epoca,
            BasadaEn = pulso.Id,
            Pensamiento = "Siento curiosidad por ver cómo sigue la mañana.",
            Intencion = new IntencionPropuesta
            {
                Objetivo = "Acercarme a trabajar con calma",
                Porque = "Siento que hoy puedo avanzar bastante",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 10 } },
            },
        };
        Assert.True(ValidadorMenciones.Validar(pulso, natural, estado, perfil).EsValido);

        var conEntidadInicio = new Respuesta
        {
            Epoca = pulso.Epoca,
            BasadaEn = pulso.Id,
            Frase = "Faro queda bastante lejos de aquí.",
        };
        var fail = ValidadorMenciones.Validar(pulso, conEntidadInicio, estado, perfil);
        Assert.False(fail.EsValido);
        Assert.Contains("Faro", fail.MencionesNoAncladas);
    }
}
