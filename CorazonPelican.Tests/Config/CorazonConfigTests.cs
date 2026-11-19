using CorazonPelican.Core.Config;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Config;

public class CorazonConfigTests
{
    private static readonly string RutaLeah = Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json");

    private static CorazonConfig ConfigValida() => new()
    {
        Personajes = new List<string> { "leah" },
    };

    [Fact]
    public void Valores_por_defecto_coinciden_con_la_especificacion()
    {
        var cfg = new CorazonConfig();

        Assert.Empty(cfg.Personajes);
        Assert.True(cfg.MenteHabilitada);
        Assert.False(cfg.ModoDesarrollo);
        Assert.Equal(25560, cfg.ApiPuerto);
        Assert.Equal(string.Empty, cfg.ApiToken);
        Assert.Equal(15, cfg.SensorioCadenciaTicks);
        Assert.Equal("auto", cfg.ModoReloj);
        Assert.Equal(8000, cfg.TiempoMaxRespuestaMs);
        Assert.True(cfg.AnticipacionHabilitada);
        Assert.Equal(512, cfg.TamanoMaxMemoriaKB);
        Assert.Empty(cfg.ValleyTalkDesactivadoPara);
        Assert.Equal(string.Empty, cfg.PelicanApiUrl);
        Assert.Equal(string.Empty, cfg.PelicanApiToken);
        Assert.True(cfg.Telemetria);
    }

    [Fact]
    public void Config_valida_y_leida_desde_json_no_produce_errores()
    {
        var json = JsonConvert.SerializeObject(ConfigValida());
        var leida = JsonConvert.DeserializeObject<CorazonConfig>(json)!;

        Assert.Empty(leida.Validate());
        Assert.Equal(new[] { "leah" }, leida.Personajes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(80)]
    [InlineData(1023)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void Validate_rechaza_puerto_fuera_de_rango(int puerto)
    {
        var cfg = ConfigValida();
        cfg.ApiPuerto = puerto;

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.ApiPuerto), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1024)]
    [InlineData(25560)]
    [InlineData(65535)]
    public void Validate_acepta_puertos_en_rango(int puerto)
    {
        var cfg = ConfigValida();
        cfg.ApiPuerto = puerto;

        Assert.Empty(cfg.Validate());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public void Validate_rechaza_tiempo_max_respuesta_no_positivo(int ms)
    {
        var cfg = ConfigValida();
        cfg.TiempoMaxRespuestaMs = ms;

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.TiempoMaxRespuestaMs), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-15)]
    public void Validate_rechaza_sensorio_cadencia_no_positiva(int ticks)
    {
        var cfg = ConfigValida();
        cfg.SensorioCadenciaTicks = ticks;

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.SensorioCadenciaTicks), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-128)]
    public void Validate_rechaza_tamano_max_memoria_no_positivo(int kb)
    {
        var cfg = ConfigValida();
        cfg.TamanoMaxMemoriaKB = kb;

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.TamanoMaxMemoriaKB), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("turbo")]
    [InlineData("invalido")]
    public void Validate_rechaza_modo_reloj_invalido(string modo)
    {
        var cfg = ConfigValida();
        cfg.ModoReloj = modo;

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.ModoReloj), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("vanilla")]
    [InlineData("pelican")]
    [InlineData("AUTO")]
    public void Validate_acepta_modos_de_reloj_conocidos(string modo)
    {
        var cfg = ConfigValida();
        cfg.ModoReloj = modo;

        Assert.Empty(cfg.Validate());
    }

    [Fact]
    public void Validate_rechaza_personajes_vacio()
    {
        var cfg = new CorazonConfig();

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.Personajes), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Leah")]
    [InlineData("1leah")]
    [InlineData("leah-npc")]
    public void Validate_rechaza_ids_de_personajes_invalidos(string idInvalido)
    {
        var cfg = ConfigValida();
        cfg.Personajes = new List<string> { idInvalido };

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.Personajes), StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_rechaza_personajes_duplicados()
    {
        var cfg = ConfigValida();
        cfg.Personajes = new List<string> { "leah", "leah" };

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains("duplicado", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("no-es-url")]
    [InlineData("/api/v1/host/events")]
    [InlineData("ftp://127.0.0.1:8085")]
    public void Validate_rechaza_pelican_api_url_invalida_cuando_no_esta_vacia(string url)
    {
        var cfg = ConfigValida();
        cfg.PelicanApiUrl = url;

        var errores = cfg.Validate();

        Assert.Contains(errores, e => e.Contains(nameof(CorazonConfig.PelicanApiUrl), StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://127.0.0.1:8085")]
    [InlineData("https://pelican.local:8443")]
    public void Validate_acepta_pelican_api_url_vacia_o_http_valida(string url)
    {
        var cfg = ConfigValida();
        cfg.PelicanApiUrl = url;

        Assert.Empty(cfg.Validate());
    }

    [Theory]
    [InlineData("auto", false, ModoReloj.Vanilla)]
    [InlineData("auto", true, ModoReloj.Pelican)]
    [InlineData("vanilla", true, ModoReloj.Vanilla)]
    [InlineData("pelican", false, ModoReloj.Pelican)]
    public void ResolverModoReloj_elige_segun_config_y_presencia_de_pelican(string configurado, bool pelicanPresente, ModoReloj esperado)
    {
        var cfg = ConfigValida();
        cfg.ModoReloj = configurado;

        Assert.Equal(esperado, cfg.ResolverModoReloj(pelicanPresente));
    }

    [Fact]
    public void SeleccionarPersonajes_detecta_personaje_configurado_sin_perfil()
    {
        var leah = JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(RutaLeah))!;
        var perfilesCargados = new Dictionary<string, PerfilPersonaje>
        {
            ["leah"] = leah,
            ["extra_no_configurado"] = leah,
        };

        var cfg = new CorazonConfig
        {
            Personajes = new List<string> { "leah", "ausente" },
        };

        var seleccion = cfg.SeleccionarPersonajes(perfilesCargados);

        Assert.Single(seleccion.Gobernados);
        Assert.True(seleccion.Gobernados.ContainsKey("leah"));
        Assert.False(seleccion.Gobernados.ContainsKey("extra_no_configurado"));
        Assert.Equal(new[] { "ausente" }, seleccion.SinPerfil);
    }

    [Fact]
    public void RegistroModulos_aisla_fallos_y_no_tumba_el_resto()
    {
        var modulos = new (string Nombre, Action Registrar)[]
        {
            ("Primero", () => { }),
            ("Defectuoso", () => throw new InvalidOperationException("parche roto")),
            ("Tercero", () => { }),
        };

        var resultado = RegistroModulos.RegistrarTodos(
            modulos,
            m => m.Nombre,
            m => m.Registrar());

        Assert.Equal(new[] { "Primero", "Tercero" }, resultado.Activos);
        var fallo = Assert.Single(resultado.Fallidos);
        Assert.Equal("Defectuoso", fallo.Nombre);
        Assert.IsType<InvalidOperationException>(fallo.Error);
    }
}
