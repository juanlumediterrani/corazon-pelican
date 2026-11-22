using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

public class CuerpoVocabularioTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Cada_accion_del_vocabulario_v1_termina_en_cumplida_en_condiciones_validas()
    {
        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 6, distancia: 4)
            .ConHorarios((_, _) => new HorarioDia("fall", new[]
            {
                new EntradaHorario(900, "LeahHouse", 3, 7, "leah_sculpt"),
                new EntradaHorario(1700, "Mountain", 40, 30),
            }))
            .Construir();

        var pasos = new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "sentarse", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "mirar", Objetivo = "farmer:1" },
            new PasoPlan { Accion = "gesto", Tipo = "saludo" },
            new PasoPlan { Accion = "murmurar", Texto = "Qué buena luz hay en la orilla." },
            new PasoPlan { Accion = "dibujar", Obra = "nueva" },
            new PasoPlan { Accion = "levantar_la_vista" },
            new PasoPlan { Accion = "dibujar", Obra = "boceto_lago-0001" },
            new PasoPlan { Accion = "guardar_cuaderno" },
            new PasoPlan { Accion = "esperar", DuracionMin = 10 },
            new PasoPlan { Accion = "volver_a_rutina" },
        };

        mundo.Cuerpo.Ejecutar(pasos, "int-vocabulario-completo");
        mundo.Avanzar(TimeSpan.FromSeconds(25));

        Assert.False(mundo.Cuerpo.Ocupado);
        Assert.DoesNotContain(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.AccionBloqueada);
        Assert.Equal(pasos.Length, mundo.Cuerpo.Sucesos.Count(s => s.Tipo == TipoSuceso.AccionTerminada));
        Assert.Single(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado && s.Detalle == "int-vocabulario-completo");
        Assert.Equal("saludo", mundo.Cuerpo.UltimoGesto);
        Assert.Equal("Qué buena luz hay en la orilla.", mundo.Cuerpo.UltimoMurmullo);
    }

    [Theory]
    [InlineData("ir_a", "lugar_inexistente", null, null, null, null, "lugar desconocido")]
    [InlineData("sentarse", "lugar_no_sentable", null, null, null, null, "lugar no sentable")]
    [InlineData("mirar", null, "npc:fantasma", null, null, null, "objetivo no encontrado")]
    [InlineData("mirar", null, "!!!invalido", null, null, null, "objetivo inválido")]
    [InlineData("dibujar", null, null, "id_invalido", null, null, "identificador de obra inválido")]
    [InlineData("gesto", null, null, null, "gesto_inventado", null, "tipo de gesto inválido")]
    [InlineData("murmurar", null, null, null, null, "", "texto vacío")]
    [InlineData("accion_inventada", null, null, null, null, null, "acción desconocida")]
    public void Cada_accion_invalida_o_imposible_termina_bloqueada_con_motivo(
        string accion,
        string? lugar,
        string? objetivo,
        string? obra,
        string? tipo,
        string? texto,
        string fragmentoMotivoEsperado)
    {
        var mundo = Escenario.Pasarela()
            .ConLugar(new LugarSimulado("lugar_no_sentable", "Mountain", 41, 30, "las piedras húmedas", Sentable: false))
            .Construir();

        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan
            {
                Accion = accion,
                Lugar = lugar,
                Objetivo = objetivo,
                Obra = obra,
                Tipo = tipo,
                Texto = texto,
            },
        });
        mundo.Avanzar(TimeSpan.FromSeconds(2));

        Assert.False(mundo.Cuerpo.Ocupado);
        var bloqueo = Assert.Single(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.AccionBloqueada);
        Assert.Equal(accion, bloqueo.Accion);
        Assert.Contains(fragmentoMotivoEsperado, bloqueo.Detalle!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado);
    }

    [Fact]
    public void Ninguna_accion_queda_colgada_mas_de_su_duracion_maxima()
    {
        var mundo = Escenario.Pasarela().Construir();
        mundo.Cuerpo.SimularRutaColgada = true;

        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
        }, "int-colgada");

        // Antes del tope (180 s) sigue en curso; al superar el tope se bloquea con tiempo_agotado:ir_a.
        mundo.Avanzar(TimeSpan.FromSeconds(100));
        Assert.True(mundo.Cuerpo.Ocupado);

        mundo.Avanzar(TimeSpan.FromSeconds(85));
        Assert.False(mundo.Cuerpo.Ocupado);
        var bloqueo = Assert.Single(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.AccionBloqueada);
        Assert.Contains("tiempo_agotado:ir_a", bloqueo.Detalle!, StringComparison.Ordinal);
        Assert.DoesNotContain(mundo.Cuerpo.Sucesos, s => s.Tipo == TipoSuceso.PlanTerminado);
    }

    [Fact]
    public void Mirar_y_levantar_la_vista_cambian_el_foco_del_sensorio()
    {
        var perfil = CargarPerfil();
        var mundo = Escenario.Pasarela()
            .ConJugador("Ana", corazones: 2, distancia: 8)
            .Construir(perfil.Lexico);

        var obs = mundo.Observar();
        var novedad = new Novedad();
        var contextoBase = ContextoSaliencia.DesdePerfil(perfil);
        var salienciaSinMirar = Saliencia.Calcular(obs, contextoBase, novedad)
            .Single(s => s.Ref == "farmer:1").Total;

        // 1. mirar("farmer:1") enfoca al granjero y eleva su saliencia.
        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "mirar", Objetivo = "farmer:1" } });
        Assert.Equal("farmer:1", mundo.Cuerpo.FocoObjetivoRef);
        Assert.Equal(EstadoAtencion.Abierta, mundo.Cuerpo.EstadoAtencion);

        var contextoMirando = ContextoSaliencia.DesdePerfil(perfil, mundo.Cuerpo.EstadoAtencion, focoObjetivoRef: mundo.Cuerpo.FocoObjetivoRef);
        var salienciaMirando = Saliencia.Calcular(obs, contextoMirando, novedad)
            .Single(s => s.Ref == "farmer:1").Total;
        Assert.True(salienciaMirando > salienciaSinMirar);

        // 2. dibujar estrecha la atención; levantar_la_vista restaura atención abierta y limpia el foco.
        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "dibujar", Obra = "nueva" } });
        Assert.Equal(EstadoAtencion.Estrecha, mundo.Cuerpo.EstadoAtencion);

        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "levantar_la_vista" } });
        Assert.Null(mundo.Cuerpo.FocoObjetivoRef);
        Assert.Equal(EstadoAtencion.Abierta, mundo.Cuerpo.EstadoAtencion);

        var contextoAbierto = ContextoSaliencia.DesdePerfil(perfil, mundo.Cuerpo.EstadoAtencion, focoObjetivoRef: mundo.Cuerpo.FocoObjetivoRef);
        var salienciaRestaurada = Saliencia.Calcular(obs, contextoAbierto, novedad)
            .Single(s => s.Ref == "farmer:1").Total;
        Assert.Equal(salienciaSinMirar, salienciaRestaurada, precision: 6);
    }

    [Fact]
    public void Ejecutor_conectado_a_MotorHabitante_registra_prosa_en_bufer_y_dispara_hito_al_terminar_plan()
    {
        var perfil = CargarPerfil();
        var plantillas = CargarPlantillas();
        var estado = new EstadoPersonaje();
        var mundo = Escenario.Pasarela().ALas(dia: 12, hora: 600).Construir(perfil.Lexico);

        var motor = new MotorHabitante(perfil, plantillas, estado, mundo.Reloj.Modo);
        motor.ConectarEjecutorCuerpo(mundo.Cuerpo.Ejecutor, () => mundo.Reloj.MilisegundosReales);

        // Primera observación del día (consume despertar).
        var pasoInicial = motor.ProcesarPaso(mundo.Observar(), horario: null, mundo.Reloj.MilisegundosReales, 0);
        Assert.Contains(pasoInicial.Disparos, d => d.Hito?.Id == HitosRutina.Despertar);

        // Lanzamos el plan "int-pasarela" con sentarse en pasarela_lago (lleva a pasarela, se sienta y termina).
        motor.Disparadores.IniciarPlanIntencion("int-pasarela", mundo.Reloj.MilisegundosReales);
        mundo.Cuerpo.Ejecutar(new[]
        {
            new PasoPlan { Accion = "ir_a", Lugar = "orilla_norte" },
            new PasoPlan { Accion = "sentarse", Lugar = "pasarela_lago" },
        }, "int-pasarela");

        var pulsosDurantePlan = new List<Pulso>();
        var pasos = 40 * 1000 / (int)FakeWorld.PasoMs;
        for (var i = 0; i < pasos; i++)
        {
            var obs = mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            motor.NotificarActividadMente(mundo.Reloj.MilisegundosReales);
            var res = motor.ProcesarPaso(obs, horario: null, mundo.Reloj.MilisegundosReales, 0.35);
            pulsosDurantePlan.AddRange(res.Pulsos);
            if (!mundo.Cuerpo.Ocupado && pulsosDurantePlan.Any(p => p.Hito?.Id == "int-pasarela"))
            {
                break;
            }
        }

        Assert.True(mundo.Cuerpo.Sentada);
        Assert.False(mundo.Cuerpo.Ocupado);
        Assert.NotEmpty(motor.Bufer.Entradas);
        Assert.Contains(pulsosDurantePlan, p => p.Motivo == Motivos.Hito && p.Hito?.Id == "int-pasarela" && p.Hito?.Origen == HitosRutina.OrigenIntencion);
    }
}
