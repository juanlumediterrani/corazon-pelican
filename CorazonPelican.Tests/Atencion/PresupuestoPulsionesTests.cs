using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Contrato;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Atencion;

public class PresupuestoPulsionesTests
{
    private static PerfilPersonaje Perfil()
    {
        var ruta = Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json");
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(File.ReadAllText(ruta))!;
        perfil.Presupuesto.Vanilla.PulsosPorDiaJuego = 3; // Dos cadenas y una plaza libre.
        perfil.Presupuesto.Vanilla.ExtraCompromisos = 1;
        return perfil;
    }

    private static Observacion Escena(int dia = 1, int hora = 1200)
        => new(dia, hora, "Forest", 0, 0, 1, "soleado", Array.Empty<EntidadObservada>());

    private static IReadOnlyList<Disparo> Paso(Disparadores motor, Observacion escena, long ahora)
        => motor.Observar(escena, Array.Empty<EntidadEnBanda>(), null, ahora, 0);

    [Fact]
    public void Compromisos_gastan_primero_ordinario_luego_extra_y_despues_piden_prorroga()
    {
        var motor = new Disparadores(Perfil());
        var escena = Escena();
        var prorrogas = new List<Disparo>();
        motor.CompromisoSinCupo += prorrogas.Add;

        motor.Compromiso("primero", escena, 100);
        Assert.Equal("primero", Assert.Single(Paso(motor, escena, 100)).Ref);
        Assert.Equal(new ContadoresPulsos(1, OrdinariosLibresGastados: 1), motor.Contadores);

        motor.Compromiso("segundo", escena, 200);
        motor.Compromiso("tercero", escena, 200);
        Assert.Equal("segundo", Assert.Single(Paso(motor, escena, 200)).Ref);
        Assert.Equal("tercero", Assert.Single(prorrogas).Ref);

        Assert.Equal(Motivos.Interlocucion, motor.Interlocucion("farmer:1", escena, 300).Motivo);
        Assert.Equal(new ContadoresPulsos(1, 1, 1, Interlocuciones: 1), motor.Contadores);
    }

    [Fact]
    public void Una_deriva_y_un_hito_denegados_no_gastan_reservas_ni_reinician_la_atencion()
    {
        var motor = new Disparadores(Perfil());
        var escena = Escena();
        motor.FinIntencion("tarde", "cumplida", escena, 100);
        Assert.Single(Paso(motor, escena, 100));

        // Incluso un plan de la mente llamado «acostarse» es un hito de intención ordinario.
        motor.IniciarPlanIntencion(HitosRutina.Acostarse, 200);
        motor.TerminarPlanIntencion(HitosRutina.Acostarse, escena, 60_000);
        var bandas = new[] { new EntidadEnBanda("luz", Banda.Mencion, 0.6) };
        Assert.Empty(motor.Observar(escena, bandas, null, 60_000, 15));
        Assert.True(motor.ValorIntegrador >= 3);
        Assert.Equal(100, motor.UltimoPulsoMs);
        Assert.Equal(new ContadoresPulsos(1, OrdinariosLibresGastados: 1), motor.Contadores);

        Assert.Equal(HitosRutina.Acostarse, motor.FinDeDia(escena, 60_001)!.Hito!.Id);
        Assert.Null(motor.FinDeDia(escena, 60_002));
        Assert.Equal(0, motor.ValorIntegrador);
    }

    [Theory]
    [InlineData(ModoReloj.Vanilla, 1, 1)]
    [InlineData(ModoReloj.Pelican, 2, 3)]
    public void El_bucle_elige_el_cupo_del_modo_del_reloj(ModoReloj modo, int ordinarios, int extras)
    {
        var perfil = Perfil();
        perfil.Presupuesto.Pelican.PulsosPorDiaJuego = 4;
        perfil.Presupuesto.Pelican.ExtraCompromisos = 3;
        var mundo = Escenario.Pasarela().EnModo(modo).Construir();
        var bucle = new BucleAtencion(mundo, perfil);
        for (var i = 0; i < 8; i++)
        {
            bucle.Disparadores.Compromiso($"compromiso-{i}", mundo.Observar(), 0);
        }

        bucle.Avanzar(TimeSpan.FromMilliseconds(FakeWorld.PasoMs));
        Assert.Equal(ordinarios + extras, bucle.Disparos.Count);
        Assert.Equal(ordinarios, bucle.Disparadores.Contadores!.OrdinariosLibresGastados);
        Assert.Equal(extras, bucle.Disparadores.Contadores.ExtrasCompromisosGastados);
    }

