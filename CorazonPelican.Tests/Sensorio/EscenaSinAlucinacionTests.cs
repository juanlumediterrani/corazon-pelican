using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Tests.Contrato;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Sensorio;

/// <summary>Guarda de R-02: toda referencia en la escena y su apéndice existe en lo observado (GSS §3.5, §13; ADS §3.3).</summary>
public class EscenaSinAlucinacionTests
{
    private static readonly JsonSerializerOptions JsonPulso = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Toda_referencia_en_escena_y_apendice_existe_en_la_observacion_en_recorrido_fakeworld()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var mundo = Escenario.Pasarela()
            .ConSemilla(7)
            .ConJugador("Ana", corazones: 6, distancia: 6, deambula: true)
            .ConNpc("Linus", "Mountain", 4, 2, deambula: false)
            .Construir(perfil.Lexico);

        var contexto = new ContextoSaliencia(perfil.Rasgos, EstadoAtencion.Abierta, perfil.Umbrales.Periferia);
        var novedad = new Novedad();
        var clasificador = new ClasificadorBandas(perfil.Umbrales);
        var lugaresOfrecidos = perfil.Lexico.OfrecerLugares(_ => true);

        foreach (var observacion in mundo.AvanzarObservando(TimeSpan.FromMinutes(2)))
        {
            var saliencias = Saliencia.Calcular(observacion, contexto, novedad);
            novedad.Exponer(observacion.Entidades.Select(e => e.Ref), 0.5);
            var bandas = clasificador.Clasificar(saliencias);
            var escena = GeneradorEscena.Generar(observacion, bandas, lugaresOfrecidos, plantillas);

            var observadas = observacion.Entidades.ToDictionary(e => e.Ref, StringComparer.Ordinal);
            var bandaPorRef = bandas.ToDictionary(b => b.Ref, b => b.Banda, StringComparer.Ordinal);

            // Toda entidad en el apéndice existe en la observación y no es Ausente.
            foreach (var par in escena.Apendice.Entidades)
            {
                Assert.True(observadas.TryGetValue(par.Id, out var entidad), $"Referencia inexistente en apéndice: {par.Id}");
                Assert.NotEqual(Banda.Ausente, bandaPorRef[par.Id]);
                Assert.Contains(par.Prosa, escena.Escena, StringComparison.Ordinal);

                if (bandaPorRef[par.Id] is Banda.Foco or Banda.Mencion)
                {
                    Assert.Equal(entidad.Nombre, par.Prosa);
                }
            }

            // Toda entidad en Foco o Mención aparece en el apéndice con su id y misma prosa.
            foreach (var entidad in observacion.Entidades)
            {
                var banda = bandaPorRef[entidad.Ref];
                if (banda is Banda.Foco or Banda.Mencion)
                {
                    Assert.Contains(escena.Apendice.Entidades, p => p.Id == entidad.Ref && p.Prosa == entidad.Nombre);
                    Assert.Contains(entidad.Nombre, escena.Escena, StringComparison.Ordinal);
                }
                else if (banda == Banda.Ausente)
                {
                    Assert.DoesNotContain(escena.Apendice.Entidades, p => p.Id == entidad.Ref);
                    Assert.DoesNotContain(escena.Apendice.Cerca, c => c.Ref == entidad.Ref);
                    Assert.DoesNotContain(entidad.Nombre, escena.Escena, StringComparison.Ordinal);
                    Assert.DoesNotContain(entidad.Ref, escena.TextoCompleto, StringComparison.Ordinal);
                }
            }

            // Toda persona en apendice.Cerca existe en la observación y no es Ausente.
            foreach (var persona in escena.Apendice.Cerca)
            {
                Assert.True(observadas.ContainsKey(persona.Ref));
                Assert.NotEqual(Banda.Ausente, bandaPorRef[persona.Ref]);
                Assert.Contains(persona.Nombre, escena.Escena, StringComparison.Ordinal);
            }

            // El lugar actual nombrado aparece en el apéndice con su id y exacta prosa.
            if (escena.Apendice.LugarActual is { } lugarActual)
            {
                Assert.Contains(lugarActual.Prosa, escena.Escena, StringComparison.Ordinal);
                Assert.Contains(escena.Apendice.Lugares, l => l.Id == lugarActual.Id && l.Prosa == lugarActual.Prosa);
            }
        }
    }

    [Fact]
    public void Lugar_nombrado_en_escena_aparece_en_apendice_con_su_id_y_misma_prosa()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var lexico = new Lexico
        {
            Lugares = new List<Lugar>
            {
                new() { Id = "pasarela_lago", Ubicacion = "Mountain", Area = new() { 40, 30, 1, 1 }, Prosa = "la pasarela de madera del lago" },
                new() { Id = "tu_casa", Ubicacion = "LeahHouse", Area = new() { 8, 5, 1, 1 }, Prosa = "tu casa" },
            },
        };
        var obs = new Observacion(68, 1740, "Mountain", 40, 30, 0.6, "soleado", Array.Empty<EntidadObservada>());
        var ofrecidos = lexico.OfrecerLugares(_ => true);

        var escena = GeneradorEscena.Generar(obs, Array.Empty<EntidadEnBanda>(), ofrecidos, plantillas);

        Assert.Contains("la pasarela de madera del lago", escena.Escena, StringComparison.Ordinal);
        Assert.Contains(escena.Apendice.Lugares, l => l.Id == "pasarela_lago" && l.Prosa == "la pasarela de madera del lago");
        Assert.Contains("- pasarela_lago — la pasarela de madera del lago", escena.TextoCompleto, StringComparison.Ordinal);
    }

    [Fact]
    public void Periferia_usa_prosa_vaga_y_no_revela_el_nombre_propio()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var obs = new Observacion(
            68,
            1740,
            "Mountain",
            40,
            30,
            0.5,
            "soleado",
            new[] { new EntidadObservada("farmer:9", CategoriaEntidad.Granjero, "Sebastián", "Mountain", 40, 14, new[] { "persona" }, Corazones: 4) });
        var bandas = new[] { new EntidadEnBanda("farmer:9", Banda.Periferia, 0.22) };

        var escena = GeneradorEscena.Generar(obs, bandas, perfil.Lexico.OfrecerLugares(_ => true), plantillas);

        Assert.DoesNotContain("Sebastián", escena.Escena, StringComparison.Ordinal);
        Assert.DoesNotContain("Sebastián", escena.TextoCompleto, StringComparison.Ordinal);
        var par = Assert.Single(escena.Apendice.Entidades);
        Assert.Equal("farmer:9", par.Id);
        Assert.Equal("alguien", par.Prosa);
        Assert.Contains("alguien", escena.Escena, StringComparison.Ordinal);
        Assert.Empty(escena.Apendice.Cerca);
    }

    [Fact]
    public void Referencias_en_bandas_ausentes_de_observacion_se_ignoran_sin_alucinar()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var obs = new Observacion(12, 1000, "Desert", 5, 5, 1.0, "soleado", Array.Empty<EntidadObservada>());
        var bandasConFantasma = new[]
        {
            new EntidadEnBanda("npc:GarzaInventada", Banda.Foco, 0.99),
            new EntidadEnBanda("agua:Fantasma", Banda.Mencion, 0.50),
        };

        var escena = GeneradorEscena.Generar(obs, bandasConFantasma, perfil.Lexico.OfrecerLugares(_ => false), plantillas);

        Assert.Empty(escena.Apendice.Entidades);
        Assert.Empty(escena.Apendice.Cerca);
        Assert.Empty(escena.Apendice.Lugares);
        Assert.Null(escena.Apendice.LugarActual);
        Assert.DoesNotContain("GarzaInventada", escena.TextoCompleto, StringComparison.Ordinal);
        Assert.DoesNotContain("Fantasma", escena.TextoCompleto, StringComparison.Ordinal);
    }

    [Fact]
    public void Huella_sin_entidades_no_alucina_referencias()
    {
        var plantillas = CargarPlantillasLeah();

        var huella = GeneradorAccion.GenerarHuella(45, Array.Empty<EntidadAtendida>(), 68, plantillas);

        Assert.Empty(huella.Entidades);
        Assert.Equal(45, huella.DesdeUltimoPensamientoMinJuego);
        Assert.Contains("45 minutos", huella.Prosa, StringComparison.Ordinal);
        Assert.DoesNotContain("Ana", huella.Prosa, StringComparison.Ordinal);
        Assert.DoesNotContain("lago", huella.Prosa, StringComparison.Ordinal);
    }

    [Fact]
    public void Pulso_creado_desde_generador_cumple_el_esquema_del_contrato()
    {
        var perfil = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 9).Construir(perfil.Lexico);

        var escena = GeneradorEscena.Generar(mundo.Observar(), perfil, plantillas);
        var pulso = escena.CrearPulsoBase(id: 1, epoca: "01JC8Q2M5V7K3T9W4X6Y8Z0A1B", personaje: perfil.Id, motivo: Motivos.Deriva);
        var nodo = JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(pulso, JsonPulso));

        Assert.True(Esquemas.EsValido("pulso", nodo), $"El pulso generado no cumple schemas/pulso.schema.json:\n{nodo}");
    }
}
