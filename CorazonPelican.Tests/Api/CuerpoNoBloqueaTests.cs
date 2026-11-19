using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Tests.Simulacion;

namespace CorazonPelican.Tests.Api;

/// <summary>Guarda de R-05: el cuerpo nunca bloquea esperando a la mente (ADR-0001, CP-203).</summary>
public class CuerpoNoBloqueaTests
{
    private static int ObtenerPuertoLibre()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    [Fact]
    public async Task Mente_http_lenta_de_30s_no_bloquea_el_tick_del_motor_ni_el_cuerpo()
    {
        var epoca = ApiMotor.GenerarEpoca();
        var motor = new ApiMotor("0.1.0");
        motor.IniciarEpoca(epoca, new[] { "habitante" });

        var aplicaciones = 0;
        var hiloAplicacion = 0;
        motor.RegistrarAplicador("habitante", (_, _) =>
        {
            aplicaciones++;
            hiloAplicacion = Environment.CurrentManagedThreadId;
            return new Resultado { Estado = Resultado.Aceptada };
        });

        var puerto = ObtenerPuertoLibre();
        using var servidor = new ServidorApiMotor(motor, puerto);
        servidor.Iniciar();

        var mundo = Escenario.Pasarela()
            .ConMente(FakeMind.Lenta(TimeSpan.FromSeconds(30)))
            .Construir();

        var pulso = new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1700 },
            Escena = "Estás en la pasarela mirando el lago.",
        };

        // Calentamiento JIT previo al muestreo (cola vacía, drenado, FakeBody y FakeMind).
        var motorCalentamiento = new ApiMotor("0.1.0");
        motorCalentamiento.IniciarEpoca("calentamiento", new[] { "habitante" });
        var pulsoCalentamiento = motorCalentamiento.EmitirPulso(new Pulso
        {
            Personaje = "habitante",
            Motivo = Motivos.Deriva,
            TiempoJuego = new TiempoJuego { Dia = 68, Hora = 1650 },
            Escena = "Calentamiento.",
        });
        _ = motorCalentamiento.ProcesarRespuestaEnHiloPrincipal(
            new Respuesta { Pensamiento = "ok", Epoca = "calentamiento", BasadaEn = pulsoCalentamiento.Id },
            "huella-calentamiento");
        var mundoCalentamiento = Escenario.Pasarela().ConMente(FakeMind.Lenta(TimeSpan.FromSeconds(1))).Construir();
        mundoCalentamiento.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" },
        });
        mundoCalentamiento.EmitirPulso(pulsoCalentamiento);
        mundoCalentamiento.Avanzar(TimeSpan.FromSeconds(5));
        motor.DrenarCola();

        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{puerto}/api/v1/") };
        using var ctsLenta = new CancellationTokenSource();

        // Petición real long-poll de 30 s abierta en paralelo representando a una mente lenta.
        var longPollLento = http.GetAsync($"pulsos?epoca={epoca}&since=999&espera_ms=30000", ctsLenta.Token);

        // El cuerpo inicia un plan en FakeWorld mientras la mente tarda 30 s en responder.
        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" },
        });

        var emitido = motor.EmitirPulso(pulso);
        mundo.EmitirPulso(emitido);

        // Simulamos 120 ticks (~30 s virtuales) en el hilo principal con peticiones HTTP lentas activas.
        var maxTickMs = 0.0;
        for (var i = 0; i < 120; i++)
        {
            var inicio = Stopwatch.GetTimestamp();
            motor.DrenarCola();
            mundo.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
            var transcurridoMs = (Stopwatch.GetTimestamp() - inicio) * 1000.0 / Stopwatch.Frequency;
            if (transcurridoMs > maxTickMs)
            {
                maxTickMs = transcurridoMs;
            }
        }

        Assert.True(maxTickMs < 2.0, $"El tick más lento tardó {maxTickMs:0.###} ms (límite R-05: 2 ms).");
        Assert.Contains(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado);
        Assert.True(mundo.Cuerpo.Sentada);
        Assert.Single(mundo.RespuestasEntregadas);
        Assert.False(longPollLento.IsCompleted, "El long-poll de 30 s debe seguir esperando sin bloquear al motor.");

        // Cuando la mente lenta finalmente envía POST /respuestas, se encola y se drena en < 2 ms en el hilo principal.
        var jsonRespuesta = JsonSerializer.Serialize(new Respuesta
        {
            Pensamiento = "He pensado con calma durante medio minuto.",
            Epoca = epoca,
            BasadaEn = emitido.Id,
        });
        var postPendiente = http.PostAsync(
            "respuestas",
            new StringContent(jsonRespuesta, Encoding.UTF8, "application/json"));

        // Esperamos brevemente a que la petición HTTP entre en MainThreadActionQueue (fuera de la medición del tick).
        var limiteEsperaCola = Stopwatch.StartNew();
        while (motor.Cola.Pendientes == 0 && limiteEsperaCola.Elapsed < TimeSpan.FromSeconds(2))
        {
            await Task.Delay(2);
        }

        var hiloQueDrena = Environment.CurrentManagedThreadId;
        var inicioDrenado = Stopwatch.GetTimestamp();
        var drenadas = motor.DrenarCola();
        var drenadoMs = (Stopwatch.GetTimestamp() - inicioDrenado) * 1000.0 / Stopwatch.Frequency;

        var httpResultado = await postPendiente;
        var cuerpoResultado = JsonSerializer.Deserialize<Resultado>(await httpResultado.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, httpResultado.StatusCode);
        Assert.NotNull(cuerpoResultado);
        Assert.Equal(Resultado.Aceptada, cuerpoResultado!.Estado);
        Assert.Equal(1, drenadas);
        Assert.Equal(1, aplicaciones);
        Assert.Equal(hiloQueDrena, hiloAplicacion);
        Assert.Equal(hiloQueDrena, motor.Cola.UltimoHiloDrenado);
        Assert.True(drenadoMs < 2.0, $"El drenado de la respuesta tardó {drenadoMs:0.###} ms (límite R-05: 2 ms).");

        ctsLenta.Cancel();
        servidor.Detener();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => longPollLento);
    }
}
