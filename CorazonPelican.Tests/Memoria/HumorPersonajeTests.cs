using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Memoria;

/// <summary>
/// Pruebas de conservación, origen, atenuación y continuidad del humor entre días (P-9, CP-210, CP-905).
/// </summary>
public class HumorPersonajeTests
{
    private static readonly JsonSerializerOptions JsonApi = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static PerfilPersonaje CargarPerfil()
    {
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        perfil.Lexico.Lugares.Single(l => l.Id == "pasarela_lago").Area = new() { 40, 30, 1, 1 };
        perfil.Lexico.Lugares.Single(l => l.Id == "orilla_norte").Area = new() { 40, 18, 1, 1 };
        return perfil;
    }

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static HorarioDia CrearHorario(string clave, params (int Hora, string Ubicacion, int X, int Y)[] entradas)
        => new(clave, entradas.Select(e => new EntradaHorario(e.Hora, e.Ubicacion, e.X, e.Y)).ToList());

    private static int ObtenerPuertoLibre()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void AvanzarRelojHasta(RelojVirtual reloj, int diaObjetivo, int horaObjetivo)
    {
        var paso = HoraJuego.MilisegundosPorDiezMinutos(reloj.Modo);
        var guardia = 0;
        while ((reloj.Juego.Dia < diaObjetivo || (reloj.Juego.Dia == diaObjetivo && reloj.Juego.Hora < horaObjetivo))
               && guardia++ < 5_000)
        {
            reloj.Avanzar(paso);
        }
    }

    private static async Task<HttpResponseMessage> PostDrenandoAsync(HttpClient http, ApiMotor api, Respuesta respuesta)
    {
        var contenido = new StringContent(System.Text.Json.JsonSerializer.Serialize(respuesta, JsonApi), Encoding.UTF8, "application/json");
        var tarea = http.PostAsync("respuestas", contenido);
        for (var i = 0; i < 500 && !tarea.IsCompleted; i++)
        {
            api.DrenarCola();
            await Task.Delay(2);
        }

        api.DrenarCola();
        return await tarea;
    }

    private static async Task<string> GetDrenandoAsync(HttpClient http, ApiMotor api, string ruta)
    {
        var tarea = http.GetStringAsync(ruta);
        for (var i = 0; i < 500 && !tarea.IsCompleted; i++)
        {
            api.DrenarCola();
            await Task.Delay(2);
        }

        api.DrenarCola();
        return await tarea;
    }

