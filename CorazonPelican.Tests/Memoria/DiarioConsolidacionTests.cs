using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Config;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Tests.Contrato;

namespace CorazonPelican.Tests.Memoria;

public class DiarioConsolidacionTests
{
    private static readonly JsonSerializerOptions JsonOpciones = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [Fact]
    public void Episodios_reciben_id_estable_unico_que_no_se_reutiliza_tras_podar()
    {
        var estado = new EstadoPersonaje();
        var ep1 = estado.AgregarEpisodio(1, 900, "Miras la lluvia desde el porche.", 0.45, new[] { "clima:lluvia" });
        var ep2 = estado.AgregarEpisodio(1, 1400, "Dibujas el borde del lago.", 0.72, new[] { "agua:Mountain:lago", "agua:Mountain:lago" });
        var ep3 = estado.AgregarEpisodio(2, 1800, "Conversas brevemente con Ana.", 0.65, new[] { "farmer:1" });

        Assert.Equal("ep-1", ep1.Id);
        Assert.Equal("ep-2", ep2.Id);
        Assert.Equal("ep-3", ep3.Id);
        Assert.Single(ep2.Entidades); // Entidades deduplicadas.

        // Podar episodios antiguos de baja saliencia; el siguiente episodio no reutiliza ep-1.
        Consolidacion.Consolidar(estado, diaActual: 20, tamanoMaxMemoriaKB: 512, semividaDias: 5, salienciaMinimaEnvejecida: 0.1);
        Assert.Empty(estado.Diario);

        var ep4 = estado.AgregarEpisodio(20, 1000, "Vuelves al claro del bosque.", 0.80);
        Assert.Equal("ep-4", ep4.Id);

        // Ida y vuelta JSON conserva versión, contador de ids, contadores e intención activa.
        estado.Contadores = new ContadoresPulsos(20, OrdinariosLibresGastados: 3, DespertarEmitido: true);
        estado.IntencionActiva = new IntencionActiva { Id = "int-9", Objetivo = "acabar el boceto" };
        var json = JsonSerializer.Serialize(estado);
        var restaurado = JsonSerializer.Deserialize<EstadoPersonaje>(json)!;

        Assert.Equal(EstadoPersonaje.VersionActual, restaurado.VersionEsquema);
        Assert.Equal(5, restaurado.SiguienteIdEpisodio);
        Assert.Equal("ep-4", Assert.Single(restaurado.Diario).Id);
        Assert.Equal(3, restaurado.Contadores!.OrdinariosLibresGastados);
        Assert.Equal("int-9", restaurado.IntencionActiva!.Id);
    }

    [Fact]
    public void Diario_respeta_tamano_maximo_memoria_kb_tras_un_ano_simulado()
    {
        var estado = new EstadoPersonaje();
        const int topeKB = 64;
        const int maxBytes = topeKB * 1024;

        // Año de Stardew (4 estaciones × 28 días = 112 días), ~16 episodios diarios (> 400 KB sin podar).
        for (var dia = 1; dia <= 112; dia++)
        {
            for (var hora = 800; hora <= 2300; hora += 100)
            {
                var saliencia = hora == 1800 ? 0.92 : 0.45 + ((hora % 300) / 1000.0);
                estado.AgregarEpisodio(
                    dia,
                    hora,
                    $"Día {dia} a las {hora}: observas el reflejo de la luz sobre el agua y anotas los tonos del entorno.",
                    saliencia,
                    new[] { "agua:Mountain:lago", "luz" });
            }

            var resultado = Consolidacion.Consolidar(estado, dia, topeKB);
            Assert.True(resultado.TamanoBytes <= maxBytes, $"Día {dia}: {resultado.TamanoBytes} > {maxBytes}");
        }

        Assert.True(Consolidacion.MedirTamanoBytes(estado) <= maxBytes);
        Assert.Contains(estado.Diario, e => e.DiaJuego == 112);

        // También con un año calendario completo (365 días) y el tope por defecto de CorazonConfig (512 KB).
        var configDefecto = new CorazonConfig();
        var estadoLargo = new EstadoPersonaje();
        var maxDefectoBytes = configDefecto.TamanoMaxMemoriaKB * 1024;
        var relleno = new string('a', 180);

        for (var dia = 1; dia <= 365; dia++)
        {
            for (var i = 0; i < 12; i++)
            {
                estadoLargo.AgregarEpisodio(
                    dia,
                    800 + (i * 100),
                    $"Jornada {dia}, tramo {i}: {relleno}",
                    0.5 + (i * 0.03),
                    new[] { "agua:Mountain:lago" });
            }

            Consolidacion.Consolidar(estadoLargo, dia, configDefecto.TamanoMaxMemoriaKB);
        }

        Assert.True(Consolidacion.MedirTamanoBytes(estadoLargo) <= maxDefectoBytes);
        Assert.Contains(estadoLargo.Diario, e => e.DiaJuego == 365);
    }

