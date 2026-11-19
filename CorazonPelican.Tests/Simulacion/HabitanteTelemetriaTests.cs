using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Observabilidad;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Contrato;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Simulacion;

public class HabitanteTelemetriaTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string tempDir = Path.Combine(
        Path.GetTempPath(),
        "corazon-pelican-cp109-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(this.tempDir))
            {
                Directory.Delete(this.tempDir, recursive: true);
            }
        }
        catch
        {
        }
    }

    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static HorarioDia HorarioOtono() => new("fall", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "Town", 86, 66, "square_1_5_3"),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2200, "LeahHouse", 13, 10),
        new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
    });

    [Fact]
    public void Dia_simulado_con_mente_deshabilitada_genera_telemetria_diario_y_costes_sin_bloquear()
    {
        var config = new CorazonConfig
        {
            Personajes = new List<string> { "leah" },
            MenteHabilitada = false,
            Telemetria = true,
        };
        Assert.Empty(config.Validate());

        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var medidor = new MedidorCosteBucle();
        var telemetria = new TelemetriaJsonl(this.tempDir, "save_f1", config.Telemetria);

        var horario = HorarioOtono();
        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 5, deambula: true)
            .ConHorarios((_, _) => horario)
            .ALas(dia: 68, hora: HoraJuego.InicioDia)
            .Construir(perfil.Lexico);

        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            mundo.Reloj.Modo,
            telemetria,
            medidor,
            tamanoMaxMemoriaKB: config.TamanoMaxMemoriaKB,
            tiempoMaxRespuestaMs: config.TiempoMaxRespuestaMs)
        {
            Epoca = "epoca-f1-sim",
        };

        var minutosPorPaso = FakeWorld.PasoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(mundo.Reloj.Modo);
        var pasosTotales = 120 * 7 * 1000 / (int)FakeWorld.PasoMs; // 6:00 -> 2:00 del día siguiente
        var pulsosEmitidos = new List<Pulso>();
        var crono = Stopwatch.StartNew();

        for (var i = 0; i < pasosTotales; i++)
        {
            Observacion obs;
            using (medidor.Medir("observar"))
            {
                obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            }

            var res = motor.ProcesarPaso(obs, mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, minutosPorPaso);
            pulsosEmitidos.AddRange(res.Pulsos);
        }

        crono.Stop();
        Assert.True(crono.Elapsed < TimeSpan.FromSeconds(5), $"El día simulado tardó demasiado: {crono.Elapsed}");

        // Pulsos: incluye despertar, hitos/estímulos del día y acostarse.
        Assert.Contains(pulsosEmitidos, p => p.Motivo == Motivos.Hito && p.Hito?.Id == HitosRutina.Despertar);
        var acostarse = Assert.Single(pulsosEmitidos, p => p.Motivo == Motivos.Hito && p.Hito?.Id == HitosRutina.Acostarse);
        Assert.NotNull(acostarse.EpisodiosDiario);
        Assert.NotEmpty(acostarse.EpisodiosDiario!);

        // Todos los pulsos emitidos cumplen el contrato JSON Schema.
        foreach (var pulso in pulsosEmitidos)
        {
            var nodo = System.Text.Json.JsonSerializer.SerializeToNode(pulso, JsonOpciones);
            var eval = Esquemas.Evaluar("pulso", nodo);
            Assert.True(eval.IsValid, $"Pulso #{pulso.Id} ({pulso.Motivo}) inválido.");
        }

        // Diario poblado con ids únicos sin spam por tick.
        Assert.NotEmpty(estado.Diario);
        Assert.True(estado.Diario.Count <= 30, $"Diario inesperado con {estado.Diario.Count} entradas en un día.");
        Assert.Equal(estado.Diario.Count, estado.Diario.Select(e => e.Id).Distinct(StringComparer.Ordinal).Count());

        // Telemetría escrita en UTF-8 sin BOM en escenas.jsonl y pulsos.jsonl.
        var rutaEscenas = telemetria.RutaArchivo(TelemetriaJsonl.ArchivoEscenas);
        var rutaPulsos = telemetria.RutaArchivo(TelemetriaJsonl.ArchivoPulsos);
        Assert.True(File.Exists(rutaEscenas));
        Assert.True(File.Exists(rutaPulsos));

        var bytesEscenas = File.ReadAllBytes(rutaEscenas);
        Assert.False(bytesEscenas.Length >= 3 && bytesEscenas[0] == 0xEF && bytesEscenas[1] == 0xBB && bytesEscenas[2] == 0xBF);

        var lineasEscenas = File.ReadAllLines(rutaEscenas, Encoding.UTF8);
        var lineasPulsos = File.ReadAllLines(rutaPulsos, Encoding.UTF8);
        Assert.Equal(pulsosEmitidos.Count, lineasEscenas.Length);
        Assert.Equal(pulsosEmitidos.Count, lineasPulsos.Length);

        foreach (var linea in lineasEscenas)
        {
            var doc = JsonNode.Parse(linea)!.AsObject();
            Assert.Equal("leah", doc["personaje"]!.GetValue<string>());
            Assert.False(string.IsNullOrWhiteSpace(doc["escena"]!.GetValue<string>()));
        }

        foreach (var linea in lineasPulsos)
        {
            var eval = Esquemas.Evaluar("pulso", JsonNode.Parse(linea));
            Assert.True(eval.IsValid);
        }

        // Costes medidos por paso sin bloquear el tick.
        var resumen = medidor.Resumen();
        Assert.Contains(resumen, m => m.Paso == "observar" && m.Muestras == pasosTotales);
        Assert.Contains(resumen, m => m.Paso == "saliencia" && m.Muestras == pasosTotales && m.MedioMs < 5.0);
        Assert.Contains(resumen, m => m.Paso == "bandas" && m.Muestras == pasosTotales);
        Assert.Contains(resumen, m => m.Paso == "bufer_huella" && m.Muestras == pasosTotales);
        Assert.Contains(resumen, m => m.Paso == "disparadores" && m.Muestras == pasosTotales);
        Assert.Contains(resumen, m => m.Paso == "pulso_escena" && m.Muestras > 0);
    }

    [Fact]
    public void PrevisualizarEscena_no_consume_presupuesto_ni_reinicia_huella_integrador_ni_escribe_diario()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var telemetria = new TelemetriaJsonl(this.tempDir, "save_preview", habilitada: true);

        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 8)
            .ALas(dia: 68, hora: 1400)
            .Construir(perfil.Lexico);

        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo, telemetria)
        {
            Epoca = "epoca-preview",
        };

        // Acumulamos ~1 minuto de juego (4 pasos) sin disparar pulso para llenar huella e integrador.
        var minutosPorPaso = FakeWorld.PasoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(mundo.Reloj.Modo);
        for (var i = 0; i < 4; i++)
        {
            var obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            var paso = motor.ProcesarPaso(obs, null, mundo.Reloj.MilisegundosReales, minutosPorPaso);
            Assert.Empty(paso.Pulsos);
        }

        var integradorAntes = motor.Disparadores.ValorIntegrador;
        var minutosHuellaAntes = motor.Huella.MinutosDesdeUltimoPensamiento;
        var entidadesHuellaAntes = motor.Huella.Entidades.Select(e => (e.Ref, e.Atencion)).ToList();
        var gastadosAntes = motor.Estado.Contadores?.OrdinariosLibresGastados ?? 0;
        var diarioAntes = motor.Estado.Diario.Count;

        Assert.True(integradorAntes > 0);
        Assert.NotEmpty(entidadesHuellaAntes);

        // Simulamos cp_escena y GET /api/v1/escena/{personaje} varias veces.
        var obsActual = mundo.Observar();
        var preview1 = motor.PrevisualizarEscena(obsActual);
        var preview2 = motor.PrevisualizarEscena(obsActual);
        var dtoApi = motor.ConstruirEscenaActualApi(obsActual);

        Assert.False(string.IsNullOrWhiteSpace(preview1.Escena));
        Assert.Equal(preview1.TextoCompleto, preview2.TextoCompleto);
        Assert.True(dtoApi.ObservacionDisponible);
        Assert.Equal(preview1.Escena, dtoApi.Escena);
        Assert.Equal("epoca-preview", dtoApi.Epoca);

        // Ningún estado de atención, presupuesto o diario se vio alterado.
        Assert.Equal(integradorAntes, motor.Disparadores.ValorIntegrador);
        Assert.Equal(minutosHuellaAntes, motor.Huella.MinutosDesdeUltimoPensamiento);
        Assert.Equal(entidadesHuellaAntes, motor.Huella.Entidades.Select(e => (e.Ref, e.Atencion)).ToList());
        Assert.Equal(gastadosAntes, motor.Estado.Contadores?.OrdinariosLibresGastados ?? 0);
        Assert.Equal(diarioAntes, motor.Estado.Diario.Count);
        Assert.False(File.Exists(telemetria.RutaArchivo(TelemetriaJsonl.ArchivoPulsos)));
        Assert.False(File.Exists(telemetria.RutaArchivo(TelemetriaJsonl.ArchivoEscenas)));
    }

    [Fact]
    public async Task ForzarPulso_respeta_presupuesto_traza_en_jsonl_y_emite_en_ApiMotor()
    {
        var perfil = CargarPerfil();
        // Límite ordinario pequeño para probar el agotamiento: 4 totales - 2 cadenas = 2 ordinarios.
        perfil.Presupuesto.Vanilla = new PresupuestoModo
        {
            PulsosPorDiaJuego = 4,
            ReservadosCadenas = 2,
            ExtraCompromisos = 1,
        };

        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var telemetria = new TelemetriaJsonl(this.tempDir, "save_pulso", habilitada: true);

        var epoca = ApiMotor.GenerarEpoca();
        var api = new ApiMotor();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Vanilla,
            telemetria,
            publicarPulso: p => api.EmitirPulso(p))
        {
            Epoca = epoca,
        };

        api.RegistrarProveedorEscena(id => id == perfil.Id ? motor.ConstruirEscenaActualApi() : null);

        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 6)
            .ALas(dia: 68, hora: 1500)
            .Construir(perfil.Lexico);

        var obs = mundo.Observar();
        var r1 = motor.ForzarPulso(obs, 1_000, "manual");
        var r2 = motor.ForzarPulso(obs, 2_000, "manual");
        var r3 = motor.ForzarPulso(obs, 3_000, "manual"); // 3º ordinario supera 4 - 2 = 2.

        Assert.True(r1.Emitido);
        Assert.NotNull(r1.Pulso);
        Assert.True(r2.Emitido);
        Assert.NotNull(r2.Pulso);
        Assert.False(r3.Emitido);
        Assert.Null(r3.Pulso);
        Assert.False(string.IsNullOrWhiteSpace(r3.MotivoRechazo));

        Assert.Equal(2, motor.Estado.Contadores!.OrdinariosLibresGastados);
        Assert.Equal(2, File.ReadAllLines(telemetria.RutaArchivo(TelemetriaJsonl.ArchivoPulsos)).Length);
        Assert.Equal(2, File.ReadAllLines(telemetria.RutaArchivo(TelemetriaJsonl.ArchivoEscenas)).Length);

        var consultaPulsos = await api.EsperarPulsosAsync(epoca, since: 0, esperaMs: 0);
        Assert.False(consultaPulsos.ConflictoEpoca);
        Assert.Equal(2, consultaPulsos.Pulsos.Count);
        Assert.Equal(new[] { r1.Pulso!.Id, r2.Pulso!.Id }, consultaPulsos.Pulsos.Select(p => p.Id));

        var consultaEscena = await api.ObtenerEscenaAsync(perfil.Id);
        Assert.Equal(EstadoRecursoApi.Ok, consultaEscena.Estado);
        Assert.True(consultaEscena.Valor!.ObservacionDisponible);

        // Las reservas de cadenas (despertar/acostarse) siguen disponibles aunque se agoten los ordinarios.
        var acostarse = motor.ProcesarFinDeDia(obs, 4_000);
        Assert.NotNull(acostarse);
        Assert.Equal(HitosRutina.Acostarse, acostarse!.Hito?.Id);
    }

    [Fact]
    public void Con_telemetria_deshabilitada_no_se_crea_carpeta_ni_ficheros_jsonl()
    {
        var baseInexistente = Path.Combine(this.tempDir, "no_debe_crearse");
        var telemetria = new TelemetriaJsonl(baseInexistente, "save_off", habilitada: false);

        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var medidor = new MedidorCosteBucle();

        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 6)
            .ALas(dia: 68, hora: HoraJuego.InicioDia)
            .Construir(perfil.Lexico);

        var motor = new MotorHabitante(perfil, plantillas, estado, ModoReloj.Vanilla, telemetria, medidor);
        var obs = mundo.Observar();

        motor.ProcesarPaso(obs, null, ahoraMs: 0, minutosJuego: 0);
        var forzado = motor.ForzarPulso(obs, ahoraMs: 1_000);
        Assert.True(forzado.Emitido);

        telemetria.RegistrarRespuesta(new Respuesta { Pensamiento = "prueba", Epoca = "ep", BasadaEn = 1 }, "leah");
        telemetria.RegistrarRechazo("leah", 1, Resultado.Rechazar(new MotivoRechazo("test", "campo", "prosa")), "ep");
        telemetria.RegistrarActo("leah", new Acto { Tipo = "rechaza", Motivo = "ahora no" }, 68, 1200, aceptado: false);
        telemetria.RegistrarCostes(medidor.Resumen(), 68, 1200, "ep");

        Assert.False(Directory.Exists(baseInexistente));
        Assert.False(Directory.Exists(telemetria.DirectorioPartida));
    }

    [Fact]
    public void Telemetria_registra_todos_los_flujos_jsonl_sin_afectar_memoria()
    {
        var telemetria = new TelemetriaJsonl(this.tempDir, "save_todos", habilitada: true);
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var mundo = Escenario.Pasarela().ConJugador("Ana", 6, 8).Construir(perfil.Lexico);
        var escena = GeneradorEscena.Generar(mundo.Observar(), perfil, plantillas);
        var pulso = escena.CrearPulsoBase(1, "ep-1", perfil.Id, Motivos.Deriva);

        telemetria.RegistrarEscena(perfil.Id, escena, "ep-1", "Mountain", "pulso", pulso.Id);
        telemetria.RegistrarPulso(pulso);
        telemetria.RegistrarRespuesta(new Respuesta { Pensamiento = "luz en el lago", Epoca = "ep-1", BasadaEn = 1 }, perfil.Id);
        telemetria.RegistrarRechazo(perfil.Id, 1, Resultado.Rechazar(new MotivoRechazo("pulso_obsoleto", "basada_en", "tarde")), "ep-1");
        telemetria.RegistrarActo(perfil.Id, new Acto { Tipo = "cita", Para = "farmer:1", Lugar = "pasarela_lago" }, 68, 1700, aceptado: true);

        foreach (var archivo in new[]
        {
            TelemetriaJsonl.ArchivoEscenas,
            TelemetriaJsonl.ArchivoPulsos,
            TelemetriaJsonl.ArchivoRespuestas,
            TelemetriaJsonl.ArchivoRechazos,
            TelemetriaJsonl.ArchivoActos,
        })
        {
            var ruta = telemetria.RutaArchivo(archivo);
            Assert.True(File.Exists(ruta), $"Falta {archivo}");
            var lineas = File.ReadAllLines(ruta, Encoding.UTF8);
            Assert.Single(lineas);
            Assert.NotNull(JsonNode.Parse(lineas[0]));
        }
    }
}
