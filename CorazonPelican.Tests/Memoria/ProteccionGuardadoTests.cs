using CorazonPelican.Core.Memoria;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Memoria;

/// <summary>Pruebas de protección de guardado ante fallos de carga o migración (CP-110).</summary>
public class ProteccionGuardadoTests
{
    [Fact]
    public void Fallo_de_lectura_y_version_futura_bloquean_el_guardado_conservan_datos_y_diagnostican_la_partida()
    {
        const string jsonCorrupto = "{ \"version_esquema\": 2, \"personajes\": { \"leah\": [rotura";
        const string jsonVersionFutura = """
        {
          "version_esquema": 99,
          "personajes": {
            "leah": {
              "version_esquema": 99,
              "diario": [
                {
                  "id": "ep-1",
                  "dia_juego": 40,
                  "hora": 1800,
                  "resumen_prosa": "Un recuerdo valioso de otra versión.",
                  "saliencia": 0.9
                }
              ]
            }
          }
        }
        """;

        var almacen = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["partida_corrupta_111"] = jsonCorrupto,
            ["partida_futura_222"] = jsonVersionFutura,
        };

        var gestor = new GestorPersistenciaPartida();

        // 1. Fallo de lectura (JSON corrupto como el de ReadSaveData / Newtonsoft.Json).
        var cargaCorrupta = gestor.Cargar(
            "partida_corrupta_111",
            () => JsonConvert.DeserializeObject<EstadoPartida>(almacen["partida_corrupta_111"]),
            new[] { "leah" });

        Assert.False(cargaCorrupta.Exito);
        Assert.True(cargaCorrupta.GuardadoBloqueado);
        Assert.True(gestor.GuardadoBloqueado);
        Assert.Equal("partida_corrupta_111", cargaCorrupta.PartidaId);
        Assert.Equal("partida_corrupta_111", gestor.PartidaAfectada);
        Assert.Contains("partida_corrupta_111", cargaCorrupta.Diagnostico, StringComparison.Ordinal);

        // El estado en memoria sigue usable para la sesión sin tumbar el mod.
        gestor.EstadoActual.ObtenerOIniciar("leah").AgregarEpisodio(1, 900, "Episodio temporal.", 0.5);

        var escrituras = 0;
        var intentoGuardarCorrupta = gestor.Guardar(estado =>
        {
            escrituras++;
            almacen["partida_corrupta_111"] = JsonConvert.SerializeObject(estado);
        });

        Assert.False(intentoGuardarCorrupta.Guardado);
        Assert.True(intentoGuardarCorrupta.OmitidoPorProteccion);
        Assert.Equal("partida_corrupta_111", intentoGuardarCorrupta.PartidaAfectada);
        Assert.Contains("partida_corrupta_111", intentoGuardarCorrupta.Motivo, StringComparison.Ordinal);
        Assert.Equal(0, escrituras);
        Assert.Equal(jsonCorrupto, almacen["partida_corrupta_111"]);

        // 2. Versión de esquema posterior a la admitida (v99 > v2).
        var cargaFutura = gestor.Cargar(
            "partida_futura_222",
            () => PersistenciaEstado.Deserializar(almacen["partida_futura_222"]),
            new[] { "leah" });

        Assert.False(cargaFutura.Exito);
        Assert.True(cargaFutura.GuardadoBloqueado);
        Assert.Equal("partida_futura_222", cargaFutura.PartidaId);
        Assert.Equal("partida_futura_222", gestor.PartidaAfectada);
        Assert.Contains("partida_futura_222", cargaFutura.Diagnostico, StringComparison.Ordinal);
        Assert.Contains("v99", cargaFutura.Diagnostico, StringComparison.Ordinal);

        var intentoGuardarFutura = gestor.Guardar(estado =>
        {
            escrituras++;
            almacen["partida_futura_222"] = PersistenciaEstado.Serializar(estado);
        });

