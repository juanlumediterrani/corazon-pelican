using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Api;

/// <summary>
/// Pruebas de conexión entre <see cref="ApiMotor"/> y <see cref="MotorHabitante"/> como en el host (CP-213).
/// </summary>
public class AplicacionRespuestasHostTests
{
    private static int ObtenerPuertoLibre()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

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

    private static async Task<HttpResponseMessage> PostRespuestaDrenandoAsync(
        HttpClient http,
        ApiMotor motor,
        Respuesta respuesta)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(respuesta);
        var contenido = new StringContent(json, Encoding.UTF8, "application/json");
        var tareaHttp = http.PostAsync("respuestas", contenido);

        var intentos = 0;
        while (!tareaHttp.IsCompleted && intentos++ < 500)
        {
            motor.DrenarCola();
            await Task.Delay(2);
        }

        motor.DrenarCola();
        return await tareaHttp;
    }

    private static async Task<string> GetStringDrenandoAsync(
        HttpClient http,
        ApiMotor motor,
        string ruta)
    {
        var tareaHttp = http.GetStringAsync(ruta);
        var intentos = 0;
        while (!tareaHttp.IsCompleted && intentos++ < 500)
        {
            motor.DrenarCola();
            await Task.Delay(2);
        }

        motor.DrenarCola();
        return await tareaHttp;
    }

    [Fact]
    public async Task Registro_del_host_confirma_reflexion_en_memoria_y_repetir_conserva_una_sola_copia()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);
        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        var motores = new Dictionary<string, MotorHabitante>(StringComparer.Ordinal);
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Vanilla,
            publicarPulso: p => api.EmitirPulso(p))
        {
            Epoca = epoca,
        };
        motores[perfil.Id] = motor;

        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 5)
            .ALas(dia: 68, hora: 1700)
            .Construir(perfil.Lexico);

        CoordinadorApiAnfitrion.Conectar(
            api,
            id => motores.TryGetValue(id, out var m) ? m : null,
            id => partida.ObtenerSiExiste(id),
            enHiloPrincipal: true,
            observarEnVivo: _ => mundo.Observar());

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(api, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        // Acumulamos una escena en el diario y emitimos el hito de acostarse.
        var paso = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs: 1_000, minutosJuego: 10);
        Assert.NotEmpty(paso.Pulsos);
        var acostarse = motor.ProcesarFinDeDia(mundo.Observar(), ahoraMs: 2_000, horario: mundo.HorarioHoy);
        Assert.NotNull(acostarse);
        Assert.NotNull(acostarse!.EpisodiosDiario);
        Assert.NotEmpty(acostarse.EpisodiosDiario!);

        var idEpisodio = acostarse.EpisodiosDiario.Keys.First();
        var respuesta = new Respuesta
        {
            Pensamiento = "Una tarde serena junto al agua me aclara las ideas.",
            Epoca = epoca,
            BasadaEn = acostarse.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Aquella tarde frente al agua me dejó con ganas de volver temprano.",
                        OrigenDiario = new List<string> { idEpisodio },
                    },
                },
            },
        };

        // Primer envío por HTTP drenado en el hilo principal.
        var httpRes1 = await PostRespuestaDrenandoAsync(http, api, respuesta);
        var res1 = System.Text.Json.JsonSerializer.Deserialize<Resultado>(await httpRes1.Content.ReadAsStringAsync())!;
        Assert.Equal(Resultado.Aceptada, res1.Estado);

        var memoriaHttp1 = System.Text.Json.JsonSerializer.Deserialize<InstantaneaMemoria>(
            await GetStringDrenandoAsync(http, api, $"memoria/{perfil.Id}"))!;
        var recuerdo = Assert.Single(memoriaHttp1.Recuerdos);
        Assert.Equal("Aquella tarde frente al agua me dejó con ganas de volver temprano.", recuerdo.Texto);
        Assert.Equal(new[] { idEpisodio }, recuerdo.OrigenDiario);

        // Repetir el mismo envío es idempotente: sigue habiendo una sola copia en el estado y en la API.
        var httpRes2 = await PostRespuestaDrenandoAsync(http, api, respuesta);
        var res2 = System.Text.Json.JsonSerializer.Deserialize<Resultado>(await httpRes2.Content.ReadAsStringAsync())!;
        Assert.Equal(Resultado.Aceptada, res2.Estado);

        Assert.Single(estado.Recuerdos);
        var tareaConsultaMem = api.ObtenerMemoriaAsync(perfil.Id);
        while (!tareaConsultaMem.IsCompleted)
        {
            api.DrenarCola();
            await Task.Delay(2);
        }
        var consultaMem = await tareaConsultaMem;
        Assert.Equal(EstadoRecursoApi.Ok, consultaMem.Estado);
        Assert.Single(consultaMem.Valor!.Recuerdos);
    }

    [Fact]
    public async Task Respuesta_con_efectos_sin_aplicador_se_rechaza_y_rechazo_de_origen_no_deja_cambios_parciales()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);
        var epoca = ApiMotor.GenerarEpoca();

        // 1. ApiMotor sin aplicador registrado: una respuesta con memoria/efectos se rechaza con sin_aplicador.
        var apiSinAplicador = new ApiMotor("0.1.0");
        apiSinAplicador.IniciarEpoca(epoca, new[] { perfil.Id });
        apiSinAplicador.RegistrarProveedorEstado(id => partida.ObtenerSiExiste(id));

        var pulsoSinAplicador = apiSinAplicador.EmitirPulso(new Pulso
        {
            Personaje = perfil.Id,
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 2200 },
            Hito = new HitoPulso { Id = HitosRutina.Acostarse, Origen = HitosRutina.OrigenRutina, Forma = HitosRutina.FormaCadena },
            Escena = "Recoges el cuaderno.",
            EpisodiosDiario = new Dictionary<string, string> { ["ep-1"] = "Dibujaste junto al lago." },
        });

        var respConEfectos = new Respuesta
        {
            Pensamiento = "Guardo este recuerdo.",
            Epoca = epoca,
            BasadaEn = pulsoSinAplicador.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new() { Texto = "Fue una tarde tranquila.", OrigenDiario = new List<string> { "ep-1" } },
                },
            },
        };

        var tareaSinAplicador = apiSinAplicador.ProcesarRespuestaAsync(respConEfectos);
        apiSinAplicador.DrenarCola();
        var resSinAplicador = await tareaSinAplicador;

        Assert.Equal(Resultado.Rechazada, resSinAplicador.Estado);
        Assert.Equal("sin_aplicador", Assert.Single(resSinAplicador.Motivos!).Codigo);
        Assert.Empty(estado.Recuerdos);

        // 2. Host conectado con MotorHabitante, pero respuesta con origen ajeno junto a intención y actos válidos:
        //    debe rechazarse completamente sin dejar cambios parciales en intención, agenda, compromisos ni recuerdos.
        var apiHost = new ApiMotor("0.1.0");
        apiHost.IniciarEpoca(epoca, new[] { perfil.Id });

        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Vanilla,
            publicarPulso: p => apiHost.EmitirPulso(p))
        {
            Epoca = epoca,
        };
        var horario = new HorarioDia("Fri", new[]
        {
            new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
            new EntradaHorario(1600, "Saloon", 2, 20),
            new EntradaHorario(2340, "LeahHouse", 2, 4, "leah_sleep"),
        });
        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 4)
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 1700)
            .Construir(perfil.Lexico);

        CoordinadorApiAnfitrion.Conectar(
            apiHost,
            id => id == perfil.Id ? motor : null,
            id => partida.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs: 1_000, minutosJuego: 10);
        var pulsoAcostarse = motor.ProcesarFinDeDia(mundo.Observar(), ahoraMs: 2_000, horario: mundo.HorarioHoy)!;
        var episodiosAntes = estado.Diario.Count;

        var respuestaMixtaInvalida = new Respuesta
        {
            Pensamiento = "Intento cambiar la rutina y citar un episodio ajeno a la vez.",
            Epoca = epoca,
            BasadaEn = pulsoAcostarse.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "ir a la pasarela",
                Porque = "me apetece",
                Plan = new List<PasoPlan> { new() { Accion = "ir_a", Lugar = "pasarela_lago" } },
                CambiosRutina = new List<CambioRutina> { new() { Hito = "Fri:1600", Cambio = "saltar" } },
            },
            Actos = new List<Acto>
            {
                new()
                {
                    Tipo = TiposActo.Cita,
                    Lugar = "pasarela_lago",
                    Para = "farmer:1",
                    Cuando = new TiempoJuego { Dia = 69, Hora = 1800 },
                },
            },
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new() { Texto = "Origen ajeno.", OrigenDiario = new List<string> { "ep-ajeno-999" } },
                },
            },
        };

        var tareaInvalida = apiHost.ProcesarRespuestaAsync(respuestaMixtaInvalida);
        apiHost.DrenarCola();
        var resInvalida = await tareaInvalida;

        Assert.Equal(Resultado.Rechazada, resInvalida.Estado);
        Assert.Contains(resInvalida.Motivos!, m => m.Codigo == "episodio_desconocido");
        Assert.Empty(estado.Recuerdos);
        Assert.Null(estado.IntencionActiva);
        Assert.Empty(estado.Compromisos);
        Assert.Equal(EstadoHito.Pendiente, motor.AgendaActual!.Buscar("Fri:1600")!.Estado);
        Assert.Equal(episodiosAntes, estado.Diario.Count);
    }

    [Fact]
    public void Heartbeat_en_Pelican_evita_mente_caida_durante_esperar_largo_y_degrada_tras_silencio_real()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);
        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        var horario = new HorarioDia("Fri", new[]
        {
            new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
            new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
        });
        var mundo = Escenario.Pasarela()
            .EnModo(ModoReloj.Pelican)
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 600)
            .Construir(perfil.Lexico);

        long ahoraMs = 1_000;
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Pelican,
            publicarPulso: p => api.EmitirPulso(p),
            tiempoMaxRespuestaMs: 10_000)
        {
            Epoca = epoca,
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => ahoraMs);
        CoordinadorApiAnfitrion.Conectar(
            api,
            id => id == perfil.Id ? motor : null,
            id => partida.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        var pasoInicial = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs, minutosJuego: 0);
        var pulso = Assert.Single(pasoInicial.Pulsos);

        var respuesta = new Respuesta
        {
            Pensamiento = "Espero tranquilo medio rato.",
            Epoca = epoca,
            BasadaEn = pulso.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "descansar un rato",
                Porque = "hay calma",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 30 } },
            },
        };
        var resApi = api.ProcesarRespuestaEnHiloPrincipal(respuesta);
        Assert.Equal(Resultado.Aceptada, resApi.Estado);
        mundo.Cuerpo.Ejecutor.AvanzarTick(ahoraMs);
        Assert.True(mundo.Cuerpo.Ocupado);
        Assert.True(motor.IntencionActivaVigente?.EstaActiva);

        var msPorDiezMin = HoraJuego.MilisegundosPorDiezMinutos(ModoReloj.Pelican);
        var minutosPorPaso = 25_000.0 * 10.0 / msPorDiezMin;
        var obsTranquila = mundo.Observar() with { Entidades = [] };
        for (var i = 0; i < 40; i++) // 16,6 min reales con heartbeat cada 25 s (> 600 s), esperar(30) sigue activo.
        {
            ahoraMs += 25_000;
            motor.NotificarActividadMente(ahoraMs);
            mundo.Cuerpo.Ejecutor.AvanzarTick(ahoraMs);
            var step = motor.ProcesarPaso(obsTranquila, mundo.HorarioHoy, ahoraMs, minutosPorPaso);
            Assert.Empty(step.Pulsos);
        }

        Assert.False(motor.EstaMenteCaida);
        Assert.Equal(0, motor.TotalDegradaciones);
        Assert.Null(motor.UltimoMotivoDegradacion);

        // Si deja de llegar actividad real durante 10 minutos completos (600_000 ms), degrada a mente_caida.
        for (var i = 0; i < 24; i++)
        {
            ahoraMs += 25_000;
            mundo.Cuerpo.Ejecutor.AvanzarTick(ahoraMs);
            motor.ProcesarPaso(obsTranquila, mundo.HorarioHoy, ahoraMs, minutosPorPaso);
        }

        Assert.Equal("mente_caida", motor.UltimoMotivoDegradacion);
        Assert.Equal(1, motor.TotalDegradaciones);
    }

    [Fact]
    public async Task Rechazo_temprano_en_api_notifica_actividad_y_no_degrada_por_mente_caida()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);
        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        var horario = new HorarioDia("Fri", new[]
        {
            new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
            new EntradaHorario(1600, "Saloon", 2, 20),
            new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
        });
        var mundo = Escenario.Pasarela()
            .EnModo(ModoReloj.Pelican)
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 600)
            .Construir(perfil.Lexico);

        long ahoraMs = 1_000;
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Pelican,
            publicarPulso: p => api.EmitirPulso(p),
            tiempoMaxRespuestaMs: 10_000)
        {
            Epoca = epoca,
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => ahoraMs);
        CoordinadorApiAnfitrion.Conectar(
            api,
            id => id == perfil.Id ? motor : null,
            id => partida.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        var p1 = Assert.Single(motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs, 0).Pulsos);
        var ok1 = api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Pensamiento = "Espero unos minutos.",
            Epoca = epoca,
            BasadaEn = p1.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "tomar aire",
                Porque = "tranquilidad",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 30 } },
            },
        });
        Assert.Equal(Resultado.Aceptada, ok1.Estado);
        mundo.Cuerpo.Ejecutor.AvanzarTick(ahoraMs);

        // Emitimos un segundo pulso pendiente a las 09:00 mientras el cuerpo sigue reclamado.
        ahoraMs += 5_000;
        var obs900 = mundo.Observar() with { Hora = 900 };
        var p2 = Assert.Single(motor.ProcesarPaso(obs900, mundo.HorarioHoy, ahoraMs, 1).Pulsos);

        // Respuesta que falla antes del aplicador (ValidadorOrigenDiario cuando episodios_diario es null).
        var tareaRechazo = api.ProcesarRespuestaAsync(new Respuesta
        {
            Pensamiento = "Intento guardar reflexión en pulso sin diario.",
            Epoca = epoca,
            BasadaEn = p2.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new() { Texto = "Origen ajeno.", OrigenDiario = new List<string> { "ep-ajeno-999" } },
                },
            },
        });
        api.DrenarCola();
        var resRechazo = await tareaRechazo;
        Assert.Equal(Resultado.Rechazada, resRechazo.Estado);
        Assert.Contains(resRechazo.Motivos!, m => m.Codigo is "episodio_desconocido" or "origen_no_permitido");

        // Superar topeCaidaPulsoMs (60_000 ms en Pelican) no debe degradar porque la API notificó recepción.
        ahoraMs += 70_000;
        motor.ProcesarPaso(obs900, mundo.HorarioHoy, ahoraMs, 1);
        Assert.Equal(0, motor.TotalDegradaciones);
        Assert.Null(motor.UltimoMotivoDegradacion);
    }

    [Fact]
    public async Task Avance_de_since_en_long_poll_limpia_espera_de_pulso_pendiente()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);
        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        var horario = new HorarioDia("Fri", new[]
        {
            new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
            new EntradaHorario(1600, "Saloon", 2, 20),
            new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
        });
        var mundo = Escenario.Pasarela()
            .EnModo(ModoReloj.Pelican)
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: 600)
            .Construir(perfil.Lexico);

        long ahoraMs = 1_000;
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Pelican,
            publicarPulso: p => api.EmitirPulso(p),
            tiempoMaxRespuestaMs: 10_000)
        {
            Epoca = epoca,
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => ahoraMs);
        CoordinadorApiAnfitrion.Conectar(
            api,
            id => id == perfil.Id ? motor : null,
            id => partida.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        var p1 = Assert.Single(motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs, 0).Pulsos);
        api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Pensamiento = "Espero.",
            Epoca = epoca,
            BasadaEn = p1.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "esperar",
                Porque = "calma",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 30 } },
            },
        });
        mundo.Cuerpo.Ejecutor.AvanzarTick(ahoraMs);

        ahoraMs += 5_000;
        var obs900 = mundo.Observar() with { Hora = 900 };
        var p2 = Assert.Single(motor.ProcesarPaso(obs900, mundo.HorarioHoy, ahoraMs, 1).Pulsos);

        // El cliente hace long-poll avanzando since >= p2.Id aunque todavía no envíe POST /respuestas.
        await api.EsperarPulsosAsync(epoca, since: p2.Id, esperaMs: 0);
        motor.NotificarActividadMente(ahoraMs, api.UltimoSincePulsos);

        ahoraMs += 70_000;
        motor.ProcesarPaso(obs900, mundo.HorarioHoy, ahoraMs, 1);
        Assert.Equal(0, motor.TotalDegradaciones);
        Assert.Null(motor.UltimoMotivoDegradacion);
    }
}
