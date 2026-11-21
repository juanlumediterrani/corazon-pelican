using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Tests.Contrato;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Atencion;

public class BuferHuellaTests
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void Bufer_solo_admite_percepciones_desde_mencion_y_acciones_propias()
    {
        var bufer = new Bufer();
        var plantillas = CargarPlantillasLeah();
        var entidad = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });

        Assert.False(bufer.RegistrarPercepcion(entidad, Banda.Ausente, 0.05, 68, 1400, plantillas));
        Assert.False(bufer.RegistrarPercepcion(entidad, Banda.Periferia, 0.25, 68, 1410, plantillas));
        Assert.False(bufer.RegistrarPercepcion(68, 1410, Banda.Periferia, 0.25, "Alguien al fondo."));
        Assert.Empty(bufer.Entradas);

        Assert.True(bufer.RegistrarPercepcion(entidad, Banda.Mencion, 0.45, 68, 1420, plantillas));
        Assert.True(bufer.RegistrarPercepcion(entidad, Banda.Foco, 0.78, 68, 1430, plantillas));
        Assert.True(bufer.RegistrarAccion(new PasoPlan { Accion = "levantar_la_vista" }, 68, 1440, plantillas));

        Assert.Equal(3, bufer.Entradas.Count);
        Assert.Equal(OrigenBufer.Percepcion, bufer.Entradas[0].Origen);
        Assert.Equal(OrigenBufer.Percepcion, bufer.Entradas[1].Origen);
        Assert.Equal(OrigenBufer.Accion, bufer.Entradas[2].Origen);
    }

    [Fact]
    public void Bufer_consolidado_respeta_tope_N_y_conserva_mayor_saliencia_de_la_ultima_hora()
    {
        var bufer = new Bufer(maxEntradas: 4, ventanaRecienteMinJuego: 60);

        // Entradas antiguas (> 60 min respecto a 17:00) con saliencia muy alta.
        bufer.RegistrarAccion(68, 1400, "Abres el cuaderno junto al agua.", saliencia: 0.99);
        bufer.RegistrarAccion(68, 1430, "Trazas la curva de la orilla.", saliencia: 0.95);
        bufer.RegistrarAccion(68, 1500, "Miras cómo cambia la luz en la madera.", saliencia: 0.90);

        // Entradas de la última hora (16:00..17:00), menos salientes que las antiguas.
        bufer.RegistrarPercepcion(68, 1610, Banda.Mencion, 0.42, "Notas la presencia de Ana.");
        bufer.RegistrarPercepcion(68, 1640, Banda.Foco, 0.72, "Te quedas mirando el lago.");

        var proyectadas = bufer.Proyectar(68, 1700);

        Assert.Equal(4, proyectadas.Count);
        Assert.Contains(proyectadas, e => e.Hora == 1610 && e.Prosa == "Notas la presencia de Ana.");
        Assert.Contains(proyectadas, e => e.Hora == 1640 && e.Prosa == "Te quedas mirando el lago.");
        Assert.Equal(new[] { 1430, 1500, 1610, 1640 }, proyectadas.Select(e => e.Hora));

        // Si en la última hora hay más de N entradas, conserva las N de mayor saliencia de la última hora.
        var buferSaturado = new Bufer(maxEntradas: 3, ventanaRecienteMinJuego: 60);
        buferSaturado.RegistrarAccion(68, 1500, "Entrada antigua.", saliencia: 0.99);
        buferSaturado.RegistrarPercepcion(68, 1610, Banda.Mencion, 0.41, "Reciente baja.");
        buferSaturado.RegistrarPercepcion(68, 1620, Banda.Mencion, 0.65, "Reciente media.");
        buferSaturado.RegistrarPercepcion(68, 1630, Banda.Foco, 0.88, "Reciente alta.");
        buferSaturado.RegistrarPercepcion(68, 1650, Banda.Foco, 0.75, "Reciente notable.");

        var consolidadas = buferSaturado.Consolidar(68, 1700);

        Assert.Equal(3, consolidadas.Count);
        Assert.Equal(
            new[] { "Reciente media.", "Reciente alta.", "Reciente notable." },
            consolidadas.Select(e => e.Prosa));
    }

    [Fact]
    public void Bufer_consolidado_no_inventa_texto_y_valida_en_pulso()
    {
        var plantillas = CargarPlantillasLeah();
        var perfil = CargarPerfilLeah();
        var bufer = new Bufer(maxEntradas: 5, ventanaRecienteMinJuego: 60);
        var originales = new HashSet<string>(StringComparer.Ordinal);

        var lago = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });
        var ana = new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 26, new[] { "persona" }, Corazones: 6);
        var lugares = perfil.Lexico.OfrecerLugares(_ => true);

        void Anotar(bool ok, string? texto)
        {
            if (ok && texto is not null)
            {
                originales.Add(texto);
            }
        }

        var paso1 = new PasoPlan { Accion = "ir_a", Lugar = "pasarela_lago" };
        Anotar(bufer.RegistrarAccion(paso1, 68, 1500, plantillas, 0.7, lugares), GeneradorAccion.GenerarAccion(paso1, 68, plantillas, lugares));
        Anotar(bufer.RegistrarPercepcion(lago, Banda.Foco, 0.80, 68, 1520, plantillas), GeneradorAccion.GenerarPercepcion(lago, Banda.Foco, 68, plantillas));
        var paso2 = new PasoPlan { Accion = "dibujar", Obra = "nueva" };
        Anotar(bufer.RegistrarAccion(paso2, 68, 1540, plantillas, 0.9, lugares), GeneradorAccion.GenerarAccion(paso2, 68, plantillas, lugares));
        Anotar(bufer.RegistrarPercepcion(ana, Banda.Mencion, 0.55, 68, 1610, plantillas), GeneradorAccion.GenerarPercepcion(ana, Banda.Mencion, 68, plantillas));
        var paso3 = new PasoPlan { Accion = "levantar_la_vista" };
        Anotar(bufer.RegistrarAccion(paso3, 68, 1620, plantillas, 0.6, lugares), GeneradorAccion.GenerarAccion(paso3, 68, plantillas, lugares));
        var paso4 = new PasoPlan { Accion = "murmurar", Texto = "la luz cambia deprisa" };
        Anotar(bufer.RegistrarAccion(paso4, 68, 1640, plantillas, 0.85, lugares), GeneradorAccion.GenerarAccion(paso4, 68, plantillas, lugares));
        Anotar(bufer.RegistrarPercepcion(lago, Banda.Foco, 0.82, 68, 1650, plantillas), GeneradorAccion.GenerarPercepcion(lago, Banda.Foco, 68, plantillas));

        var consolidadas = bufer.Consolidar(68, 1700);

        Assert.InRange(consolidadas.Count, 1, 5);
        Assert.All(consolidadas, entrada => Assert.Contains(entrada.Prosa, originales));

        var mundo = Escenario.Pasarela().ConJugador("Ana", corazones: 6, distancia: 8).Construir(perfil.Lexico);
        var pulso = GeneradorEscena.Generar(mundo.Observar(), perfil, plantillas).CrearPulsoBase(1, "epoca-1", perfil.Id, Motivos.Hito);
        pulso.Hito = new HitoPulso { Id = "fall:1700", Origen = "rutina", Forma = "pensamiento" };
        pulso.Bufer = consolidadas.ToList();

        var nodo = System.Text.Json.JsonSerializer.SerializeToNode(pulso, JsonOpciones);
        var evaluacion = Esquemas.Evaluar("pulso", nodo);
        Assert.True(evaluacion.IsValid);
    }

    [Fact]
    public void Huella_tarde_mirando_el_lago_con_paso_breve_del_jugador_cambia_orden_segun_rasgos_del_perfil()
    {
        var perfilLeah = CargarPerfilLeah();
        var plantillas = CargarPlantillasLeah();

        // Perfil alternativo con interés social alto y menor peso en el agua.
        var rasgosSociales = new Dictionary<string, double>(perfilLeah.Rasgos)
        {
            ["agua"] = 0.6,
            ["persona"] = 4.0,
        };

        var lago = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });
        var jugador = new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 26, new[] { "persona" }, Corazones: 3);

        Huella SimularTarde(IReadOnlyDictionary<string, double> rasgos)
        {
            var contexto = new ContextoSaliencia(
                rasgos,
                EstadoAtencion.Abierta,
                perfilLeah.Umbrales.Periferia,
                new Dictionary<string, double> { ["agua"] = 2.0 });
            var novedad = new Novedad();
            var bandas = new ClasificadorBandas(perfilLeah.Umbrales);
            var huella = new HuellaAtencion();

            // 180 minutos de tarde mirando el lago (14:00..17:00), con un paso breve del jugador de 10 minutos (16:20..16:30).
            var hora = 1400;
            for (var bloque = 0; bloque < 18; bloque++)
            {
                var pasoJugador = bloque == 14;
                var entidades = pasoJugador ? new[] { lago, jugador } : new[] { lago };
                var obs = new Observacion(68, hora, "Mountain", 40, 30, 0.85, "soleado", entidades);

                var saliencias = Saliencia.Calcular(obs, contexto, novedad);
                novedad.Exponer(obs.Entidades.Select(e => e.Ref), 10);
                var enBandas = bandas.Clasificar(saliencias);
                huella.Observar(obs, enBandas, minutosJuego: 10);

                hora = Core.Tiempo.HoraJuego.SumarDiezMinutos(hora);
            }

            return huella.EmitirPulso(68, plantillas);
        }

        var huellaLeah = SimularTarde(perfilLeah.Rasgos);
        var huellaSocial = SimularTarde(rasgosSociales);

        Assert.Equal(180, huellaLeah.DesdeUltimoPensamientoMinJuego);
        Assert.Equal("agua:Mountain:lago", huellaLeah.Entidades[0].Ref);
        Assert.Contains("el lago", huellaLeah.Prosa);

        Assert.Equal(180, huellaSocial.DesdeUltimoPensamientoMinJuego);
        Assert.Equal("farmer:1", huellaSocial.Entidades[0].Ref);
        Assert.NotEqual(huellaLeah.Entidades[0].Ref, huellaSocial.Entidades[0].Ref);
    }

    [Fact]
    public void Huella_solo_nombra_entidades_observadas_en_mencion_o_foco_y_se_reinicia_al_emitir_pulso()
    {
        var plantillas = CargarPlantillasLeah();
        var huella = new HuellaAtencion(semividaMin: 30);

        var lago = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });
        var desconocido = new EntidadObservada("npc:Desconocido", CategoriaEntidad.Npc, "Desconocido", "Mountain", 5, 5, new[] { "persona" }, Visible: false);
        var obs = new Observacion(68, 1500, "Mountain", 40, 30, 0.9, "soleado", new[] { lago, desconocido });

        var bandas = new List<EntidadEnBanda>
        {
            new("agua:Mountain:lago", Banda.Mencion, 0.55),
            new("npc:Desconocido", Banda.Periferia, 0.20),
            new("npc:Fantasma", Banda.Foco, 0.95), // Ausente de Observacion: no debe aparecer nunca.
        };

        // Pasos fraccionarios como en vanilla (0.357 min/paso) durante 10 minutos de juego.
        for (var i = 0; i < 28; i++)
        {
            huella.Observar(obs, bandas, 10.0 / 28.0);
        }

        var sinReiniciar = huella.Generar(68, plantillas);
        Assert.Equal(10, sinReiniciar.DesdeUltimoPensamientoMinJuego);
        var unica = Assert.Single(sinReiniciar.Entidades);
        Assert.Equal("agua:Mountain:lago", unica.Ref);
        Assert.Contains("el lago", sinReiniciar.Prosa);
        Assert.DoesNotContain("Desconocido", sinReiniciar.Prosa);
        Assert.DoesNotContain("Fantasma", sinReiniciar.Prosa);

        // Al emitir pulso, devuelve la huella y reinicia el acumulador.
        var emitida = huella.EmitirPulso(68, plantillas);
        Assert.Equal(sinReiniciar.Prosa, emitida.Prosa);
        Assert.Equal(0, huella.MinutosDesdeUltimoPensamiento);
        Assert.Empty(huella.Entidades);

        var trasReinicio = huella.Generar(68, plantillas);
        Assert.Equal(0, trasReinicio.DesdeUltimoPensamientoMinJuego);
        Assert.Empty(trasReinicio.Entidades);
    }

    [Fact]
    public void Huella_aplica_fuga_temporal_parametrizable()
    {
        var lago = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });
        var pasarela = new EntidadObservada("obj:Mountain:40,30:pasarela", CategoriaEntidad.Objeto, "la pasarela de madera", "Mountain", 40, 30, new[] { "madera" });

        double AtencionRestanteTrasSilencio(double semividaMin)
        {
            var huella = new HuellaAtencion(semividaMin);
            var obs1 = new Observacion(68, 1400, "Mountain", 40, 30, 1.0, "soleado", new[] { lago });
            huella.Observar(obs1, new[] { new EntidadEnBanda(lago.Ref, Banda.Foco, 0.8) }, 20);

            var obs2 = new Observacion(68, 1500, "Mountain", 40, 30, 1.0, "soleado", new[] { pasarela });
            huella.Observar(obs2, new[] { new EntidadEnBanda(pasarela.Ref, Banda.Ausente, 0.05) }, 60);

            return huella.Entidades.Single(e => e.Ref == lago.Ref).Atencion;
        }

        Assert.True(AtencionRestanteTrasSilencio(15) < AtencionRestanteTrasSilencio(90));
    }

    [Fact]
    public void MotorHabitante_usa_parametros_de_atencion_del_perfil_o_los_valores_por_defecto_p10()
    {
        var plantillas = CargarPlantillasLeah();

        // 1. Perfil sin bloque "atencion" -> valores por defecto del motor.
        var perfilDefecto = CargarPerfilLeah();
        perfilDefecto.Atencion = null;
        var motorDefecto = new MotorHabitante(perfilDefecto, plantillas, new CorazonPelican.Core.Memoria.EstadoPersonaje());
        Assert.Equal(Bufer.MaxEntradasPorDefecto, motorDefecto.Bufer.MaxEntradas);
        Assert.Equal(Bufer.VentanaRecientePorDefectoMin, motorDefecto.Bufer.VentanaRecienteMinJuego);
        Assert.Equal(Bufer.SemividaAntiguasPorDefectoMin, motorDefecto.Bufer.SemividaAntiguasMinJuego);
        Assert.Equal(HuellaAtencion.SemividaPorDefectoMin, motorDefecto.Huella.SemividaMin);

        // 2. Perfil distraído (tope 2, ventana corta, semividas cortas) vs atento (tope 8, semividas largas).
        var perfilDistraido = CargarPerfilLeah();
        perfilDistraido.Umbrales.Pico = 0.999;
        perfilDistraido.Umbrales.Deriva = 100.0;
        perfilDistraido.Atencion = new ConfigAtencion
        {
            BuferMaxEntradas = 2,
            BuferVentanaRecienteMin = 20,
            BuferSemividaAntiguasMin = 10,
            HuellaSemividaMin = 10,
        };

        var perfilAtento = CargarPerfilLeah();
        perfilAtento.Umbrales.Pico = 0.999;
        perfilAtento.Umbrales.Deriva = 100.0;
        perfilAtento.Atencion = new ConfigAtencion
        {
            BuferMaxEntradas = 8,
            BuferVentanaRecienteMin = 120,
            BuferSemividaAntiguasMin = 120,
            HuellaSemividaMin = 90,
        };

        var motorDistraido = new MotorHabitante(perfilDistraido, plantillas, new CorazonPelican.Core.Memoria.EstadoPersonaje());
        var motorAtento = new MotorHabitante(perfilAtento, plantillas, new CorazonPelican.Core.Memoria.EstadoPersonaje());

        var lago = new EntidadObservada("agua:Mountain:lago", CategoriaEntidad.Terreno, "el lago", "Mountain", 40, 34, new[] { "agua" });
        var pasarela = new EntidadObservada("obj:Mountain:40,30:pasarela", CategoriaEntidad.Objeto, "la pasarela de madera", "Mountain", 40, 30, new[] { "madera" });
        var obsLago14 = new Observacion(68, 1400, "Mountain", 40, 30, 1.0, "soleado", new[] { lago });
        var obsPasarela15 = new Observacion(68, 1500, "Mountain", 40, 30, 1.0, "soleado", new[] { pasarela });

        var pasoLagoDist = motorDistraido.ProcesarPaso(obsLago14, null, 1_000, minutosJuego: 10);
        var pasoLagoAtento = motorAtento.ProcesarPaso(obsLago14, null, 1_000, minutosJuego: 10);
        Assert.Empty(pasoLagoDist.Pulsos);
        Assert.Empty(pasoLagoAtento.Pulsos);

        // Silencio de 60 minutos de juego sin entidades en ambos motores -> fuga distinta de la huella del lago.
        var obsSilencio = new Observacion(68, 1500, "Mountain", 40, 30, 1.0, "soleado", Array.Empty<EntidadObservada>());
        var pasoSilDist = motorDistraido.ProcesarPaso(obsSilencio, null, 2_000, minutosJuego: 60);
        var pasoSilAtento = motorAtento.ProcesarPaso(obsSilencio, null, 2_000, minutosJuego: 60);
        Assert.Empty(pasoSilDist.Pulsos);
        Assert.Empty(pasoSilAtento.Pulsos);

        var atencionDistraido = motorDistraido.Huella.Entidades.Single(e => e.Ref == lago.Ref).Atencion;
        var atencionAtento = motorAtento.Huella.Entidades.Single(e => e.Ref == lago.Ref).Atencion;
        Assert.True(atencionDistraido < atencionAtento);

        // Llenamos acciones/percepciones para comprobar consolidación con distinta capacidad de búfer.
        motorDistraido.Bufer.RegistrarAccion(68, 1400, "Anotas el agua.", 0.9);
        motorDistraido.Bufer.RegistrarAccion(68, 1420, "Miras los juncos.", 0.8);
        motorDistraido.ProcesarPaso(obsPasarela15, null, 3_000, minutosJuego: 10);

        motorAtento.Bufer.RegistrarAccion(68, 1400, "Anotas el agua.", 0.9);
        motorAtento.Bufer.RegistrarAccion(68, 1420, "Miras los juncos.", 0.8);
        motorAtento.ProcesarPaso(obsPasarela15, null, 3_000, minutosJuego: 10);

        var pulsoDistraido = motorDistraido.ForzarPulso(obsPasarela15, 4_000).Pulso!;
        var pulsoAtento = motorAtento.ForzarPulso(obsPasarela15, 4_000).Pulso!;

        Assert.Equal(2, pulsoDistraido.Bufer.Count);
        Assert.True(pulsoAtento.Bufer.Count > pulsoDistraido.Bufer.Count);
    }
}