    [Fact]
    public void Guardar_y_cargar_conserva_plazas_y_cadenas_y_otro_personaje_tiene_su_propio_saldo()
    {
        var perfil = Perfil();
        var motor = new Disparadores(perfil);
        var dia = Escena(hora: 600);
        Assert.Equal(HitosRutina.Despertar, Assert.Single(Paso(motor, dia, 0)).Hito!.Id);
        motor.Compromiso("a", dia, 1);
        motor.Compromiso("b", dia, 1);
        Assert.Equal(2, Paso(motor, dia, 1).Count);
        Assert.NotNull(motor.FinDeDia(dia with { Hora = 2300 }, 2));

        var json = System.Text.Json.JsonSerializer.Serialize(motor.Contadores);
        var estado = System.Text.Json.JsonSerializer.Deserialize<ContadoresPulsos>(json);
        var recargado = new Disparadores(perfil, guardados: estado);
        Assert.Empty(Paso(recargado, dia, 0));
        Assert.Null(recargado.FinDeDia(dia, 1));
        recargado.Compromiso("c", dia, 2);
        Assert.Empty(Paso(recargado, dia, 2));
        Assert.Equal(motor.Contadores, recargado.Contadores);

        var otro = new Disparadores(perfil);
        otro.Compromiso("c", Escena(), 2);
        Assert.Equal("c", Assert.Single(Paso(otro, Escena(), 2)).Ref);

        var manana = dia with { Dia = 2 };
        Assert.Equal(HitosRutina.Despertar, Assert.Single(Paso(recargado, manana, 3)).Hito!.Id);
        Assert.Equal(new ContadoresPulsos(2, DespertarEmitido: true), recargado.Contadores);
        recargado.Compromiso("c", manana, 4);
        Assert.Equal("c", Assert.Single(Paso(recargado, manana, 4)).Ref);
    }

    [Fact]
    public void Un_compromiso_pendiente_de_ayer_no_descuenta_una_plaza_de_hoy()
    {
        var motor = new Disparadores(Perfil());
        var ayer = Escena();
        var hoy = Escena(2, 600);
        var prorrogas = new List<Disparo>();
        motor.CompromisoSinCupo += prorrogas.Add;
        motor.Compromiso("a", ayer, 10);
        motor.Compromiso("b", ayer, 10);
        Assert.Equal(2, Paso(motor, ayer, 10).Count);
        motor.Compromiso("tardio", ayer, 20);
        motor.Interlocucion("farmer:1", hoy, 30);

        var emitidos = Paso(motor, hoy, 31);
        Assert.Equal(new[] { HitosRutina.Acostarse, HitosRutina.Despertar }, emitidos.Select(d => d.Hito!.Id));
        Assert.Equal("tardio", Assert.Single(prorrogas).Ref);
        Assert.Equal(new ContadoresPulsos(2, DespertarEmitido: true, Interlocuciones: 1), motor.Contadores);
    }

    [Fact]
    public void El_perfil_anterior_que_omite_la_bolsa_tiene_una_plaza_y_cero_es_configurable()
    {
        var ruta = Path.Combine(Esquemas.Ejemplos("validos"), "perfil--ejemplo.json");
        var documento = Esquemas.Leer(ruta);
        var modo = documento["presupuesto"]!["vanilla"]!.AsObject();
        modo.Remove("extra_compromisos");
        Assert.True(Esquemas.EsValido("perfil", documento));
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(documento.ToJsonString())!;
        Assert.Equal(1, perfil.Presupuesto.Vanilla.ExtraCompromisos);

        modo["extra_compromisos"] = -1;
        Assert.False(Esquemas.EsValido("perfil", documento));
        perfil.Presupuesto.Vanilla.ExtraCompromisos = -1;
        Assert.Contains(PerfilValidator.Validar(perfil, ModoValidacion.Desarrollo).Errores,
            e => e.Campo == "presupuesto.vanilla.extra_compromisos");

        perfil.Presupuesto.Vanilla.PulsosPorDiaJuego = 3;
        perfil.Presupuesto.Vanilla.ExtraCompromisos = 0;
        var motor = new Disparadores(perfil);
        motor.Compromiso("a", Escena(), 1);
        motor.Compromiso("b", Escena(), 1);
        Assert.Equal("a", Assert.Single(Paso(motor, Escena(), 1)).Ref);
        Assert.Equal(0, motor.Contadores!.ExtrasCompromisosGastados);
    }

    [Theory]
    [InlineData("presupuesto")]
    [InlineData("pulsos_por_dia_juego")]
    [InlineData("reservados_cadenas")]
    [InlineData("extra_compromisos")]
    public void El_esquema_rechaza_campos_de_presupuesto_en_la_respuesta(string campo)
    {
        var respuesta = Esquemas.Leer(Path.Combine(Esquemas.Ejemplos("validos"), "respuesta--despertar.json"));
        Assert.True(Esquemas.EsValido("respuesta", respuesta));
        respuesta[campo] = 100;
        Assert.False(Esquemas.EsValido("respuesta", respuesta));
        respuesta.AsObject().Remove(campo);
        respuesta["intencion"]![campo] = 100;
        Assert.False(Esquemas.EsValido("respuesta", respuesta));
    }
}
