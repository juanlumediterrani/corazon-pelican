using System.Diagnostics;
using System.Text.Json.Nodes;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Observabilidad;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

public class DialogoIntercepcionTests : IDisposable
{
    private readonly string tempDir = Path.Combine(
        Path.GetTempPath(),
        "corazon-pelican-cp503-" + Guid.NewGuid().ToString("N"));

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

    [Fact]
    public void Reflejo_inmediato_emite_pulso_con_caducidad_en_menos_de_300ms_y_entrega_frase_pensada()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var telemetria = new TelemetriaJsonl(this.tempDir, "save_cp503", habilitada: true);
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 4, distancia: 1).ALas(68, 1500).Construir(perfil.Lexico);

        var pulsosEmitidos = new List<Pulso>();
        long nextId = 1;
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            mundo.Reloj.Modo,
            telemetria: telemetria,
            publicarPulso: p =>
            {
                p.Id = nextId++;
                pulsosEmitidos.Add(p);
                return p;
            },
            tiempoMaxRespuestaMs: 4000)
        {
            Epoca = "epoca-cp503",
            MenteHabilitada = true,
        };

        var resultadosDialogo = new List<DesenlaceDialogoTurno>();
        motor.ResultadoDialogoDisponible += r => resultadosDialogo.Add(r);

        var obs = mundo.Observar();
        var sw = Stopwatch.StartNew();
        var reaccion = motor.IniciarTurnoDialogo(obs, "farmer:1", "Ana", ahoraMs: 1000, reaccionMs: 2.5);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 300);
        Assert.Equal(GestorDialogo.EmotePensando, reaccion.EmoteCodigo);
        Assert.False(string.IsNullOrWhiteSpace(reaccion.LineaBreve));
        Assert.NotNull(reaccion.Pulso);
        Assert.Equal(Motivos.Interlocucion, reaccion.Pulso!.Motivo);
        Assert.Equal(4000, reaccion.Pulso.CaducaEnMs);
        Assert.Equal("farmer:1", reaccion.Pulso.Interlocutor?.Ref);
        Assert.True(motor.Dialogo.HayTurnoActivo);

        // Respuesta pensada antes de caducar -> muestra la frase.
        var res = motor.AplicarRespuesta(reaccion.Pulso, new Respuesta
        {
            Epoca = "epoca-cp503",
            BasadaEn = reaccion.Pulso.Id,
            Pensamiento = "Saludo cálido a Ana junto al lago.",
            Frase = "Hoy el agua tiene un tono distinto, ¿verdad?",
        });

        Assert.Equal(Resultado.Aceptada, res.Estado);
        var entregado = Assert.Single(resultadosDialogo);
        Assert.Equal(DesenlaceTurnoDialogo.MostrarFrasePensada, entregado.Desenlace);
        Assert.Equal("Hoy el agua tiene un tono distinto, ¿verdad?", entregado.Frase);
        Assert.False(motor.Dialogo.HayTurnoActivo);

        // Telemetría registra el reflejo < 300 ms en dialogos.jsonl.
        var rutaDialogos = telemetria.RutaArchivo(TelemetriaJsonl.ArchivoDialogos);
        Assert.True(File.Exists(rutaDialogos));
        var doc = JsonNode.Parse(File.ReadAllLines(rutaDialogos)[0])!.AsObject();
        Assert.Equal("reflejo", doc["evento"]!.GetValue<string>());
        Assert.True(doc["reaccion_ms"]!.GetValue<double>() < 300.0);
    }

    [Fact]
    public void Reutiliza_el_pulso_anticipado_cuando_existe_para_el_mismo_interlocutor()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 4, distancia: 1).ALas(68, 1500).Construir(perfil.Lexico);

        var emitidos = 0;
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            mundo.Reloj.Modo,
            publicarPulso: p =>
            {
                emitidos++;
                p.Id = 99;
                return p;
            });

        var obs = mundo.Observar();
        var anticipado = new Pulso
        {
            Id = 55,
            Epoca = "local",
            Personaje = perfil.Id,
            Motivo = Motivos.Interlocucion,
            Anticipado = true,
            TiempoJuego = new TiempoJuego { Dia = obs.Dia, Hora = obs.Hora },
            Interlocutor = new Interlocutor { Ref = "farmer:1" },
            CaducaEnMs = 3000,
        };
        motor.Dialogo.RegistrarPulsoAnticipado(anticipado);

        var reaccion = motor.IniciarTurnoDialogo(obs, "farmer:1", "Ana", ahoraMs: 1200);
        Assert.True(reaccion.ReutilizoAnticipado);
        Assert.Equal(55, reaccion.Pulso?.Id);
        Assert.Equal(0, emitidos);
    }

    [Fact]
    public void Con_mente_caida_o_apagada_se_muestra_el_dialogo_vanilla_y_no_se_intercepta()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 3, distancia: 1).Construir(perfil.Lexico);

        var pulsos = new List<Pulso>();
        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo, publicarPulso: p =>
        {
            p.Id = pulsos.Count + 1;
            pulsos.Add(p);
            return p;
        })
        {
            MenteHabilitada = true,
        };

        // 1. Con mente deshabilitada -> cae a vanilla.
        motor.DesactivarMente(100);
        var dApagada = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
            motor.MenteHabilitada,
            motor.EstaMenteCaida,
            EnEvento: false,
            ValleyTalkActivo: false));
        Assert.False(dApagada.Interceptar);
        Assert.Equal("mente_deshabilitada", dApagada.Motivo);

        // 2. Con mente caída -> cae a vanilla sin emitir pulso de interlocución.
        motor.MenteHabilitada = true;
        motor.NotificarMenteCaida(200);
        Assert.True(motor.EstaMenteCaida);

        var dCaida = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
            motor.MenteHabilitada,
            motor.EstaMenteCaida,
            EnEvento: false,
            ValleyTalkActivo: false));
        Assert.False(dCaida.Interceptar);
        Assert.Equal("mente_caida", dCaida.Motivo);
        Assert.DoesNotContain(pulsos, p => p.Motivo == Motivos.Interlocucion);
    }

    [Fact]
    public void La_mente_nunca_ve_un_pulso_de_interlocucion_durante_un_evento_con_guion()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 5, distancia: 1).Construir(perfil.Lexico);

        var pulsos = new List<Pulso>();
        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo, publicarPulso: p =>
        {
            p.Id = pulsos.Count + 1;
            pulsos.Add(p);
            return p;
        });

        var decision = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
            MenteHabilitada: true,
            MenteCaida: false,
            EnEvento: true,
            ValleyTalkActivo: false));

        Assert.False(decision.Interceptar);
        Assert.Equal("evento_en_curso", decision.Motivo);
        Assert.Throws<InvalidOperationException>(() =>
            motor.IniciarTurnoDialogo(mundo.Observar(), "farmer:1", "Ana", ahoraMs: 500, enEvento: true));
        Assert.DoesNotContain(pulsos, p => p.Motivo == Motivos.Interlocucion);
    }

    [Fact]
    public void Si_el_pulso_caduca_cae_a_vanilla_con_emote_suspensivo_y_si_se_aleja_guarda_en_memoria()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 4, distancia: 1).ALas(68, 1500).Construir(perfil.Lexico);

        long nextId = 1;
        var motor = new MotorHabitante(
            perfil,
            plantillas,
            estado,
            mundo.Reloj.Modo,
            publicarPulso: p =>
            {
                p.Id = nextId++;
                return p;
            },
            tiempoMaxRespuestaMs: 1000)
        {
            Epoca = "epoca-timeout",
            MenteHabilitada = true,
        };

        var obs = mundo.Observar();

        // Caso A: caduca esperando respuesta -> "..." + caída a vanilla.
        _ = motor.IniciarTurnoDialogo(obs, "farmer:1", "Ana", ahoraMs: 1000, caducaEnMs: 1000);
        var antes = motor.EvaluarTickDialogo(ahoraMs: 1500, interlocutorPresente: true, obs.Dia, obs.Hora, "farmer:1");
        Assert.Equal(DesenlaceTurnoDialogo.Ninguno, antes.Desenlace);

        var caducado = motor.EvaluarTickDialogo(ahoraMs: 2100, interlocutorPresente: true, obs.Dia, obs.Hora, "farmer:1");
        Assert.Equal(DesenlaceTurnoDialogo.CaducadoCaerAVanilla, caducado.Desenlace);
        Assert.Equal(GestorDialogo.EmoteCaducado, caducado.EmoteCodigo);
        Assert.Equal(GestorDialogo.TextoCaducado, caducado.Frase);
        Assert.True(motor.EstaMenteCaida);

        // Caso B: el jugador se aleja antes de llegar la respuesta -> se guarda "lo que iba a decir" en memoria.
        motor.NotificarActividadMente(ahoraMs: 3000);
        var resultados = new List<DesenlaceDialogoTurno>();
        motor.ResultadoDialogoDisponible += r => resultados.Add(r);

        var reaccion2 = motor.IniciarTurnoDialogo(obs, "farmer:1", "Ana", ahoraMs: 3000, caducaEnMs: 5000);
        // Quitamos al interlocutor de la observación actual y avanzamos tick.
        motor.ProcesarPaso(new Observacion(obs.Dia, obs.Hora, obs.Ubicacion, obs.X, obs.Y, obs.Luz, obs.Clima, Array.Empty<EntidadObservada>()), null, 3100, 0.1);
        _ = motor.EvaluarTickDialogo(ahoraMs: 3200, interlocutorPresente: false, obs.Dia, obs.Hora, "farmer:1");

        var res2 = motor.AplicarRespuesta(reaccion2.Pulso!, new Respuesta
        {
            Epoca = "epoca-timeout",
            BasadaEn = reaccion2.Pulso!.Id,
            Pensamiento = "Iba a decirle algo.",
            Frase = "Es verdad, gracias por pasarte un momento.",
        });

        Assert.True(res2.Estado == Resultado.Aceptada, string.Join(" | ", res2.Motivos?.Select(m => $"{m.Codigo}:{m.Prosa}") ?? Array.Empty<string>()));
        var guardado = Assert.Single(resultados);
        Assert.Equal(DesenlaceTurnoDialogo.GuardadaLoQueIbaADecir, guardado.Desenlace);
        Assert.Contains(estado.Recuerdos, r => r.Texto.Contains("Lo que iba a decirle a Ana") && r.Texto.Contains("gracias por pasarte"));
    }
}
