using CorazonPelican.Core.Perfil;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CorazonPelican.Tests.Perfil;

public class PerfilValidatorTests
{
    private static readonly string RutaLeah = Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json");

    /// <summary>Lee como lo hace SMAPI: Newtonsoft.Json con [DataContract]/[DataMember].</summary>
    private static PerfilPersonaje Leah() => JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(RutaLeah))!;

    private static ResultadoValidacionPerfil Validar(PerfilPersonaje perfil) => PerfilValidator.Validar(perfil, ModoValidacion.Desarrollo);

    private static void TieneError(ResultadoValidacionPerfil resultado, string campo, string fragmento)
        => Assert.Contains(resultado.Errores, h => h.Campo.StartsWith(campo, StringComparison.Ordinal) && h.Mensaje.Contains(fragmento, StringComparison.Ordinal));

    [Fact]
    public void Leah_se_lee_con_los_nombres_del_esquema()
    {
        var leah = Leah();

        Assert.Equal("leah", leah.Id);
        Assert.Equal("docs/rubrica-caracter.md", leah.RubricaCaracter);
        Assert.Equal(0.40, leah.Umbrales.Mencion);
        Assert.Equal(12, leah.Presupuesto.Pelican.PulsosPorDiaJuego);
        Assert.Contains(leah.Lexico.Lugares, l => l.Id == "pasarela_lago" && l.Sentable);
        Assert.Contains(leah.Rutina.Anotaciones, a => a.Id == "saloon" && a.Limites!.MaxPorSemana == 1);
    }

    [Fact]
    public void Leah_cumple_el_esquema_del_perfil()
    {
        var documento = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(RutaLeah));

        Assert.True(Contrato.Esquemas.EsValido("perfil", documento));
    }

    [Fact]
    public void Leah_en_desarrollo_es_valida_con_avisos_solo_por_areas_pendientes()
    {
        var resultado = PerfilValidator.Validar(Leah(), ModoValidacion.Desarrollo);

        Assert.True(resultado.EsValido, string.Join("\n", resultado.Errores));
        Assert.Equal(2, resultado.Avisos.Count);
        Assert.All(resultado.Avisos, a => Assert.Contains("pendiente de medir", a.Mensaje));
    }

    [Fact]
    public void Leah_en_modo_normal_falla_solo_por_areas_pendientes()
    {
        var resultado = PerfilValidator.Validar(Leah(), ModoValidacion.Normal);

        Assert.False(resultado.EsValido);
        Assert.Equal(2, resultado.Errores.Count);
        Assert.All(resultado.Errores, e => Assert.Contains("pendiente de medir", e.Mensaje));
        Assert.Empty(resultado.Avisos);
    }

    [Fact]
    public void Umbrales_desordenados_son_error()
    {
        var perfil = Leah();
        perfil.Umbrales.Mencion = 0.9;

        TieneError(Validar(perfil), "umbrales", "periferia < mencion < foco < pico");
    }

    [Fact]
    public void Accion_fuera_del_vocabulario_es_error()
    {
        var perfil = Leah();
        perfil.Acciones.Add("teletransportarse");

        TieneError(Validar(perfil), "acciones", "«teletransportarse» no está en el vocabulario v1");
    }

    [Fact]
    public void Lugar_duplicado_es_error()
    {
        var perfil = Leah();
        perfil.Lexico.Lugares.Add(new Lugar { Id = "playa", Ubicacion = "Beach", Area = new() { 1, 1, 1, 1 }, Prosa = "otra playa" });

        TieneError(Validar(perfil), "lexico.lugares", "«playa» está duplicado");
    }

    [Fact]
    public void Area_mal_formada_es_error_tambien_en_desarrollo()
    {
        var perfil = Leah();
        perfil.Lexico.Lugares[0].Area = new() { 8, 5, 0, 1 };

        TieneError(Validar(perfil), "lexico.lugares[0].area", "ancho, alto > 0");
    }

    [Fact]
    public void Cambio_de_rutina_desconocido_es_error()
    {
        var perfil = Leah();
        perfil.Rutina.Anotaciones.Single(a => a.Id == "saloon").Cambios!.Add("cancelar_festival");

        TieneError(Validar(perfil), "rutina.anotaciones", "«cancelar_festival» no es un cambio de rutina");
    }

    [Fact]
    public void Fuente_de_contexto_desconocida_es_error()
    {
        var perfil = Leah();
        perfil.Rutina.Anotaciones.Single(a => a.Id == "saloon").Contexto!.Add("ventas_dia");

        TieneError(Validar(perfil), "rutina.anotaciones", "el motor no sabe reunir «ventas_dia»");
    }

    [Fact]
    public void Anotacion_sin_patron_es_error()
    {
        var perfil = Leah();
        perfil.Rutina.Anotaciones.Single(a => a.Id == "saloon").Si = null;

        TieneError(Validar(perfil), "rutina.anotaciones", "necesita un patrón");
    }

    [Theory]
    [InlineData("despertar")]
    [InlineData("acostarse")]
    public void Hitos_del_motor_no_llevan_patron_ni_se_cambian(string hito)
    {
        var perfil = Leah();
        var anotacion = perfil.Rutina.Anotaciones.Single(a => a.Id == hito);
        Assert.True(Validar(perfil).EsValido);

        anotacion.Cambios = new() { "saltar" };
        anotacion.Forma = "pensamiento";

        var resultado = Validar(perfil);
        TieneError(resultado, "rutina.anotaciones", "no se puede saltar");
        TieneError(resultado, "rutina.anotaciones", "siempre abre una cadena");
    }

    [Fact]
    public void Presupuesto_sin_reserva_para_las_cadenas_es_error()
    {
        var perfil = Leah();
        perfil.Presupuesto.Vanilla.ReservadosCadenas = 1;

        TieneError(Validar(perfil), "presupuesto.vanilla.reservados_cadenas", "despertar y acostarse");
    }

    [Fact]
    public void Apertura_debe_empezar_en_cero_y_crecer()
    {
        var perfil = Leah();
        perfil.Apertura[2].DesdeCorazones = 3;

        TieneError(Validar(perfil), "apertura[2]", "orden creciente");
    }

    [Fact]
    public void Ida_y_vuelta_con_newtonsoft_sigue_cumpliendo_el_esquema()
    {
        var vuelta = JsonConvert.SerializeObject(Leah(), new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

        Assert.True(Contrato.Esquemas.EsValido("perfil", System.Text.Json.Nodes.JsonNode.Parse(vuelta)));
        Assert.Equal(JToken.Parse(File.ReadAllText(RutaLeah))["lexico"]!["lugares"]!.Count(), JToken.Parse(vuelta)["lexico"]!["lugares"]!.Count());
    }
}