        Assert.False(intentoGuardarFutura.Guardado);
        Assert.True(intentoGuardarFutura.OmitidoPorProteccion);
        Assert.Equal("partida_futura_222", intentoGuardarFutura.PartidaAfectada);
        Assert.Equal(0, escrituras);
        Assert.Equal(jsonVersionFutura, almacen["partida_futura_222"]);
    }

    [Fact]
    public void Abrir_despues_una_partida_legible_y_otra_nueva_desbloquea_el_guardado_sin_tocar_la_afectada()
    {
        const string jsonFutura = """{ "version_esquema": 99, "personajes": {} }""";
        var partidaLegible = new EstadoPartida();
        partidaLegible.ObtenerOIniciar("leah").AgregarEpisodio(12, 1100, "Mañana serena junto al río.", 0.72);
        var jsonLegible = PersistenciaEstado.Serializar(partidaLegible);

        var almacen = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["partida_afectada_999"] = jsonFutura,
            ["partida_legible_333"] = jsonLegible,
        };

        var gestor = new GestorPersistenciaPartida();

        // Primero falla la carga de la partida con esquema v99.
        var fallo = gestor.Cargar(
            "partida_afectada_999",
            () => PersistenciaEstado.Deserializar(almacen["partida_afectada_999"]),
            new[] { "leah" });
        Assert.True(fallo.GuardadoBloqueado);
        Assert.False(gestor.Guardar(e => almacen["partida_afectada_999"] = PersistenciaEstado.Serializar(e)).Guardado);

        // Luego se abre una partida legible (incluso sin llamar Limpiar() explícitamente): debe poder guardar.
        var cargaLegible = gestor.Cargar(
            "partida_legible_333",
            () => PersistenciaEstado.Deserializar(almacen["partida_legible_333"]),
            new[] { "leah" });

        Assert.True(cargaLegible.Exito);
        Assert.False(cargaLegible.EsPartidaNueva);
        Assert.False(cargaLegible.GuardadoBloqueado);
        Assert.False(gestor.GuardadoBloqueado);
        Assert.Null(gestor.PartidaAfectada);

        gestor.EstadoActual.ObtenerOIniciar("leah").AgregarEpisodio(12, 1700, "Tarde tranquila en la cabaña.", 0.76);
        var guardadoLegible = gestor.Guardar(e => almacen["partida_legible_333"] = PersistenciaEstado.Serializar(e));

        Assert.True(guardadoLegible.Guardado);
        Assert.False(guardadoLegible.OmitidoPorProteccion);
        var recargadaLegible = PersistenciaEstado.Deserializar(almacen["partida_legible_333"]);
        Assert.Equal(2, recargadaLegible.ObtenerSiExiste("leah")!.Diario.Count);
        Assert.Equal(jsonFutura, almacen["partida_afectada_999"]);

        // Volver al título y abrir una partida nueva (sin datos previos en clave): inicio válido y guardable.
        gestor.Limpiar();
        var cargaNueva = gestor.Cargar(
            "partida_nueva_444",
            () => almacen.TryGetValue("partida_nueva_444", out var raw) ? PersistenciaEstado.Deserializar(raw) : null,
            new[] { "leah" });

        Assert.True(cargaNueva.Exito);
        Assert.True(cargaNueva.EsPartidaNueva);
        Assert.False(cargaNueva.GuardadoBloqueado);
        gestor.EstadoActual.ObtenerOIniciar("leah").AgregarEpisodio(1, 900, "Primer día en el valle.", 0.65);

        var guardadoNueva = gestor.Guardar(e => almacen["partida_nueva_444"] = PersistenciaEstado.Serializar(e));
        Assert.True(guardadoNueva.Guardado);

        var recargadaNueva = PersistenciaEstado.Deserializar(almacen["partida_nueva_444"]);
        Assert.Single(recargadaNueva.ObtenerSiExiste("leah")!.Diario);
        Assert.Equal(jsonFutura, almacen["partida_afectada_999"]);
    }

    [Fact]
    public void Hook_pre_guardado_vuelca_el_estado_antes_de_checkpoint_y_respeta_proteccion_cp110()
    {
        var almacen = new Dictionary<string, string>(StringComparer.Ordinal);
        var gestor = new GestorPersistenciaPartida();

        var carga = gestor.Cargar(
            "partida_checkpoint",
            () => almacen.TryGetValue("partida_checkpoint", out var raw) ? PersistenciaEstado.Deserializar(raw) : null,
            new[] { "leah" });
        Assert.True(carga.Exito);

        gestor.EstadoActual.ObtenerOIniciar("leah").AgregarEpisodio(3, 1430, "Dibujando en el claro antes del checkpoint.", 0.7);
        Action hookPreGuardado = () => gestor.Guardar(e => almacen["partida_checkpoint"] = PersistenciaEstado.Serializar(e));

        hookPreGuardado();

        Assert.True(almacen.ContainsKey("partida_checkpoint"));
        var recargada = PersistenciaEstado.Deserializar(almacen["partida_checkpoint"]);
        Assert.Single(recargada.ObtenerSiExiste("leah")!.Diario);

        // Si la carga posterior falla por versión futura, el hook pre-guardado tampoco sobrescribe.
        almacen["partida_futura"] = """{ "version_esquema": 99, "personajes": {} }""";
        gestor.Limpiar();
        var cargaFutura = gestor.Cargar(
            "partida_futura",
            () => JsonConvert.DeserializeObject<EstadoPartida>(almacen["partida_futura"]),
            new[] { "leah" });
        Assert.False(cargaFutura.Exito);

        var escrituras = 0;
        var res = gestor.Guardar(_ => escrituras++);
        Assert.True(res.OmitidoPorProteccion);
        Assert.Equal(0, escrituras);
    }
}
