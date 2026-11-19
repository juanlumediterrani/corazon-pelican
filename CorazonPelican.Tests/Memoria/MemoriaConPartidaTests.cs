using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Memoria;

/// <summary>Guarda de R-10 (P-6, CP-107): la memoria se persiste y restaura con la partida.</summary>
public class MemoriaConPartidaTests
{
    private static PerfilPersonaje Perfil()
    {
        var ruta = Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json");
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(ruta))!;
        perfil.Presupuesto.Vanilla.PulsosPorDiaJuego = 4; // 2 cadenas + 2 ordinarios libres.
        perfil.Presupuesto.Vanilla.ExtraCompromisos = 1;
        return perfil;
    }

    [Fact]
    public void Guardar_anadir_episodios_y_recargar_sin_guardar_restaura_ultimo_guardado()
    {
        var perfil = Perfil();
        var mundo = Escenario.Pasarela().ALas(dia: 68, hora: 900).Construir();
        var bucle = new BucleAtencion(mundo, perfil);
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);

        bucle.Avanzar(TimeSpan.FromSeconds(14));
        var ep1 = estado.AgregarEpisodio(
            mundo.Reloj.Juego.Dia,
            mundo.Reloj.Juego.Hora,
            "Observas el lago en calma desde la pasarela.",
            0.62,
            new[] { "agua:Mountain:lago" });
        var ep2 = estado.AgregarEpisodio(
            mundo.Reloj.Juego.Dia,
            1100,
            "Anotas un boceto rápido de la orilla.",
            0.71,
            new[] { "obj:Mountain:40,30:pasarela" });
        estado.Contadores = bucle.Disparadores.Contadores;

        // Simula GameLoop.Saving en el anfitrión.
        var guardadoJson = PersistenciaEstado.Serializar(partida);

        // Avanza la partida y añade episodios sin volver a guardar.
        bucle.Avanzar(TimeSpan.FromSeconds(28));
        estado.AgregarEpisodio(
            mundo.Reloj.Juego.Dia,
            1600,
            "Conversas en el sendero antes de cerrar sin guardar.",
            0.85,
            new[] { "farmer:1" });
        estado.AgregarEpisodio(
            mundo.Reloj.Juego.Dia,
            1900,
            "Otro episodio que no debería persistir tras recargar.",
            0.78);
        bucle.Interlocucion("farmer:1");
        estado.Contadores = bucle.Disparadores.Contadores;
        Assert.Equal(4, estado.Diario.Count);

        // Simula GameLoop.SaveLoaded del último guardado (sin guardar los episodios nuevos).
        var recargada = PersistenciaEstado.Deserializar(guardadoJson);
        Assert.Empty(PersistenciaEstado.Validar(recargada));

        var restaurado = recargada.ObtenerSiExiste(perfil.Id)!;
        Assert.NotNull(restaurado);
        Assert.Equal(new[] { ep1.Id, ep2.Id }, restaurado.Diario.Select(e => e.Id));
        Assert.Equal(
            new[] { "Observas el lago en calma desde la pasarela.", "Anotas un boceto rápido de la orilla." },
            restaurado.Diario.Select(e => e.ResumenProsa));
        Assert.DoesNotContain(restaurado.Diario, e => e.ResumenProsa.Contains("sin guardar", StringComparison.Ordinal));
        Assert.Equal(3, restaurado.SiguienteIdEpisodio);
        Assert.Equal(0, restaurado.Contadores?.Interlocuciones ?? 0);

        // Al seguir viviendo tras la recarga, el siguiente id continúa desde el guardado (ep-3).
        var ep3 = restaurado.AgregarEpisodio(68, 1300, "Retomas el cuaderno tras cargar.", 0.65);
        Assert.Equal("ep-3", ep3.Id);
    }

    [Fact]
    public void Estado_v1_serializado_a_mano_migra_a_version_actual_sin_perder_episodios_ni_contadores()
    {
        // v1 explícito con contadores, pero sin siguiente_id_episodio, entidades, recuerdos ni relaciones.
        const string jsonV1ConContadores = """
        {
          "version_esquema": 1,
          "personajes": {
            "leah": {
              "version_esquema": 1,
              "diario": [
                {
                  "id": "ep-1",
                  "dia_juego": 14,
                  "hora": 920,
                  "resumen_prosa": "Miras la niebla sobre el río.",
                  "saliencia": 0.66
                },
                {
                  "id": "ep-4",
                  "dia_juego": 14,
                  "hora": 1740,
                  "resumen_prosa": "Guardas los lápices al anochecer.",
                  "saliencia": 0.74
                }
              ],
              "contadores": {
                "Dia": 14,
                "OrdinariosLibresGastados": 5,
                "ExtrasCompromisosGastados": 1,
                "DespertarEmitido": true,
                "AcostarseEmitido": false,
                "Interlocuciones": 2
              }
            }
          }
        }
        """;

        var migrado = PersistenciaEstado.Deserializar(jsonV1ConContadores);
        Assert.Empty(PersistenciaEstado.Validar(migrado));
        Assert.Equal(EstadoPartida.VersionActual, migrado.VersionEsquema);

        var personaje = migrado.ObtenerSiExiste("leah")!;
        Assert.NotNull(personaje);
        Assert.Equal(EstadoPersonaje.VersionActual, personaje.VersionEsquema);
        Assert.Equal(2, personaje.Diario.Count);
        Assert.Equal(new[] { "ep-1", "ep-4" }, personaje.Diario.Select(e => e.Id));
        Assert.All(personaje.Diario, e => Assert.NotNull(e.Entidades));
        Assert.Equal(5, personaje.SiguienteIdEpisodio);
        Assert.NotNull(personaje.Recuerdos);
        Assert.NotNull(personaje.Relaciones);
        Assert.NotNull(personaje.Portafolio);
        Assert.NotNull(personaje.Compromisos);

        Assert.Equal(
            new ContadoresPulsos(14, 5, 1, DespertarEmitido: true, AcostarseEmitido: false, Interlocuciones: 2),
            personaje.Contadores);

        var nuevoEp = personaje.AgregarEpisodio(14, 1900, "Nuevo episodio tras migrar.", 0.55);
        Assert.Equal("ep-5", nuevoEp.Id);

        // v1 mínimo sin version_esquema ni contadores ni ids en episodios heredados.
        const string jsonV1SinVersionNiContadores = """
        {
          "personajes": {
            "artista": {
              "diario": [
                {
                  "dia_juego": 3,
                  "hora": 800,
                  "resumen_prosa": "Primera entrada heredada sin id ni entidades.",
                  "saliencia": 0.5
                }
              ]
            }
          }
        }
        """;

        var migradoMinimo = PersistenciaEstado.Deserializar(jsonV1SinVersionNiContadores);
        Assert.Empty(PersistenciaEstado.Validar(migradoMinimo));
        Assert.Equal(EstadoPartida.VersionActual, migradoMinimo.VersionEsquema);
        var artista = migradoMinimo.ObtenerSiExiste("artista")!;
        Assert.Equal(EstadoPersonaje.VersionActual, artista.VersionEsquema);
        Assert.Single(artista.Diario);
        Assert.Equal("ep-1", artista.Diario[0].Id);
        Assert.NotNull(artista.Diario[0].Entidades);
        Assert.Equal(2, artista.SiguienteIdEpisodio);
        Assert.Null(artista.Contadores);

        // Una versión futura no soportada falla explícitamente.
        const string jsonVersionFutura = """{ "version_esquema": 99, "personajes": {} }""";
        Assert.Throws<NotSupportedException>(() => PersistenciaEstado.Deserializar(jsonVersionFutura));
    }

    [Fact]
    public void Dos_dias_en_FakeWorld_conservan_episodios_orden_ids_estables_y_contadores_tras_guardar_y_recargar()
    {
        var perfil = Perfil();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);

        // --- Día 10 ---
        var mundoDia1 = Escenario.Pasarela().ALas(dia: 10, hora: HoraJuego.InicioDia).Construir();
        var bucleDia1 = new BucleAtencion(mundoDia1, perfil);

        bucleDia1.Avanzar(TimeSpan.FromSeconds(14));
        bucleDia1.Disparadores.Compromiso("comp-1", mundoDia1.Observar(), mundoDia1.Reloj.MilisegundosReales);
        bucleDia1.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
        bucleDia1.Interlocucion("farmer:1");

        var buferDia1 = new Bufer();
        buferDia1.RegistrarPercepcion(10, 700, Banda.Mencion, 0.55, "Día 10 mañana: el agua refleja el cielo claro.", "agua:Mountain:lago");
        buferDia1.RegistrarAccion(10, 1400, "Día 10 tarde: trazas la pasarela de madera.", 0.72);
        var creadosDia1 = Consolidacion.RegistrarDesdeBufer(estado, buferDia1);
        estado.Contadores = bucleDia1.Disparadores.Contadores;

        Assert.Equal(new[] { "ep-1", "ep-2" }, creadosDia1.Select(e => e.Id));
        var guardadoDia1 = PersistenciaEstado.Serializar(partida);

        // --- Día 11 ---
        var mundoDia2 = Escenario.Pasarela().ALas(dia: 11, hora: HoraJuego.InicioDia).Construir();
        var bucleDia2 = new BucleAtencion(mundoDia2, perfil, guardados: estado.Contadores);

        bucleDia2.Avanzar(TimeSpan.FromSeconds(14));
        bucleDia2.Disparadores.Compromiso("comp-2", mundoDia2.Observar(), mundoDia2.Reloj.MilisegundosReales);
        bucleDia2.Disparadores.Compromiso("comp-3", mundoDia2.Observar(), mundoDia2.Reloj.MilisegundosReales);
        bucleDia2.Disparadores.Compromiso("comp-4", mundoDia2.Observar(), mundoDia2.Reloj.MilisegundosReales);
        bucleDia2.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));

        var buferDia2 = new Bufer();
        buferDia2.RegistrarPercepcion(11, 800, Banda.Foco, 0.81, "Día 11 mañana: la niebla cubre el borde norte.", "agua:Mountain:lago");
        buferDia2.RegistrarAccion(11, 1800, "Día 11 tarde: cierras el cuaderno antes de regresar.", 0.69);
        var creadosDia2 = Consolidacion.RegistrarDesdeBufer(estado, buferDia2);
        estado.Contadores = bucleDia2.Disparadores.Contadores;

        Assert.Equal(new[] { "ep-3", "ep-4" }, creadosDia2.Select(e => e.Id));
        var guardadoDia2 = PersistenciaEstado.Serializar(partida);

        // Recarga del guardado del Día 10: solo existen sus entradas, en orden, y su presupuesto ya gastado.
        var recargaDia1 = PersistenciaEstado.Deserializar(guardadoDia1);
        var estadoRecargado1 = recargaDia1.ObtenerSiExiste(perfil.Id)!;
        Assert.Equal(new[] { "ep-1", "ep-2" }, estadoRecargado1.Diario.Select(e => e.Id));
        Assert.Equal(10, estadoRecargado1.Contadores!.Dia);
        Assert.Equal(1, estadoRecargado1.Contadores.OrdinariosLibresGastados);
        Assert.True(estadoRecargado1.Contadores.DespertarEmitido);
        Assert.Equal(1, estadoRecargado1.Contadores.Interlocuciones);

        // El bucle reanudado en el mismo día 10 respeta los contadores restaurados (no reemite Despertar).
        var mundoReanudadoDia1 = Escenario.Pasarela().ALas(dia: 10, hora: 1500).Construir();
        var bucleReanudadoDia1 = new BucleAtencion(mundoReanudadoDia1, perfil, guardados: estadoRecargado1.Contadores);
        bucleReanudadoDia1.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
        Assert.DoesNotContain(bucleReanudadoDia1.Disparos, d => d.Hito?.Id == HitosRutina.Despertar);

        // Recarga del guardado del Día 11: ambos días coexisten en orden exacto con ids estables y contadores del Día 11.
        var recargaDia2 = PersistenciaEstado.Deserializar(guardadoDia2);
        Assert.Empty(PersistenciaEstado.Validar(recargaDia2));
        var estadoRecargado2 = recargaDia2.ObtenerSiExiste(perfil.Id)!;

        Assert.Equal(new[] { "ep-1", "ep-2", "ep-3", "ep-4" }, estadoRecargado2.Diario.Select(e => e.Id));
        Assert.Equal(
            new[]
            {
                "Día 10 mañana: el agua refleja el cielo claro.",
                "Día 10 tarde: trazas la pasarela de madera.",
                "Día 11 mañana: la niebla cubre el borde norte.",
                "Día 11 tarde: cierras el cuaderno antes de regresar.",
            },
            estadoRecargado2.Diario.Select(e => e.ResumenProsa));

        var proyDia1 = Consolidacion.ProyectarEpisodiosDelDia(estadoRecargado2, 10);
        var proyDia2 = Consolidacion.ProyectarEpisodiosDelDia(estadoRecargado2, 11);
        Assert.Equal(new[] { "ep-1", "ep-2" }, proyDia1.Keys);
        Assert.Equal(new[] { "ep-3", "ep-4" }, proyDia2.Keys);

        Assert.Equal(11, estadoRecargado2.Contadores!.Dia);
        Assert.Equal(2, estadoRecargado2.Contadores.OrdinariosLibresGastados);
        Assert.Equal(1, estadoRecargado2.Contadores.ExtrasCompromisosGastados);
        Assert.True(estadoRecargado2.Contadores.DespertarEmitido);
        Assert.Equal(5, estadoRecargado2.SiguienteIdEpisodio);
    }

    [Fact]
    public void Serializacion_es_determinista_y_conserva_todo_el_estado_del_personaje()
    {
        static EstadoPartida ConstruirPartida(bool ordenInverso)
        {
            var partida = new EstadoPartida();
            var ids = ordenInverso ? new[] { "p2", "p1" } : new[] { "p1", "p2" };
            foreach (var id in ids)
            {
                var e = partida.ObtenerOIniciar(id);
                e.AgregarEpisodio(5, 1000, $"Episodio de {id}", 0.7, new[] { "luz" });
                e.Recuerdos.Add(new Recuerdo
                {
                    Id = $"rec-{id}",
                    Texto = "Recuerdo consolidado",
                    Importancia = 0.8,
                    OrigenDiario = new List<string> { "ep-1" },
                });
                if (ordenInverso)
                {
                    e.Relaciones["farmer:2"] = new RelacionEstado { Impresion = "amable", UltimaVez = new TiempoJuego { Dia = 5, Hora = 900 } };
                    e.Relaciones["farmer:1"] = new RelacionEstado { Impresion = "atenta", UltimaVez = new TiempoJuego { Dia = 5, Hora = 1000 } };
                }
                else
                {
                    e.Relaciones["farmer:1"] = new RelacionEstado { Impresion = "atenta", UltimaVez = new TiempoJuego { Dia = 5, Hora = 1000 } };
                    e.Relaciones["farmer:2"] = new RelacionEstado { Impresion = "amable", UltimaVez = new TiempoJuego { Dia = 5, Hora = 900 } };
                }

                e.Compromisos.Add(new CompromisoEstado
                {
                    Id = "c-1",
                    Tipo = "cita",
                    Destinatario = "farmer:1",
                    Objeto = "orilla_norte",
                    PlazoVigente = new TiempoJuego { Dia = 6, Hora = 1700 },
                    Estado = "pendiente",
                });
                e.IntencionActiva = new IntencionActiva { Id = "int-1", Objetivo = "bocetar la orilla" };
                e.Contadores = new ContadoresPulsos(5, OrdinariosLibresGastados: 2, ExtrasCompromisosGastados: 1, DespertarEmitido: true);
            }

            return partida;
        }

        var jsonA = PersistenciaEstado.Serializar(ConstruirPartida(ordenInverso: false));
        var jsonB = PersistenciaEstado.Serializar(ConstruirPartida(ordenInverso: true));
        Assert.Equal(jsonA, jsonB);

        var restaurada = PersistenciaEstado.Deserializar(jsonA);
        var p1 = restaurada.ObtenerSiExiste("p1")!;
        Assert.Equal("rec-p1", Assert.Single(p1.Recuerdos).Id);
        Assert.Equal("atenta", p1.Relaciones["farmer:1"].Impresion);
        Assert.Equal("c-1", Assert.Single(p1.Compromisos).Id);
        Assert.Equal("int-1", p1.IntencionActiva!.Id);
        Assert.Equal(2, p1.Contadores!.OrdinariosLibresGastados);
    }

    [Fact]
    public void Ida_y_vuelta_con_Newtonsoft_Json_como_WriteSaveData_de_SMAPI_conserva_estado_y_esquema()
    {
        var partida = new EstadoPartida();
        var leah = partida.ObtenerOIniciar("leah");
        leah.AgregarEpisodio(8, 1230, "Pintaste junto al muelle.", 0.77, new[] { "agua:Mountain:lago" });
        leah.Contadores = new ContadoresPulsos(8, OrdinariosLibresGastados: 3, ExtrasCompromisosGastados: 1, DespertarEmitido: true, Interlocuciones: 1);
        leah.IntencionActiva = new IntencionActiva { Id = "int-4", Objetivo = "seguir dibujando" };
        leah.Relaciones["farmer:1"] = new RelacionEstado
        {
            Impresion = "buena compañía",
            UltimaVez = new TiempoJuego { Dia = 8, Hora = 1200 },
        };

        // SMAPI ReadSaveData / WriteSaveData usa Newtonsoft.Json internamente.
        var jsonNewtonsoft = JsonConvert.SerializeObject(partida);
        Assert.Contains("\"version_esquema\":2", jsonNewtonsoft, StringComparison.Ordinal);
        Assert.Contains("\"siguiente_id_episodio\":2", jsonNewtonsoft, StringComparison.Ordinal);

        var leidaConNewtonsoft = JsonConvert.DeserializeObject<EstadoPartida>(jsonNewtonsoft)!;
        Assert.False(MigradorEstado.Migrar(leidaConNewtonsoft));
        Assert.Empty(PersistenciaEstado.Validar(leidaConNewtonsoft));

        var leahLeida = leidaConNewtonsoft.ObtenerSiExiste("leah")!;
        Assert.Equal("ep-1", Assert.Single(leahLeida.Diario).Id);
        Assert.Equal(leah.Contadores, leahLeida.Contadores);
        Assert.Equal("int-4", leahLeida.IntencionActiva!.Id);
        Assert.Equal(1200, leahLeida.Relaciones["farmer:1"].UltimaVez!.Hora);

        // También debe poder leerse desde el deserializador puro de Core.
        var leidaConCore = PersistenciaEstado.Deserializar(jsonNewtonsoft);
        Assert.Equal(leah.Contadores, leidaConCore.ObtenerSiExiste("leah")!.Contadores);
    }
}
