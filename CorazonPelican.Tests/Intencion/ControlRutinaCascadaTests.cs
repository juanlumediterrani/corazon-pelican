using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

public class ControlRutinaCascadaTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static readonly HorarioDia HorarioViernes = new("Fri", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "LeahHouse", 6, 5),
        new EntradaHorario(1600, "Saloon", 2, 20),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2340, "LeahHouse", 2, 4, "leah_sleep"),
    });

    private static readonly HorarioDia HorarioOtono = new("fall", new[]
    {
        new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
        new EntradaHorario(1200, "Forest", 33, 25, "leah_draw"),
        new EntradaHorario(1900, "LeahHouse", 8, 5),
        new EntradaHorario(2400, "LeahHouse", 2, 4, "leah_sleep"),
    });

    [Fact]
    public void Saltar_un_hito_no_altera_los_siguientes_y_al_dia_siguiente_la_rutina_es_la_canonica()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();

        var mundo = Escenario.Pasarela()
            .ConHorarios((dia, _) => dia == 68 ? HorarioViernes : HorarioOtono)
            .ALas(dia: 68, hora: HoraJuego.InicioDia)
            .Construir(perfil.Lexico);

        long nextId = 1;
        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo, publicarPulso: p =>
        {
            p.Id = nextId++;
            return p;
        })
        {
            Epoca = "epoca-cp306",
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        var pasoDespertar = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 0);
        var pulsoDespertar = Assert.Single(pasoDespertar.Pulsos, p => p.Hito?.Id == HitosRutina.Despertar);

        var res = motor.AplicarRespuesta(pulsoDespertar, new Respuesta
        {
            Epoca = "epoca-cp306",
            BasadaEn = pulsoDespertar.Id,
            Pensamiento = "Hoy prefiero quedarme tranquila en casa.",
            Intencion = new IntencionPropuesta
            {
                Objetivo = "Quedarme tranquila esta tarde",
                Porque = "Prefiero evitar la salida de las cuatro",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 10 } },
                CambiosRutina = new List<CambioRutina>
                {
                    new() { Hito = "Fri:1600", Cambio = TiposCambioRutina.Saltar },
                },
            },
        });

        Assert.Equal(Resultado.Aceptada, res.Estado);
        Assert.Equal(EstadoHito.Saltado, motor.AgendaActual!.Buscar("Fri:1600")!.Estado);
        Assert.Equal(EstadoHito.Pendiente, motor.AgendaActual.Buscar("Fri:1900")!.Estado);
        Assert.Equal(EstadoHito.Pendiente, motor.AgendaActual.Buscar("Fri:2340")!.Estado);
        Assert.Single(mundo.Cuerpo.CambiosHorarioAplicados, c => c.Hito == "Fri:1600" && c.Estado == EstadoHito.Saltado);
        Assert.Contains(estado.Recuerdos, r => r.Texto.Contains("Fri:1600"));

        // Al día siguiente (día 69), la agenda vuelve a ser la canónica sin arrastrar el salto.
        mundo.Reloj.Juego.Dia = 69;
        mundo.Reloj.Juego.Hora = HoraJuego.InicioDia;
        motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 0);
        Assert.Equal("fall", motor.AgendaActual!.ClaveHorario);
        Assert.All(motor.AgendaActual.Entradas, e => Assert.Equal(EstadoHito.Pendiente, e.Estado));
    }

    [Fact]
    public void Dia_completo_en_cascada_reequilibra_la_agenda_tras_lluvia_y_el_cuerpo_sigue_la_nueva()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();

        var mundo = Escenario.Pasarela()
            .ConHorarios((_, _) => HorarioOtono)
            .ALas(dia: 68, hora: HoraJuego.InicioDia)
            .Construir(perfil.Lexico);

        long nextId = 1;
        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo, publicarPulso: p =>
        {
            p.Id = nextId++;
            return p;
        })
        {
            Epoca = "epoca-cascada",
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        // 1. Despertar: agenda inicial del día sin cambios.
        var pasoDespertar = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 0);
        var pulsoDespertar = Assert.Single(pasoDespertar.Pulsos, p => p.Hito?.Id == HitosRutina.Despertar);
        Assert.Equal(Resultado.Aceptada, motor.AplicarRespuesta(pulsoDespertar, new Respuesta
        {
            Epoca = "epoca-cascada",
            BasadaEn = pulsoDespertar.Id,
            Pensamiento = "Empiezo el día con el plan normal.",
        }).Estado);

        // 1b. Hito de las 09:00 al disponerse (08:50): avanza el plan del día.
        mundo.Reloj.Avanzar(1_000);
        mundo.Reloj.Juego.Hora = 850;
        var paso900 = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 10);
        var pulso900 = Assert.Single(paso900.Pulsos, p => p.Motivo == Motivos.Hito && p.Hito?.Id == "fall:900");
        Assert.Equal(Resultado.Aceptada, motor.AplicarRespuesta(pulso900, new Respuesta
        {
            Epoca = "epoca-cascada",
            BasadaEn = pulso900.Id,
            Pensamiento = "Sigo con la mañana tranquila.",
        }).Estado);

        // 2. Empieza a llover antes del hito de dibujar fuera (1200): la mente reequilibra sustituyendo fall:1200.
        mundo.Clima = "lluvia";
        mundo.Reloj.Avanzar(1_000);
        mundo.Reloj.Juego.Hora = 1150;
        var pasoHito = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 10);
        var pulsoHito = Assert.Single(pasoHito.Pulsos, p => p.Motivo == Motivos.Hito && p.Hito?.Id == "fall:1200");

        var resReequilibrio = motor.AplicarRespuesta(pulsoHito, new Respuesta
        {
            Epoca = "epoca-cascada",
            BasadaEn = pulsoHito.Id,
            Pensamiento = "Ha empezado a llover; mejor me acerco un instante a mirar la orilla norte.",
            Intencion = new IntencionPropuesta
            {
                Objetivo = "Ver cómo cae la lluvia sobre la orilla norte",
                Porque = "No quiero mojar el cuaderno",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 10 } },
                CambiosRutina = new List<CambioRutina>
                {
                    new()
                    {
                        Hito = "fall:1200",
                        Cambio = TiposCambioRutina.Sustituir,
                        Plan = new List<PasoPlan>
                        {
                            new() { Accion = "ir_a", Lugar = "orilla_norte" },
                        },
                    },
                },
            },
        });

        Assert.Equal(Resultado.Aceptada, resReequilibrio.Estado);
        Assert.Equal(EstadoHito.Sustituido, motor.AgendaActual!.Buscar("fall:1200")!.Estado);

        // 3. Al llegar las 12:00 y avanzar el cuerpo, ejecuta el plan sustituto hacia orilla_norte.
        mundo.Reloj.Juego.Hora = 1200;
        var llegoAOrillaNorte = false;
        for (var i = 0; i < 200; i++)
        {
            var obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            motor.NotificarActividadMente(mundo.Reloj.MilisegundosReales);
            var paso = motor.ProcesarPaso(obs, mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 0.25);
            foreach (var p in paso.Pulsos)
            {
                motor.AplicarRespuesta(p, new Respuesta
                {
                    Epoca = "epoca-cascada",
                    BasadaEn = p.Id,
                    Pensamiento = "sigo con el plan sustituto.",
                });
            }

            if (mundo.Personaje.Ubicacion == "Mountain" && mundo.Personaje.X == 64 && mundo.Personaje.Y == 14)
            {
                llegoAOrillaNorte = true;
            }

            if (llegoAOrillaNorte && mundo.Cuerpo.Sucesos.Any(s => s.Tipo == TipoSuceso.PlanTerminado && s.Detalle == "fall:1200"))
            {
                break;
            }
        }

        Assert.True(llegoAOrillaNorte);
        Assert.Contains(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado && s.Detalle == "fall:1200");
    }

    [Fact]
    public void Con_cambio_de_rutina_activo_y_mente_caida_restaura_horario_canonico_y_degrada()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();

        var mundo = Escenario.Pasarela()
            .ConHorarios((_, _) => HorarioViernes)
            .ALas(dia: 68, hora: HoraJuego.InicioDia)
            .Construir(perfil.Lexico);

        long nextId = 1;
        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo, publicarPulso: p =>
        {
            p.Id = nextId++;
            return p;
        })
        {
            Epoca = "epoca-caida-rutina",
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        var paso = motor.ProcesarPaso(mundo.Observar(), mundo.HorarioHoy, mundo.Reloj.MilisegundosReales, 0);
        var pulso = paso.Pulsos.Single();

        Assert.Equal(Resultado.Aceptada, motor.AplicarRespuesta(pulso, new Respuesta
        {
            Epoca = "epoca-caida-rutina",
            BasadaEn = pulso.Id,
            Pensamiento = "hoy prefiero evitar esa salida.",
            Intencion = new IntencionPropuesta
            {
                Objetivo = "evitar esa salida",
                Porque = "prefiero quedarme tranquila en casa",
                Plan = new List<PasoPlan> { new() { Accion = "esperar", DuracionMin = 10 } },
                CambiosRutina = new List<CambioRutina>
                {
                    new() { Hito = "Fri:1600", Cambio = TiposCambioRutina.Saltar },
                },
            },
        }).Estado);

        // Cae la mente -> degrada y restaura el horario canónico.
        motor.NotificarMenteCaida(mundo.Reloj.MilisegundosReales + 100);
        Assert.True(mundo.Cuerpo.HorarioCanonicoRestaurado);
        Assert.Equal("mente_caida", motor.UltimoMotivoDegradacion);
    }
}
