using System.Text.Json;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Tests.Contrato;

public class OrigenDiarioTests
{
    private static T Leer<T>(string fichero) => JsonSerializer.Deserialize<T>(
        File.ReadAllText(Path.Combine(Esquemas.Ejemplos("validos"), fichero)))!;

    private readonly Pulso pulso = Leer<Pulso>("pulso--diario-con-origen.json");
    private readonly Respuesta respuesta = Leer<Respuesta>("respuesta--reflexion-con-origen.json");

    [Fact]
    public void Acepta_origenes_del_pulso_y_rechaza_la_reflexion_si_uno_es_ajeno()
    {
        Assert.Null(ValidadorOrigenDiario.Comprobar(this.pulso, this.respuesta));
        this.respuesta.Memoria!.Reflexiones![0].OrigenDiario[1] = "episodio-de-otro-personaje";

        var motivo = ValidadorOrigenDiario.Comprobar(this.pulso, this.respuesta);
        Assert.Equal("episodio_desconocido", motivo!.Codigo);
        Assert.Equal("memoria.reflexiones[0].origen_diario[1]", motivo.Campo);
    }

    [Theory]
    [InlineData("otro-mundo", 52, "epoca_obsoleta")]
    [InlineData("primavera", 51, "pulso_distinto")]
    public void Comprueba_el_contexto_aunque_coincidan_los_ids(string epoca, long pulsoId, string codigo)
    {
        this.respuesta.Epoca = epoca;
        this.respuesta.BasadaEn = pulsoId;
        Assert.Equal(codigo, ValidadorOrigenDiario.Comprobar(this.pulso, this.respuesta)!.Codigo);
    }

    [Fact]
    public void Pide_origen_solo_si_el_pulso_ofrece_episodios_identificados()
    {
        this.respuesta.Memoria = new MemoriaPropuesta { Recordar = new() { "Un recuerdo en prosa" } };
        Assert.Equal("origen_ausente", ValidadorOrigenDiario.Comprobar(this.pulso, this.respuesta)!.Codigo);
        this.pulso.EpisodiosDiario = null;
        this.pulso.DiarioDia = new() { "Una tarde en casa" };
        Assert.Null(ValidadorOrigenDiario.Comprobar(this.pulso, this.respuesta));
    }
}