    [Fact]
    public void Poda_prioriza_episodios_de_hoy_y_mayor_saliencia_envejecida()
    {
        var estado = new EstadoPersonaje();
        var antiguoTrivial = estado.AgregarEpisodio(1, 900, "Pasaste junto a una piedra.", 0.41);
        var antiguoImportante = estado.AgregarEpisodio(1, 1700, "Viste una luz extraordinaria sobre el lago durante la tormenta.", 0.98);
        var recienteHoy = estado.AgregarEpisodio(15, 1600, "Guardaste el cuaderno al caer la tarde.", 0.50);

        Assert.True(
            Consolidacion.SalienciaEnvejecida(antiguoImportante, 15) > Consolidacion.SalienciaEnvejecida(antiguoTrivial, 15));

        // Tope de 1 KB: obliga a podar al menos el episodio antiguo menos valioso.
        var tamanoSoloUno = Consolidacion.MedirTamanoBytes(new EstadoPersonaje { Diario = { recienteHoy } });
        Assert.True(tamanoSoloUno < 1024);

        // Añadimos texto extra al trivial para superar 1 KB y forzar poda.
        antiguoTrivial.ResumenProsa = new string('x', 800);
        var res = Consolidacion.Consolidar(estado, diaActual: 15, tamanoMaxMemoriaKB: 1);

        Assert.True(res.TamanoBytes <= 1024);
        Assert.DoesNotContain(estado.Diario, e => e.Id == antiguoTrivial.Id);
        Assert.Contains(estado.Diario, e => e.Id == recienteHoy.Id);
    }

    [Fact]
    public void Proyeccion_hacia_episodios_diario_cumple_esquema_pulso_y_validador_cp211()
    {
        var bufer = new Bufer();
        bufer.RegistrarPercepcion(4, 1500, Banda.Mencion, 0.52, "La lluvia golpeaba el tejado mientras trabajabas.", "clima:lluvia");
        bufer.RegistrarAccion(4, 1800, "Al caer la tarde guardaste el cuaderno.", 0.70);

        var estado = new EstadoPersonaje();
        estado.AgregarEpisodio(3, 1200, "Ayer salió el sol.", 0.60);
        var creados = Consolidacion.RegistrarDesdeBufer(estado, bufer);
        Assert.Equal(2, creados.Count);

        var pulso = new Pulso
        {
            Id = 52,
            Epoca = "primavera",
            Personaje = "artista",
            Motivo = Motivos.Hito,
            TiempoJuego = new TiempoJuego { Dia = 4, Hora = 2200 },
            Hito = new HitoPulso { Id = "acostarse", Origen = "rutina", Forma = "cadena" },
            Escena = "Cierras el cuaderno al acabar el día.",
            DiarioDia = new List<string> { "Formato antiguo que debe despejarse." },
        };

        Consolidacion.AplicarEpisodiosAlPulso(pulso, estado, diaJuego: 4);

        Assert.Null(pulso.DiarioDia);
        Assert.NotNull(pulso.EpisodiosDiario);
        Assert.Equal(2, pulso.EpisodiosDiario!.Count);
        Assert.Equal("La lluvia golpeaba el tejado mientras trabajabas.", pulso.EpisodiosDiario[creados[0].Id]);
        Assert.Equal("Al caer la tarde guardaste el cuaderno.", pulso.EpisodiosDiario[creados[1].Id]);

        var nodo = JsonSerializer.SerializeToNode(pulso, JsonOpciones);
        var eval = Esquemas.Evaluar("pulso", nodo);
        Assert.True(eval.IsValid);

        var respuesta = new Respuesta
        {
            Pensamiento = "Recuerdo la lluvia y haber guardado el cuaderno.",
            Epoca = "primavera",
            BasadaEn = 52,
            Memoria = new MemoriaPropuesta
            {
                Reflexiones = new List<ReflexionPropuesta>
                {
                    new()
                    {
                        Texto = "Trabajar bajo la lluvia me dio calma.",
                        OrigenDiario = new List<string> { creados[0].Id, creados[1].Id },
                    },
                },
            },
        };

        Assert.Null(ValidadorOrigenDiario.Comprobar(pulso, respuesta));
    }
}
