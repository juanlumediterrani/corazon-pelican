using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Perfil;

public class LexicoUnicoTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    [Fact]
    public void El_apendice_ir_a_y_cita_comparten_los_nombres_del_perfil_cargado()
    {
        var perfil = CargarPerfil();
        var lugarDelPack = new Lugar { Id = "rincon", Ubicacion = "Town", Area = new() { 1, 2, 3, 4 }, Prosa = "el rincón junto al puente" };
        perfil.Lexico.Lugares.Add(lugarDelPack);
        Assert.True(PerfilValidator.Validar(perfil, ModoValidacion.Desarrollo).EsValido);
        var mundo = Escenario.Pasarela().Construir(perfil.Lexico);
        var pulso = new Pulso { Lugares = mundo.LugaresOfrecidos.CrearApendice() };

        Assert.Equal(perfil.Lexico.Lugares.Select(lugar => lugar.Id), pulso.Lugares.Select(par => par.Id));
        foreach (var par in pulso.Lugares)
        {
            var paso = new PasoPlan { Accion = "ir_a", Lugar = par.Id };
            var cita = new Acto { Tipo = "cita", Lugar = par.Id };
            var destino = Assert.IsType<Lugar>(mundo.LugaresOfrecidos.DestinoDe(paso));
            Assert.Same(destino, mundo.LugaresOfrecidos.DestinoDe(cita));
            Assert.Equal(par.Prosa, destino.Prosa);
        }

        mundo.Cuerpo.Ejecutar(new[] { new PasoPlan { Accion = "ir_a", Lugar = "rincon" } });
        Assert.Equal((lugarDelPack.Ubicacion, 1, 2), (mundo.Personaje.Ubicacion, mundo.Personaje.X, mundo.Personaje.Y));
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("Playa")]
    [InlineData("la playa, junto a las rocas")]
    [InlineData(null)]
    public void Un_nombre_ajeno_al_apendice_tampoco_resuelve_un_destino(string? id)
    {
        var ofrecidos = CargarPerfil().Lexico.OfrecerLugares(_ => true);

        Assert.DoesNotContain(ofrecidos.CrearApendice(), par => par.Id == id);
        Assert.Null(ofrecidos.DestinoDe(new PasoPlan { Accion = "ir_a", Lugar = id }));
        Assert.Null(ofrecidos.DestinoDe(new Acto { Tipo = "cita", Lugar = id }));
    }

    [Fact]
    public void El_pulso_solo_ofrece_los_lugares_elegidos_con_su_prosa_exacta()
    {
        var lexico = CargarPerfil().Lexico;
        var playa = lexico.Lugares.Single(lugar => lugar.Id == "playa");
        var ofrecidos = lexico.OfrecerLugares(lugar => lugar.Ubicacion == "Beach");
        var par = Assert.Single(ofrecidos.CrearApendice());

        Assert.Equal((playa.Id, playa.Prosa), (par.Id, par.Prosa));
        foreach (var lugar in lexico.Lugares)
        {
            var destino = lugar == playa ? playa : null;
            Assert.Same(destino, ofrecidos.DestinoDe(new PasoPlan { Accion = "ir_a", Lugar = lugar.Id }));
            Assert.Same(destino, ofrecidos.DestinoDe(new Acto { Tipo = "cita", Lugar = lugar.Id }));
        }

        Assert.Null(ofrecidos.DestinoDe(new PasoPlan { Accion = "mirar", Lugar = playa.Id }));
        Assert.Null(ofrecidos.DestinoDe(new Acto { Tipo = "rechaza", Lugar = playa.Id }));
    }

    [Theory]
    [InlineData("Playa")]
    [InlineData("mi playa")]
    [InlineData("playa ")]
    [InlineData("playa\n")]
    public void El_validador_rechaza_ids_con_mayusculas_o_espacios(string id)
    {
        var perfil = CargarPerfil();
        perfil.Lexico.Lugares[0].Id = id;

        var resultado = PerfilValidator.Validar(perfil, ModoValidacion.Desarrollo);

        Assert.Contains(resultado.Errores, e => e.Campo == "lexico.lugares[0].id");
    }
}
