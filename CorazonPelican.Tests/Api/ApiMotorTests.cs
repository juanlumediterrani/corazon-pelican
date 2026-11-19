using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Tests.Contrato;

namespace CorazonPelican.Tests.Api;

public class ApiMotorTests
{
    private static int ObtenerPuertoLibre()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<HttpResponseMessage> PostRespuestaDrenandoAsync(
        HttpClient http,
        ApiMotor motor,
        Respuesta respuesta)
    {
        var json = JsonSerializer.Serialize(respuesta);
        var contenido = new StringContent(json, Encoding.UTF8, "application/json");
        var tareaHttp = http.PostAsync("respuestas", contenido);

        var crono = Stopwatch.StartNew();
        while (!tareaHttp.IsCompleted && crono.Elapsed < TimeSpan.FromSeconds(5))
        {
            motor.DrenarCola();
            await Task.Delay(2);
        }

        motor.DrenarCola();
        return await tareaHttp;
    }

    private static Pulso CrearPulsoConEpisodios(string personaje = "habitante") => new()
    {
        Personaje = personaje,
        Motivo = Motivos.Hito,
        TiempoJuego = new TiempoJuego { Dia = 68, Hora = 2200 },
        Hito = new HitoPulso { Id = "acostarse", Origen = "rutina", Forma = "cadena" },
        Escena = "Recoges tus cosas antes de dormir.",
        EpisodiosDiario = new Dictionary<string, string>
        {
            ["ep-1"] = "Dibujaste junto al lago por la tarde.",
            ["ep-2"] = "Saludaste a la granjera al pasar.",
        },
    };

