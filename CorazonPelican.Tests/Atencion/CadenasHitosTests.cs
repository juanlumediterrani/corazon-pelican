using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Atencion;

/// <summary>
/// Pruebas del lado motor/anfitrión para las cadenas de <c>despertar</c> y <c>acostarse</c> (CP-209, CP-905).
/// </summary>
public class CadenasHitosTests
{
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

    private static HorarioDia CrearHorarioDia(string clave, params (int Hora, string Ubicacion, int X, int Y)[] entradas)
        => new(clave, entradas.Select(e => new EntradaHorario(e.Hora, e.Ubicacion, e.X, e.Y)).ToList());

    [Fact]
    public void Reflexion_tardia_de_acostarse_tras_despertar_en_misma_epoca_se_aplica_una_vez_y_otra_epoca_da_epoca_obsoleta()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var partida = new EstadoPartida();
        var estado = partida.ObtenerOIniciar(perfil.Id);
        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        MotorHabitante? motor = null;
        motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            ModoReloj.Vanilla,
            publicarPulso: p => api.EmitirPulso(p))
        {
            Epoca = epoca,
        };

        CoordinadorApiAnfitrion.Conectar(
            api,
            id => string.Equals(id, perfil.Id, StringComparison.Ordinal) ? motor : null,
            id => partida.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        // Día 68: tarde tranquila y cadena de acostarse.
        var obsTarde = new Observacion(
            68,
            1740,
            "Forest",
            40,
            30,
            0.9,
            "soleado",
            new[] { new EntidadObservada("agua:Forest:rio", CategoriaEntidad.Terreno, "el río", "Forest", 40, 31, new[] { "agua" }) });
        motor.ProcesarPaso(obsTarde, CrearHorarioDia("spring_68", (900, "Forest", 40, 30)), ahoraMs: 1_000, minutosJuego: 20);

        var pulsoAcostarse = motor.ProcesarFinDeDia(obsTarde, ahoraMs: 2_000);
        Assert.NotNull(pulsoAcostarse);
        Assert.Equal(HitosRutina.Acostarse, pulsoAcostarse!.Hito?.Id);
        Assert.NotNull(pulsoAcostarse.EpisodiosDiario);
        var idEpisodio = pulsoAcostarse.EpisodiosDiario!.Keys.First();

        // Avanzamos al amanecer del día 69: se emite despertar ANTES de que llegue la respuesta de acostarse.
        var obsAmanecer = new Observacion(69, 600, "LeahHouse", 5, 5, 1.0, "soleado", Array.Empty<EntidadObservada>());
        var pasoAmanecer = motor.ProcesarPaso(
            obsAmanecer,
            CrearHorarioDia("spring_69", (900, "Forest", 40, 30), (1600, "Saloon", 10, 20)),
            ahoraMs: 3_000,
            minutosJuego: 0);
        var pulsoDespertar = Assert.Single(pasoAmanecer.Pulsos, p => p.Hito?.Id == HitosRutina.Despertar);
        Assert.True(pulsoDespertar.Id > pulsoAcostarse.Id);

        // Llega tarde la reflexión de acostarse en la misma época: NO es pulso_obsoleto y se aplica UNA vez.
        var respAcostarse = new Respuesta
        {
            Pensamiento = "Ayer junto al lago encontré calma aunque estuve sola.",
            Epoca = epoca,
            BasadaEn = pulsoAcostarse.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Trabajar a solas en la orilla despejó el dibujo.",
                        OrigenDiario = new List<string> { idEpisodio },
                    },
                },
            },
        };

        var r1 = api.ProcesarRespuestaEnHiloPrincipal(respAcostarse);
        Assert.Equal(Resultado.Aceptada, r1.Estado);
        var recuerdo = Assert.Single(estado.Recuerdos);
        Assert.Equal(new[] { idEpisodio }, recuerdo.OrigenDiario);

        // Repetir el envío conserva una sola copia.
        var r2 = api.ProcesarRespuestaEnHiloPrincipal(respAcostarse);
        Assert.Equal(Resultado.Aceptada, r2.Estado);
        Assert.Single(estado.Recuerdos);

        // Una reflexión con otra época sigue recibiendo epoca_obsoleta.
        var rOtraEpoca = api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Pensamiento = "Intento de otra sesión.",
            Epoca = "epoca-vieja",
            BasadaEn = pulsoAcostarse.Id,
            Memoria = respAcostarse.Memoria,
        });
        Assert.Equal(Resultado.Rechazada, rOtraEpoca.Estado);
        Assert.Contains(rOtraEpoca.Motivos!, m => m.Codigo == ValidadorEpoca.CodigoEpocaObsoleta);
    }

    [Fact]
    public void FakeWorld_guardado_antes_de_respuesta_acostarse_persiste_tras_siguiente_guardado_y_llega_al_despertar()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var archivoGuardado = new Dictionary<string, EstadoPartida>(StringComparer.Ordinal);
        var api = new ApiMotor("0.1.0");
        var epoca = ApiMotor.GenerarEpoca();
        api.IniciarEpoca(epoca, new[] { perfil.Id });

        var gestor1 = new GestorPersistenciaPartida();
        var carga1 = gestor1.Cargar("dev_save", () => null, new[] { perfil.Id });
        var estado1 = carga1.Estado.ObtenerOIniciar(perfil.Id);
        var mundo = Escenario.Pasarela()
            .ALas(dia: 68, hora: 1700)
            .ConHorarios((_, _) => CrearHorarioDia("Fri", (900, "Town", 10, 10), (1600, "Saloon", 20, 20)))
            .Construir(perfil.Lexico);

        var motor1 = new MotorHabitante(
            perfil,
            plantillas,
            estado1,
            ModoReloj.Vanilla,
            publicarPulso: p => api.EmitirPulso(p))
        {
            Epoca = epoca,
        };
        CoordinadorApiAnfitrion.Conectar(
            api,
            id => id == perfil.Id ? motor1 : null,
            id => gestor1.EstadoActual.ObtenerSiExiste(id),
            enHiloPrincipal: false);

        // Tarde del día 68 y cadena de acostarse.
        motor1.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, ahoraMs: 1_000, minutosJuego: 10);
        var pulsoAcostarse = motor1.ProcesarFinDeDia(mundo.Observar(), ahoraMs: 2_000, horario: mundo.HorarioHoy)!;
        var idEpisodio = pulsoAcostarse.EpisodiosDiario!.Keys.First();

        // Vanilla guarda ANTES de que llegue la respuesta de acostarse.
        var g1 = gestor1.Guardar(partida => archivoGuardado[PersistenciaEstado.ClaveGuardado] = ClonarPartida(partida));
        Assert.True(g1.Guardado);
        Assert.Empty(archivoGuardado[PersistenciaEstado.ClaveGuardado].ObtenerSiExiste(perfil.Id)!.Recuerdos);

        // Llega la respuesta tardía en la misma época (día 69 ya amanecido).
        var res = api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
        {
            Pensamiento = "Cierro el cuaderno con lo pensado junto al agua.",
            Epoca = epoca,
            BasadaEn = pulsoAcostarse.Id,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "La pasarela en calma dejó una línea clara.",
                        OrigenDiario = new List<string> { idEpisodio },
                    },
                },
            },
        });
        Assert.Equal(Resultado.Aceptada, res.Estado);

        // Siguiente guardado después de aplicar la reflexión.
        var g2 = gestor1.Guardar(partida => archivoGuardado[PersistenciaEstado.ClaveGuardado] = ClonarPartida(partida));
        Assert.True(g2.Guardado);

        // Recarga de partida en una nueva sesión / nuevo motor.
        var gestor2 = new GestorPersistenciaPartida();
        var carga2 = gestor2.Cargar(
            "dev_save",
            () => archivoGuardado.TryGetValue(PersistenciaEstado.ClaveGuardado, out var guardado) ? ClonarPartida(guardado) : null,
            new[] { perfil.Id });
        var estadoRecargado = carga2.Estado.ObtenerOIniciar(perfil.Id);
        var recuerdoRecargado = Assert.Single(estadoRecargado.Recuerdos);
        Assert.Equal(new[] { idEpisodio }, recuerdoRecargado.OrigenDiario);

        var epoca2 = ApiMotor.GenerarEpoca();
        var motor2 = new MotorHabitante(perfil, plantillas, estadoRecargado, ModoReloj.Vanilla) { Epoca = epoca2 };
        var pasoDespertar = motor2.ProcesarPaso(
            new Observacion(70, 600, "LeahHouse", 5, 5, 1.0, "soleado", Array.Empty<EntidadObservada>()),
            CrearHorarioDia("Sun", (900, "Forest", 40, 30)),
            ahoraMs: 4_000,
            minutosJuego: 0);

        var pulsoDespertar = Assert.Single(pasoDespertar.Pulsos, p => p.Hito?.Id == HitosRutina.Despertar);
        Assert.Contains(pulsoDespertar.Recuerdos!, r => r.Contains("pasarela en calma"));
    }

    [Fact]
    public void Despertar_valida_agenda_solo_con_horario_de_hoy_o_planes_permitidos_y_rechaza_violacion_r16()
    {
        var perfil = CargarPerfil();
        perfil.Rutina.Anotaciones = new List<AnotacionHito>
        {
            new()
            {
                Id = "saloon_fijo",
                Si = new PatronEntrada { DesdeHora = 1900, HastaHora = 2000 },
                Cambios = new List<string> { TiposCambioRutina.Saltar },
                Limites = new LimitesCambio { Nunca = new List<string> { "lluvia" } },
            },
        };
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        estado.Compromisos.Add(new CompromisoEstado
        {
            Id = "comp-12",
            Tipo = TiposActo.Promete,
            Destinatario = "farmer:123",
            Objeto = "retrato del lago",
            PlazoVigente = new TiempoJuego { Dia = 69, Hora = 1900 },
            Estado = "pendiente",
        });

        var motor = new MotorHabitante(perfil, plantillas, estado, ModoReloj.Vanilla)
        {
            Epoca = "ep-agenda",
        };
        motor.ContextoRutina.AvanzarDia(69, new[] { "lluvia" });

        var paso = motor.ProcesarPaso(
            new Observacion(69, 600, "LeahHouse", 5, 5, 1.0, "lluvia", Array.Empty<EntidadObservada>()),
            CrearHorarioDia("Fri", (900, "Forest", 40, 30), (1900, "Saloon", 10, 20)),
            ahoraMs: 1_000,
            minutosJuego: 0);
        var pulso = Assert.Single(paso.Pulsos, p => p.Hito?.Id == HitosRutina.Despertar);
        Assert.Contains(pulso.CompromisosProximos!, c => c.Id == "comp-12");

        // Respuesta válida: entradas reales del horario de hoy + compromiso pendiente explícito en la agenda.
        var respValida = new Respuesta
        {
            Pensamiento = "Llueve hoy; mantengo el Saloon y dejo hueco para el compromiso.",
            Epoca = "ep-agenda",
            BasadaEn = pulso.Id,
            Agenda = new List<EstadoAgenda>
            {
                new() { Hito = "Fri:900", Estado = EstadoHito.Pendiente },
                new() { Hito = "Fri:1900", Estado = EstadoHito.Pendiente },
                new() { Hito = "comp-12", Estado = EstadoHito.Pendiente },
            },
        };
        var ok = motor.AplicarRespuesta(pulso, respValida);
        Assert.Equal(Resultado.Aceptada, ok.Estado);

        // Hito inexistente en la agenda de hoy o salto prohibido en día de lluvia -> rechazado por R-16.
        var respViolacionR16 = new Respuesta
        {
            Pensamiento = "Salto el Saloon aunque llueva y meto un hito inventado.",
            Epoca = "ep-agenda",
            BasadaEn = pulso.Id,
            Intencion = new IntencionPropuesta
            {
                Objetivo = "quedarme en casa",
                Porque = "no quiero salir",
                Plan = new List<PasoPlan> { new() { Accion = "guardar_cuaderno" } },
                CambiosRutina = new List<CambioRutina>
                {
                    new() { Hito = "Fri:1900", Cambio = TiposCambioRutina.Saltar },
                },
            },
            Agenda = new List<EstadoAgenda>
            {
                new() { Hito = "hito_inventado:9999", Estado = EstadoHito.Pendiente },
                new() { Hito = "Fri:1900", Estado = EstadoHito.Saltado },
            },
        };
        var rechazada = motor.AplicarRespuesta(pulso, respViolacionR16);
        Assert.Equal(Resultado.Rechazada, rechazada.Estado);
        Assert.Contains(rechazada.Motivos!, m => m.Codigo == "condicion_prohibida");
        Assert.Contains(rechazada.Motivos!, m => m.Codigo == "hito_inexistente");
    }

    private static EstadoPartida ClonarPartida(EstadoPartida origen)
        => PersistenciaEstado.Deserializar(PersistenciaEstado.Serializar(origen));
}