    [Fact]
    public void Impresion_vinculada_a_episodio_llega_al_despertar_tras_guardar_y_cargar_y_rechaza_origen_o_epoca_invalidos()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var almacen = new Dictionary<string, EstadoPartida>(StringComparer.Ordinal);
        var gestor1 = new GestorPersistenciaPartida();
        var carga1 = gestor1.Cargar("humor_save", () => null, new[] { perfil.Id });
        var estado1 = carga1.Estado.ObtenerOIniciar(perfil.Id);

        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });
        var motor1 = new MotorHabitante(perfil, plantillas, estado1, ModoReloj.Vanilla, publicarPulso: p => api.EmitirPulso(p))
        {
            Epoca = epoca,
        };
        CoordinadorApiAnfitrion.Conectar(
            api,
            id => id == perfil.Id ? motor1 : null,
            id => gestor1.EstadoActual.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        var mundo = Escenario.Pasarela().ALas(dia: 10, hora: 1730).Construir(perfil.Lexico);
        motor1.ProcesarPaso(mundo.Observar(), CrearHorario("day_10", (900, "Forest", 40, 30)), ahoraMs: 1_000, minutosJuego: 10);
        var acostarse = motor1.ProcesarFinDeDia(mundo.Observar(), ahoraMs: 2_000)!;
        var epId = acostarse.EpisodiosDiario!.Keys.First();

        // Origen desconocido se rechaza sin mutar estado.
        var rechazoOrigen = api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Pensamiento = "Algo inventado.",
            Epoca = epoca,
            BasadaEn = acostarse.Id,
            Humor = new HumorPropuesto
            {
                Resumen = "inquieta sin base",
                Intensidad = 0.6,
                OrigenDiario = new List<string> { "ep-inexistente" },
            },
        });
        Assert.Equal(Resultado.Rechazada, rechazoOrigen.Estado);
        Assert.Contains(rechazoOrigen.Motivos!, m => m.Codigo == "episodio_desconocido");
        Assert.Null(estado1.Humor);

        // Otra época se rechaza con epoca_obsoleta.
        var rechazoEpoca = api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Pensamiento = "Otra sesión.",
            Epoca = "epoca-ajena",
            BasadaEn = acostarse.Id,
            Humor = new HumorPropuesto
            {
                Resumen = "serena",
                Intensidad = 0.6,
                OrigenDiario = new List<string> { epId },
            },
        });
        Assert.Equal(Resultado.Rechazada, rechazoEpoca.Estado);
        Assert.Contains(rechazoEpoca.Motivos!, m => m.Codigo == ValidadorEpoca.CodigoEpocaObsoleta);
        Assert.Null(estado1.Humor);

        // Respuesta válida con origen verificado: se aplica y reintentar conserva un único efecto.
        var respValida = new Respuesta
        {
            Pensamiento = "La tarde junto al agua me dejó una quietud limpia.",
            Epoca = epoca,
            BasadaEn = acostarse.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new() { Texto = "Me serenó mirar el lago en silencio.", OrigenDiario = new List<string> { epId } },
                },
            },
            Humor = new HumorPropuesto
            {
                Resumen = "serena tras la tarde junto al agua",
                Intensidad = 0.80,
                OrigenDiario = new List<string> { epId },
            },
        };
        Assert.Equal(Resultado.Aceptada, api.ProcesarRespuestaEnHiloPrincipal(respValida).Estado);
        Assert.Equal(Resultado.Aceptada, api.ProcesarRespuestaEnHiloPrincipal(respValida).Estado);
        Assert.Single(estado1.Recuerdos);
        Assert.NotNull(estado1.Humor);

        // Guardar y recargar partida.
        gestor1.Guardar(p => almacen[PersistenciaEstado.ClaveGuardado] = PersistenciaEstado.Deserializar(PersistenciaEstado.Serializar(p)));
        var gestor2 = new GestorPersistenciaPartida();
        var carga2 = gestor2.Cargar("humor_save", () => almacen[PersistenciaEstado.ClaveGuardado], new[] { perfil.Id });
        var estado2 = carga2.Estado.ObtenerOIniciar(perfil.Id);
        Assert.NotNull(estado2.Humor);

        var motor2 = new MotorHabitante(perfil, plantillas, estado2, ModoReloj.Vanilla) { Epoca = ApiMotor.GenerarEpoca() };
        var despertar = motor2.ProcesarPaso(
            new Observacion(11, 600, "LeahHouse", 5, 5, 1.0, "soleado", Array.Empty<EntidadObservada>()),
            CrearHorario("day_11", (900, "Forest", 40, 30)),
            ahoraMs: 3_000,
            minutosJuego: 0).Pulsos.Single(p => p.Hito?.Id == HitosRutina.Despertar);

        Assert.NotNull(despertar.Humor);
        Assert.Equal("serena tras la tarde junto al agua", despertar.Humor!.Resumen);
        Assert.Equal(new[] { epId }, despertar.Humor.OrigenDiario);
        Assert.True(despertar.Humor.Intensidad > 0.5 && despertar.Humor.Intensidad <= perfil.ObtenerConfigHumor().MaxIntensidad);
    }

    [Fact]
    public void RelojVirtual_atenua_humor_hasta_disiparse_conserva_recuerdos_y_respeta_tope_sustitucion_y_partidas_previas()
    {
        var perfil = CargarPerfil();
        var cfg = perfil.ObtenerConfigHumor();
        Assert.Equal(2.5, cfg.SemividaDias);
        Assert.Equal(0.85, cfg.MaxIntensidad);
        Assert.Equal(0.15, cfg.UmbralDisipacion);
        Assert.Equal(0.65, cfg.UmbralSustitucion);

        // Compatibilidad con partida v2 anterior sin campo humor.
        const string jsonPrevio = """
            {
              "version_esquema": 2,
              "personajes": {
                "leah": {
                  "version_esquema": 2,
                  "siguiente_id_episodio": 2,
                  "diario": [
                    { "id": "ep-1", "dia_juego": 1, "hora": 1200, "resumen_prosa": "Dibujaste cerca del puente.", "saliencia": 0.7, "entidades": [] }
                  ],
                  "recuerdos": [
                    { "id": "rec-1", "texto": "Aquel puente me inspiró.", "importancia": 0.7, "origen_diario": ["ep-1"] }
                  ]
                }
              }
            }
            """;
        var partidaAntigua = PersistenciaEstado.Deserializar(jsonPrevio);
        var erroresAntigua = PersistenciaEstado.Validar(partidaAntigua);
        Assert.Empty(erroresAntigua);
        var estado = partidaAntigua.ObtenerSiExiste(perfil.Id)!;
        Assert.Null(estado.Humor);
        Assert.Null(GestorHumor.ObtenerVigente(estado, new TiempoJuego { Dia = 2, Hora = 900 }, cfg));

        // La mente propone intensidad 1.0 -> el host acota a MaxIntensidad (0.85).
        var ep2 = estado.AgregarEpisodio(3, 1600, "Encontró luz dorada en el lago.", 0.9, new[] { "luz" });
        var reloj = new RelojVirtual(ModoReloj.Vanilla, dia: 3, hora: 1600);
        var pulsoInicio = new Pulso
        {
            Id = 10,
            Epoca = "ep-reloj",
            Personaje = perfil.Id,
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = reloj.Juego.Dia, Hora = reloj.Juego.Hora },
        };
        var resInicio = GestorHumor.AplicarPropuesta(estado, pulsoInicio, new Respuesta
        {
            Pensamiento = "Qué claridad más rara sobre el agua.",
            Epoca = "ep-reloj",
            BasadaEn = 10,
            Humor = new HumorPropuesto
            {
                Resumen = "inspirada por la luz dorada",
                Intensidad = 1.0,
                OrigenDiario = new List<string> { ep2.Id },
            },
        }, cfg);
        Assert.True(resInicio.Aceptada);
        Assert.Equal(0.85, resInicio.HumorVigente!.Intensidad);

        // Atenuación tras 2.5 días tranquilos (de día 3 16:00 a día 6 06:00).
        AvanzarRelojHasta(reloj, diaObjetivo: 6, horaObjetivo: 600);
        var trasDosDiasYMedio = GestorHumor.ObtenerVigente(estado, new TiempoJuego { Dia = reloj.Juego.Dia, Hora = reloj.Juego.Hora }, cfg);
        Assert.NotNull(trasDosDiasYMedio);
        Assert.InRange(trasDosDiasYMedio!.Intensidad, 0.35, 0.48);

        // Guardar y recargar conserva la evolución exacta por tiempo de juego.
        var jsonGuardado = PersistenciaEstado.Serializar(partidaAntigua);
        var recargada = PersistenciaEstado.Deserializar(jsonGuardado);
        var estadoRecargado = recargada.ObtenerSiExiste(perfil.Id)!;
        var humorRecargado = GestorHumor.ObtenerVigente(estadoRecargado, new TiempoJuego { Dia = reloj.Juego.Dia, Hora = reloj.Juego.Hora }, cfg);
        Assert.Equal(trasDosDiasYMedio.Intensidad, humorRecargado!.Intensidad);

        // Tras muchos días tranquilos (día 12), cae por debajo de UmbralDisipacion (0.15) y se disipa, pero los recuerdos permanecen.
        var recCopy = estadoRecargado.AgregarRecuerdo("El brillo del agua aquel día merecía recordarse.", 0.85, new[] { ep2.Id });
        AvanzarRelojHasta(reloj, diaObjetivo: 12, horaObjetivo: 1200);
        Assert.Null(GestorHumor.ObtenerVigente(estadoRecargado, new TiempoJuego { Dia = reloj.Juego.Dia, Hora = reloj.Juego.Hora }, cfg));
        Assert.Contains(estadoRecargado.Recuerdos, r => r.Id == recCopy.Id);

        // Sustitución anticipada cuando hay una experiencia significativa antes de disiparse.
        var ep3 = estadoRecargado.AgregarEpisodio(12, 1400, "Se partió la madera buena en la mesa.", 0.88);
        var ep4 = estadoRecargado.AgregarEpisodio(12, 1800, "Ana trajo pigmentos nuevos al taller.", 0.95);
        GestorHumor.AplicarPropuesta(estadoRecargado, new Pulso
        {
            Id = 20,
            Epoca = "ep-reloj",
            Personaje = perfil.Id,
            Motivo = Motivos.Pico,
            TiempoJuego = new TiempoJuego { Dia = 12, Hora = 1500 },
        }, new Respuesta
        {
            Pensamiento = "Qué rabia haber estropeado la talla.",
            Epoca = "ep-reloj",
            BasadaEn = 20,
            Humor = new HumorPropuesto
            {
                Resumen = "frustrada con la madera",
                Intensidad = 0.6,
                OrigenDiario = new List<string> { ep3.Id },
            },
        }, cfg);

        var resSustitucion = GestorHumor.AplicarPropuesta(estadoRecargado, new Pulso
        {
            Id = 21,
            Epoca = "ep-reloj",
            Personaje = perfil.Id,
            Motivo = Motivos.Interlocucion,
            TiempoJuego = new TiempoJuego { Dia = 12, Hora = 1800 },
        }, new Respuesta
        {
            Pensamiento = "Este gesto inesperado lo cambia todo.",
            Epoca = "ep-reloj",
            BasadaEn = 21,
            Humor = new HumorPropuesto
            {
                Resumen = "agradecida e ilusionada",
                Intensidad = 0.55,
                OrigenDiario = new List<string> { ep4.Id },
                Significativa = true,
            },
        }, cfg);

        Assert.True(resSustitucion.Aplicada);
        Assert.Equal("agradecida e ilusionada", resSustitucion.HumorVigente!.Resumen);
        Assert.Equal(new[] { ep4.Id }, resSustitucion.HumorVigente.OrigenDiario);
    }

    [Fact]
    public async Task Prueba_de_dos_dias_CP905_por_api_y_componentes_del_host_conserva_episodio_y_humor_tras_recargar()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var discoSimulado = new Dictionary<string, string>(StringComparer.Ordinal);
        var gestor1 = new GestorPersistenciaPartida();
        var estado1 = gestor1.Cargar("cp905", () => null, new[] { perfil.Id }).Estado.ObtenerOIniciar(perfil.Id);

        var api1 = new ApiMotor("0.1.0");
        var epoca1 = ApiMotor.GenerarEpoca();
        api1.IniciarEpoca(epoca1, new[] { perfil.Id });
        var motor1 = new MotorHabitante(perfil, plantillas, estado1, ModoReloj.Vanilla, publicarPulso: p => api1.EmitirPulso(p))
        {
            Epoca = epoca1,
        };
        CoordinadorApiAnfitrion.Conectar(
            api1,
            id => id == perfil.Id ? motor1 : null,
            id => gestor1.EstadoActual.ObtenerSiExiste(id),
            enHiloPrincipal: true);

        var puerto1 = ObtenerPuertoLibre();
        using var servidor1 = new ServidorApiMotor(api1, puerto1);
        servidor1.Iniciar();
        using var http1 = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto1}/api/v1/") };

        // 1. Tarde a solas en FakeWorld -> episodio identificable.
        var mundo = Escenario.Pasarela()
            .ALas(dia: 68, hora: 1700)
            .ConHorarios((dia, _) => CrearHorario($"d{dia}", (900, "Forest", 40, 30), (1600, "Forest", 40, 18)))
            .Construir(perfil.Lexico);
        motor1.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs: 1_000, minutosJuego: 15);
        var acostarse = motor1.ProcesarFinDeDia(mundo.Observar(), ahoraMs: 2_000, horario: mundo.HorarioHoy)!;
        var idEpisodio = acostarse.EpisodiosDiario!.Keys.First();

        // Guardado antes de la respuesta de acostarse + cambio de día.
        gestor1.Guardar(p => discoSimulado[PersistenciaEstado.ClaveGuardado] = PersistenciaEstado.Serializar(p));
        motor1.ProcesarPaso(
            new Observacion(69, 600, "LeahHouse", 5, 5, 1.0, "soleado", Array.Empty<EntidadObservada>()),
            mundo.HorarioHoy,
            ahoraMs: 3_000,
            minutosJuego: 0);

        // 2. Entrega tardía en la misma época vía HTTP del host (reflexión + humor).
        var respNocturna = new Respuesta
        {
            Pensamiento = "La tarde sola me dio aire para pensar el próximo dibujo.",
            Epoca = epoca1,
            BasadaEn = acostarse.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new() { Texto = "A solas junto al agua decidí volver a la orilla norte.", OrigenDiario = new List<string> { idEpisodio } },
                },
            },
            Humor = new HumorPropuesto
            {
                Resumen = "reflexiva y con impulso por volver al lago",
                Intensidad = 0.78,
                OrigenDiario = new List<string> { idEpisodio },
            },
        };
        var httpPost = await PostDrenandoAsync(http1, api1, respNocturna);
        var resPost = System.Text.Json.JsonSerializer.Deserialize<Resultado>(await httpPost.Content.ReadAsStringAsync())!;
        Assert.Equal(Resultado.Aceptada, resPost.Estado);

        // 3. Siguiente guardado y recarga completa en sesión nueva.
        gestor1.Guardar(p => discoSimulado[PersistenciaEstado.ClaveGuardado] = PersistenciaEstado.Serializar(p));
        servidor1.Dispose();

        var gestor2 = new GestorPersistenciaPartida();
        var estado2 = gestor2.Cargar(
            "cp905",
            () => PersistenciaEstado.Deserializar(discoSimulado[PersistenciaEstado.ClaveGuardado]),
            new[] { perfil.Id }).Estado.ObtenerOIniciar(perfil.Id);

        var api2 = new ApiMotor("0.1.0");
        var epoca2 = ApiMotor.GenerarEpoca();
        api2.IniciarEpoca(epoca2, new[] { perfil.Id });
        var motor2 = new MotorHabitante(perfil, plantillas, estado2, ModoReloj.Vanilla, publicarPulso: p => api2.EmitirPulso(p))
        {
            Epoca = epoca2,
        };
        CoordinadorApiAnfitrion.Conectar(
            api2,
            id => id == perfil.Id ? motor2 : null,
            id => gestor2.EstadoActual.ObtenerSiExiste(id),
            enHiloPrincipal: true);

        var puerto2 = ObtenerPuertoLibre();
        using var servidor2 = new ServidorApiMotor(api2, puerto2);
        servidor2.Iniciar();
        using var http2 = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto2}/api/v1/") };

        // Avanza al día 69 tarde (hito ordinario en la misma escena de la pasarela).
        var horario69 = CrearHorario("Fri", (900, "Forest", 40, 30), (1600, "Forest", 40, 18), (1900, "Town", 10, 10));
        motor2.ProcesarPaso(mundo.Observar(), horario69, ahoraMs: 4_000, minutosJuego: 10);
        var forzadoTarde = motor2.ForzarPulso(
            new Observacion(69, 1700, "Mountain", 40, 30, 0.9, "soleado", Array.Empty<EntidadObservada>()),
            ahoraMs: 5_000,
            motivo: Motivos.Deriva,
            horario: horario69);
        var pulsoTarde = forzadoTarde.Pulso!;

        Assert.NotNull(pulsoTarde.Humor);
        Assert.Equal("reflexiva y con impulso por volver al lago", pulsoTarde.Humor!.Resumen);
        Assert.Equal(new[] { idEpisodio }, pulsoTarde.Humor.OrigenDiario);

        var instantanea = System.Text.Json.JsonSerializer.Deserialize<InstantaneaMemoria>(
            await GetDrenandoAsync(http2, api2, $"memoria/{perfil.Id}"))!;
        var rec = Assert.Single(instantanea.Recuerdos);
        Assert.Contains(idEpisodio, rec.OrigenDiario);
        Assert.NotNull(instantanea.Humor);
        Assert.Equal(new[] { idEpisodio }, instantanea.Humor!.OrigenDiario);
    }

    [Fact]
    public void Misma_tarde_con_distinta_experiencia_previa_entrega_contexto_diferenciado_y_elecciones_dentro_del_pulso()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var horario = CrearHorario("Fri", (900, "Forest", 40, 30), (1600, "Saloon", 2, 20));
        var obsTarde = Escenario.Pasarela().ALas(dia: 15, hora: 1700).Construir(perfil.Lexico).Observar();

        // Tarde A: viene de una tarde de serenidad junto al lago.
        var estadoSereno = new EstadoPersonaje();
        var epA = estadoSereno.AgregarEpisodio(14, 1700, "Dibujaste sin prisas junto a la pasarela.", 0.85, new[] { "agua:Mountain:lago" });
        estadoSereno.AgregarRecuerdo("La luz de la pasarela me dejó serenidad.", 0.85, new[] { epA.Id });
        estadoSereno.Humor = new EstadoHumor
        {
            Resumen = "serena con ganas de volver al boceto",
            IntensidadInicial = 0.78,
            OrigenDiario = new List<string> { epA.Id },
            CreadoEn = new TiempoJuego { Dia = 14, Hora = 2200 },
        };
        var motorA = new MotorHabitante(perfil, plantillas, estadoSereno, ModoReloj.Vanilla) { Epoca = "ep-A" };
        var pulsoA = motorA.ForzarPulso(obsTarde, ahoraMs: 1_000, motivo: Motivos.Deriva, horario: horario).Pulso!;

        // Tarde B: viene de un desencuentro incómodo, pero con perfil, horario, lugar y opciones idénticos.
        var estadoTenso = new EstadoPersonaje();
        var epB = estadoTenso.AgregarEpisodio(14, 1900, "Discutiste de forma seca y te marchaste pronto.", 0.90);
        estadoTenso.AgregarRecuerdo("Ayer me fui incómoda tras una charla tensa.", 0.90, new[] { epB.Id });
        estadoTenso.Humor = new EstadoHumor
        {
            Resumen = "retraída y necesitada de silencio",
            IntensidadInicial = 0.78,
            OrigenDiario = new List<string> { epB.Id },
            CreadoEn = new TiempoJuego { Dia = 14, Hora = 2200 },
        };
        var motorB = new MotorHabitante(perfil, plantillas, estadoTenso, ModoReloj.Vanilla) { Epoca = "ep-B" };
        var pulsoB = motorB.ForzarPulso(obsTarde, ahoraMs: 1_000, motivo: Motivos.Deriva, horario: horario).Pulso!;

        // Mismo lugar, horario y acciones ofrecidas; solo cambia el humor nacido de lo vivido.
        Assert.Equal(pulsoA.Escena, pulsoB.Escena);
        Assert.Equal(pulsoA.AccionesDisponibles, pulsoB.AccionesDisponibles);
        Assert.Equal("serena con ganas de volver al boceto", pulsoA.Humor!.Resumen);
        Assert.Equal("retraída y necesitada de silencio", pulsoB.Humor!.Resumen);

        // Las dos elecciones distintas propuestos solo usan acciones y lugares del pulso.
        var eleccionA = new Respuesta
        {
            Pensamiento = "Me quedo dibujando en la pasarela aprovechando esta calma.",
            Epoca = "ep-A",
            BasadaEn = pulsoA.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "dibujar en la pasarela",
                Porque = "vengo serena desde ayer",
                Plan = new List<PasoPlan>
                {
                    new() { Accion = "sentarse", Lugar = "pasarela_lago" },
                    new() { Accion = "dibujar", Obra = "nueva" },
                },
            },
        };
        var eleccionB = new Respuesta
        {
            Pensamiento = "Prefiero recoger el cuaderno y volver tranquila hacia casa.",
            Epoca = "ep-B",
            BasadaEn = pulsoB.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "volver a casa a solas",
                Porque = "hoy necesito poco ruido",
                Plan = new List<PasoPlan>
                {
                    new() { Accion = "guardar_cuaderno" },
                    new() { Accion = "ir_a", Lugar = "tu_casa" },
                },
            },
        };

        Assert.Equal(Resultado.Aceptada, motorA.AplicarRespuesta(pulsoA, eleccionA).Estado);
        Assert.Equal(Resultado.Aceptada, motorB.AplicarRespuesta(pulsoB, eleccionB).Estado);
    }
}