    [Fact]
    public async Task Post_respuestas_con_reflexion_invalida_o_ajena_se_rechaza_antes_de_aplicar_cambios()
    {
        var epoca = ApiMotor.GenerarEpoca();
        var motor = new ApiMotor();
        motor.IniciarEpoca(epoca, new[] { "habitante" });

        var aplicaciones = 0;
        motor.RegistrarAplicador("habitante", (_, _) =>
        {
            aplicaciones++;
            return new Resultado { Estado = Resultado.Aceptada };
        });

        var pulso = motor.EmitirPulso(CrearPulsoConEpisodios());

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        var respuestaAjena = new Respuesta
        {
            Pensamiento = "Ordeno mis recuerdos del día.",
            Epoca = epoca,
            BasadaEn = pulso.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Recuerdo inventado.",
                        OrigenDiario = new List<string> { "ep-1", "ep-de-otro-personaje" },
                    },
                },
            },
        };

        var httpRes = await PostRespuestaDrenandoAsync(http, motor, respuestaAjena);
        var json = await httpRes.Content.ReadAsStringAsync();
        var resultado = JsonSerializer.Deserialize<Resultado>(json);

        Assert.Equal(HttpStatusCode.OK, httpRes.StatusCode);
        Assert.NotNull(resultado);
        Assert.Equal(Resultado.Rechazada, resultado!.Estado);
        Assert.NotNull(resultado.Motivos);
        Assert.Contains(resultado.Motivos!, m => m.Codigo == "episodio_desconocido");
        Assert.True(Esquemas.EsValido("resultado", JsonNode.Parse(json)));
        Assert.Equal(0, aplicaciones);

        // Con orígenes válidos del propio pulso sí invoca el aplicador.
        var respuestaValida = new Respuesta
        {
            Pensamiento = "Ahora sí cito mis propios episodios.",
            Epoca = epoca,
            BasadaEn = pulso.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Buena tarde junto al lago.",
                        OrigenDiario = new List<string> { "ep-1", "ep-2" },
                    },
                },
            },
        };

        var httpValida = await PostRespuestaDrenandoAsync(http, motor, respuestaValida);
        var resultadoValido = JsonSerializer.Deserialize<Resultado>(await httpValida.Content.ReadAsStringAsync());

        Assert.Equal(Resultado.Aceptada, resultadoValido!.Estado);
        Assert.Equal(1, aplicaciones);
    }

    [Fact]
    public async Task Repetir_post_respuestas_devuelve_el_mismo_resultado_sin_reaplicar()
    {
        var epoca = ApiMotor.GenerarEpoca();
        var motor = new ApiMotor();
        motor.IniciarEpoca(epoca, new[] { "habitante" });

        var aplicaciones = 0;
        motor.RegistrarAplicador("habitante", (_, _) =>
        {
            aplicaciones++;
            return new Resultado { Estado = Resultado.Aceptada };
        });

        var pulso = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1710 },
            Escena = "Miras el agua.",
        });

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        var respuesta = new Respuesta
        {
            Pensamiento = "El agua está tranquila.",
            Epoca = epoca,
            BasadaEn = pulso.Id,
            Frase = "Qué calma.",
        };

        var primera = await PostRespuestaDrenandoAsync(http, motor, respuesta);
        var segunda = await PostRespuestaDrenandoAsync(http, motor, respuesta);

        var json1 = await primera.Content.ReadAsStringAsync();
        var json2 = await segunda.Content.ReadAsStringAsync();
        var r1 = JsonSerializer.Deserialize<Resultado>(json1);
        var r2 = JsonSerializer.Deserialize<Resultado>(json2);

        Assert.Equal(Resultado.Aceptada, r1!.Estado);
        Assert.Equal(Resultado.Aceptada, r2!.Estado);
        Assert.Equal(json1, json2);
        Assert.Equal(1, aplicaciones);
    }

    [Fact]
    public async Task Cambio_de_epoca_no_colisiona_con_mismo_id_de_pulso_y_get_pulsos_con_epoca_vieja_da_409()
    {
        var epoca1 = ApiMotor.GenerarEpoca();
        var epoca2 = ApiMotor.GenerarEpoca();
        Assert.NotEqual(epoca1, epoca2);

        var motor = new ApiMotor();
        var aplicaciones = 0;
        motor.RegistrarAplicador("habitante", (_, _) =>
        {
            aplicaciones++;
            return new Resultado { Estado = Resultado.Aceptada };
        });

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        // Época 1: pulso Id = 1 aceptado.
        motor.IniciarEpoca(epoca1, new[] { "habitante" });
        var pulsoEp1 = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1200 },
            Escena = "Mediodía en la cabaña.",
        });
        Assert.Equal(1, pulsoEp1.Id);

        var resEp1 = await PostRespuestaDrenandoAsync(http, motor, new Respuesta
        {
            Pensamiento = "Época uno.",
            Epoca = epoca1,
            BasadaEn = pulsoEp1.Id,
        });
        Assert.Equal(Resultado.Aceptada, JsonSerializer.Deserialize<Resultado>(await resEp1.Content.ReadAsStringAsync())!.Estado);
        Assert.Equal(1, aplicaciones);

        // Simula recarga (SaveLoaded): nueva época, los id de pulso vuelven a empezar en 1.
        motor.IniciarEpoca(epoca2, new[] { "habitante" });
        var pulsoEp2 = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1200 },
            Escena = "Mediodía tras recargar.",
        });
        Assert.Equal(1, pulsoEp2.Id);

        // GET pulsos con la época anterior debe responder 409 con la época vigente.
        var getVieja = await http.GetAsync($"pulsos?epoca={epoca1}&since=0&espera_ms=0");
        Assert.Equal(HttpStatusCode.Conflict, getVieja.StatusCode);
        var errorVieja = JsonSerializer.Deserialize<ErrorApi>(await getVieja.Content.ReadAsStringAsync());
        Assert.Equal(epoca2, errorVieja!.Epoca);

        // POST con época anterior se rechaza como epoca_obsoleta.
        var postVieja = await PostRespuestaDrenandoAsync(http, motor, new Respuesta
        {
            Pensamiento = "Tardía de la época 1.",
            Epoca = epoca1,
            BasadaEn = 1,
        });
        var resVieja = JsonSerializer.Deserialize<Resultado>(await postVieja.Content.ReadAsStringAsync());
        Assert.Equal(Resultado.Rechazada, resVieja!.Estado);
        Assert.Equal(ValidadorEpoca.CodigoEpocaObsoleta, resVieja.Motivos!.Single().Codigo);

        // POST con época 2 y mismo Id = 1 NO choca con el de época 1 y sí se aplica.
        var resEp2 = await PostRespuestaDrenandoAsync(http, motor, new Respuesta
        {
            Pensamiento = "Época dos.",
            Epoca = epoca2,
            BasadaEn = 1,
        });
        Assert.Equal(Resultado.Aceptada, JsonSerializer.Deserialize<Resultado>(await resEp2.Content.ReadAsStringAsync())!.Estado);
        Assert.Equal(2, aplicaciones);
    }

    [Fact]
    public async Task Long_poll_despierta_al_emitir_pulso_y_corta_con_409_si_cambia_la_epoca()
    {
        var epoca1 = ApiMotor.GenerarEpoca();
        var epoca2 = ApiMotor.GenerarEpoca();
        var motor = new ApiMotor();
        motor.IniciarEpoca(epoca1, new[] { "habitante" });

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        // 1) Long-poll esperando un pulso nuevo y despertado inmediatamente al emitir.
        var crono = Stopwatch.StartNew();
        var tareaPoll = http.GetAsync($"pulsos?epoca={epoca1}&since=0&espera_ms=5000");
        await Task.Delay(40);
        var pulso = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Pico,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1500 },
            Escena = "Brilla el lago.",
        });

        var resPoll = await tareaPoll;
        crono.Stop();
        Assert.Equal(HttpStatusCode.OK, resPoll.StatusCode);
        Assert.True(crono.Elapsed < TimeSpan.FromSeconds(2), $"El long-poll tardó demasiado en despertar: {crono.Elapsed}");
        var payload = JsonSerializer.Deserialize<RespuestaPulsos>(await resPoll.Content.ReadAsStringAsync());
        Assert.Equal(epoca1, payload!.Epoca);
        Assert.Single(payload.Pulsos);
        Assert.Equal(pulso.Id, payload.Pulsos[0].Id);

        // 2) Long-poll esperando since=1 que recibe 409 inmediatamente cuando cambia la época.
        var cronoCambio = Stopwatch.StartNew();
        var tareaPollCambio = http.GetAsync($"pulsos?epoca={epoca1}&since={pulso.Id}&espera_ms=5000");
        await Task.Delay(40);
        motor.IniciarEpoca(epoca2, new[] { "habitante" });

        var resCambio = await tareaPollCambio;
        cronoCambio.Stop();
        Assert.Equal(HttpStatusCode.Conflict, resCambio.StatusCode);
        Assert.True(cronoCambio.Elapsed < TimeSpan.FromSeconds(2));
        var err = JsonSerializer.Deserialize<ErrorApi>(await resCambio.Content.ReadAsStringAsync());
        Assert.Equal(epoca2, err!.Epoca);
    }

    [Fact]
    public async Task Respuesta_obsoleta_se_descarta_salvo_interlocucion()
    {
        var epoca = ApiMotor.GenerarEpoca();
        var motor = new ApiMotor();
        motor.IniciarEpoca(epoca, new[] { "habitante" });

        var aplicadas = new List<long>();
        motor.RegistrarAplicador("habitante", (p, _) =>
        {
            aplicadas.Add(p.Id);
            return new Resultado { Estado = Resultado.Aceptada };
        });

        var p1Deriva = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1600 },
            Escena = "Brisa.",
        });
        var p2Hito = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1700 },
            Escena = "Cambio de tramo.",
        });

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        // p1 (deriva) superado por p2 (hito, prioridad >= deriva) -> rechazada como pulso_obsoleto.
        var resObsoleta = await PostRespuestaDrenandoAsync(http, motor, new Respuesta
        {
            Pensamiento = "Tarde para p1.",
            Epoca = epoca,
            BasadaEn = p1Deriva.Id,
        });
        var dtoObsoleta = JsonSerializer.Deserialize<Resultado>(await resObsoleta.Content.ReadAsStringAsync());
        Assert.Equal(Resultado.Rechazada, dtoObsoleta!.Estado);
        Assert.Equal("pulso_obsoleto", dtoObsoleta.Motivos!.Single().Codigo);
        Assert.Empty(aplicadas);

        // Interlocución seguida de hito: interlocución nunca se descarta en silencio (ADS §4.1).
        var p3Interlocucion = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Interlocucion,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1710 },
            Escena = "Te hablan.",
            Interlocutor = new Interlocutor { Ref = "farmer:1", Texto = "Hola" },
            CaducaEnMs = 8000,
        });
        _ = motor.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1800 },
            Escena = "Otro hito.",
        });

        var resInter = await PostRespuestaDrenandoAsync(http, motor, new Respuesta
        {
            Pensamiento = "Respondo a la conversación.",
            Epoca = epoca,
            BasadaEn = p3Interlocucion.Id,
            Frase = "Hola.",
        });
        var dtoInter = JsonSerializer.Deserialize<Resultado>(await resInter.Content.ReadAsStringAsync());
        Assert.Equal(Resultado.Aceptada, dtoInter!.Estado);
        Assert.Equal(new[] { p3Interlocucion.Id }, aplicadas);
        Assert.NotEqual(0, p2Hito.Id);
    }

    [Fact]
    public async Task Endpoints_salud_eventos_memoria_y_escena_responden_segun_contrato()
    {
        var motor = new ApiMotor("0.1.0");
        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        // Sin partida cargada: salud indica mundo_cargado=false y omite epoca.
        var saludSinMundoJson = await http.GetStringAsync("salud");
        Assert.True(Esquemas.EsValido("salud", JsonNode.Parse(saludSinMundoJson)));
        var saludSinMundo = JsonSerializer.Deserialize<Salud>(saludSinMundoJson)!;
        Assert.False(saludSinMundo.MundoCargado);
        Assert.Null(saludSinMundo.Epoca);

        // Con partida cargada y proveedores conectados.
        var epoca = ApiMotor.GenerarEpoca();
        motor.IniciarEpoca(epoca, new[] { "habitante" });
        motor.RegistrarProveedorMemoria(p => p == "habitante"
            ? new InstantaneaMemoria
            {
                Epoca = epoca,
                Personaje = p,
                Recuerdos = new() { new RecuerdoMemoria { Id = "rec-1", Texto = "Tarde en el muelle", Importancia = 0.7 } },
            }
            : null);
        motor.RegistrarProveedorEscena(p => p == "habitante"
            ? new EscenaActual
            {
                Epoca = epoca,
                Personaje = p,
                ObservacionDisponible = true,
                Ubicacion = "Mountain",
                TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1730 },
                Escena = "La luz cae sobre el agua.",
            }
            : null);

        var saludConMundoJson = await http.GetStringAsync("salud");
        Assert.True(Esquemas.EsValido("salud", JsonNode.Parse(saludConMundoJson)));
        var saludConMundo = JsonSerializer.Deserialize<Salud>(saludConMundoJson)!;
        Assert.True(saludConMundo.MundoCargado);
        Assert.Equal(epoca, saludConMundo.Epoca);
        Assert.Equal(new[] { "habitante" }, saludConMundo.Personajes);

        var memoria = JsonSerializer.Deserialize<InstantaneaMemoria>(await http.GetStringAsync("memoria/habitante"))!;
        Assert.Equal("habitante", memoria.Personaje);
        Assert.Single(memoria.Recuerdos);

        var escena = JsonSerializer.Deserialize<EscenaActual>(await http.GetStringAsync("escena/habitante"))!;
        Assert.True(escena.ObservacionDisponible);
        Assert.Equal("Mountain", escena.Ubicacion);

        var escenaDesconocida = await http.GetAsync("escena/nadie");
        Assert.Equal(HttpStatusCode.NotFound, escenaDesconocida.StatusCode);

        var ev1 = motor.RegistrarEvento("acto_aplicado", "habitante", "Promesa guardada");
        var ev2 = motor.RegistrarEvento("obra_creada", "habitante", "Boceto iniciado");
        var eventos = JsonSerializer.Deserialize<RespuestaEventos>(await http.GetStringAsync($"eventos?since={ev1.Id}"))!;
        Assert.Single(eventos.Eventos);
        Assert.Equal(ev2.Id, eventos.Eventos[0].Id);
        Assert.Equal("obra_creada", eventos.Eventos[0].Tipo);
    }

    [Theory]
    [InlineData("0.0.0.0")]
    [InlineData("*")]
    [InlineData("+")]
    [InlineData("::")]
    [InlineData("192.168.1.50")]
    [InlineData("10.0.0.1")]
    [InlineData("externo.example")]
    [InlineData("localhost")]
    public void Configuracion_del_listener_rechaza_direcciones_o_hosts_no_loopback(string hostProhibido)
    {
        var motor = new ApiMotor();

        Assert.Throws<ArgumentException>(() => ServidorApiMotor.ConstruirPrefijoLoopback(25560, hostProhibido));
        Assert.Throws<ArgumentException>(() => new ServidorApiMotor(motor, 25560, host: hostProhibido));
    }

    [Theory]
    [InlineData("http://0.0.0.0:25560/api/v1/")]
    [InlineData("http://*:25560/api/v1/")]
    [InlineData("http://+:25560/api/v1/")]
    [InlineData("http://192.168.1.10:25560/api/v1/")]
    [InlineData("https://127.0.0.1:25560/api/v1/")]
    public void Validador_de_prefijo_rechaza_prefijos_inseguros(string prefijo)
    {
        Assert.Throws<ArgumentException>(() => ServidorApiMotor.ValidarPrefijoLoopback(prefijo));
    }

    [Fact]
    public void Validacion_de_ip_y_cabeceras_rechaza_origenes_e_ips_no_loopback()
    {
        Assert.True(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.Loopback, "127.0.0.1:25560", null));
        Assert.True(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.IPv6Loopback, "[::1]:25560", "http://127.0.0.1:3000"));
        Assert.True(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.Loopback.MapToIPv6(), "127.0.0.1:25560", null));

        Assert.False(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.Parse("192.168.1.20"), "127.0.0.1:25560", null));
        Assert.False(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.Parse("203.0.113.5"), "127.0.0.1:25560", null));
        Assert.False(ServidorApiMotor.EsPeticionLoopbackValida(null, "127.0.0.1:25560", null));
        Assert.False(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.Loopback, "atacante.example:25560", null));
        Assert.False(ServidorApiMotor.EsPeticionLoopbackValida(IPAddress.Loopback, "127.0.0.1:25560", "https://atacante.example"));
    }

    [Fact]
    public async Task Token_bearer_se_valida_cuando_esta_configurado_y_origin_externo_recibe_403()
    {
        var epoca = ApiMotor.GenerarEpoca();
        var motor = new ApiMotor();
        motor.IniciarEpoca(epoca, new[] { "habitante" });

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto, token: "secreto-cp-203");
        servidor.Iniciar();
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };

        // Sin token -> 401.
        var sinToken = await http.GetAsync("salud");
        Assert.Equal(HttpStatusCode.Unauthorized, sinToken.StatusCode);

        // Token incorrecto -> 401.
        using (var reqMala = new HttpRequestMessage(HttpMethod.Get, "salud"))
        {
            reqMala.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "incorrecto");
            var resMala = await http.SendAsync(reqMala);
            Assert.Equal(HttpStatusCode.Unauthorized, resMala.StatusCode);
        }

        // Token válido + Origin externo -> 403.
        using (var reqOrigenExterno = new HttpRequestMessage(HttpMethod.Get, "salud"))
        {
            reqOrigenExterno.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secreto-cp-203");
            reqOrigenExterno.Headers.Add("Origin", "https://externo.example");
            var resOrigen = await http.SendAsync(reqOrigenExterno);
            Assert.Equal(HttpStatusCode.Forbidden, resOrigen.StatusCode);
        }

        // Token válido en loopback -> 200.
        using var reqValida = new HttpRequestMessage(HttpMethod.Get, "salud");
        reqValida.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "secreto-cp-203");
        var resValida = await http.SendAsync(reqValida);
        Assert.Equal(HttpStatusCode.OK, resValida.StatusCode);
    }

    [Fact]
    public async Task MainThreadActionQueue_abandona_acciones_caducadas_sin_ejecutarlas_al_drenar()
    {
        var cola = new MainThreadActionQueue();
        var ejecutada = false;

        var tareaCaducada = cola.EncolarAsync(() =>
        {
            ejecutada = true;
            return 42;
        }, TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAsync<TimeoutException>(() => tareaCaducada);

        var drenadas = cola.Drenar();
        Assert.Equal(0, drenadas);
        Assert.False(ejecutada);
        Assert.Equal(1, cola.TotalAbandonadas);
        Assert.Equal(0, cola.TotalEjecutadas);
    }
}
