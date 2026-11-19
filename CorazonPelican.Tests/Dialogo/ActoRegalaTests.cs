using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

/// <summary>
/// Guarda de R-08: <c>regala</c> solo acepta obras existentes y disponibles (<c>terminada</c> o
/// <c>colgada</c>), con destinatario presente en la escena y apertura suficiente.
/// </summary>
public class ActoRegalaTests
{
    private static PerfilPersonaje CargarPerfil()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static Observacion EscenaConDestinatario(int corazones = 9, bool presente = true)
    {
        var entidades = presente
            ? new[]
            {
                new EntidadObservada("farmer:1", CategoriaEntidad.Granjero, "Ana", "Mountain", 40, 29, new[] { "persona" }, Corazones: corazones),
            }
            : Array.Empty<EntidadObservada>();

        return new Observacion(68, 1700, "Mountain", 40, 30, 1.0, "soleado", entidades);
    }

    private static ContextoValidacionActos ContextoPara(
        EstadoPersonaje estado,
        int corazones = 9,
        bool destinatarioPresente = true)
    {
        var perfil = CargarPerfil();
        return new ContextoValidacionActos(
            EscenaConDestinatario(corazones, destinatarioPresente),
            perfil,
            perfil.Lexico.OfrecerLugares(_ => true),
            estado,
            InterlocutorRef: "farmer:1",
            CorazonesInterlocutor: corazones);
    }

    private static MotivoRechazo AfirmarMotivo(
        ResultadoTurnoDialogo resultado,
        string codigoEsperado,
        string campoEsperado)
    {
        Assert.False(resultado.Aceptado);
        Assert.Null(resultado.FraseVisible);
        var motivo = Assert.Single(resultado.Motivos, m => m.Codigo == codigoEsperado && m.Campo == campoEsperado);
        Assert.False(string.IsNullOrWhiteSpace(motivo.Prosa));
        return motivo;
    }

    [Fact]
    public void Obra_inexistente_se_rechaza_con_motivo_y_bloquea_la_frase()
    {
        var estado = new EstadoPersonaje();
        var respuesta = new Respuesta
        {
            Frase = "Quiero regalarte este cuadro.",
            Actos = new() { new Acto { Tipo = TiposActo.Regala, ObraId = "leah-9999", Para = "farmer:1" } },
        };

        var resultado = AplicadorTurnoDialogo.Aplicar(respuesta, ContextoPara(estado));

        AfirmarMotivo(resultado, "obra_inexistente", "actos[0].obra_id");
    }

    [Theory]
    [InlineData(EstadosObra.Regalada, "obra_ya_regalada")]
    [InlineData(EstadosObra.EnCurso, "obra_no_regalable")]
    [InlineData(EstadosObra.Boceto, "obra_no_regalable")]
    public void Obra_regalada_en_curso_o_boceto_se_rechaza_con_motivo(string estadoObra, string codigoEsperado)
    {
        var estado = new EstadoPersonaje();
        estado.Portafolio.Add(new ObraPortafolio
        {
            ObraId = "leah-0007",
            Estado = estadoObra,
            Destinatario = estadoObra == EstadosObra.Regalada ? "farmer:2" : null,
        });

        var respuesta = new Respuesta
        {
            Frase = "Quédate con esta obra.",
            Actos = new() { new Acto { Tipo = TiposActo.Regala, ObraId = "leah-0007", Para = "farmer:1" } },
        };

        var resultado = AplicadorTurnoDialogo.Aplicar(respuesta, ContextoPara(estado));

        AfirmarMotivo(resultado, codigoEsperado, "actos[0].obra_id");
        Assert.Equal(estadoObra, estado.Portafolio.Single().Estado);
    }

    [Fact]
    public void Regala_sin_apertura_suficiente_o_con_destinatario_ausente_se_rechaza()
    {
        var estado = new EstadoPersonaje();
        estado.Portafolio.Add(new ObraPortafolio { ObraId = "leah-0010", Estado = EstadosObra.Terminada });

        var respuesta = new Respuesta
        {
            Frase = "Ten, es para ti.",
            Actos = new() { new Acto { Tipo = TiposActo.Regala, ObraId = "leah-0010", Para = "farmer:1" } },
        };

        // Con 6 corazones, Regalar == false en leah.json.
        var resSinApertura = AplicadorTurnoDialogo.Aplicar(respuesta, ContextoPara(estado, corazones: 6));
        AfirmarMotivo(resSinApertura, "apertura_insuficiente", "actos[0]");
        Assert.Equal(EstadosObra.Terminada, estado.Portafolio.Single().Estado);

        // Con 9 corazones pero destinatario ausente de la escena.
        var resAusente = AplicadorTurnoDialogo.Aplicar(respuesta, ContextoPara(estado, corazones: 9, destinatarioPresente: false));
        AfirmarMotivo(resAusente, "destinatario_ausente", "actos[0].para");
        Assert.Equal(EstadosObra.Terminada, estado.Portafolio.Single().Estado);
    }

    [Theory]
    [InlineData(EstadosObra.Terminada)]
    [InlineData(EstadosObra.Colgada)]
    public void Obra_disponible_con_apertura_y_destinatario_presente_se_regala_y_muestra_la_frase(string estadoInicial)
    {
        var estado = new EstadoPersonaje();
        estado.Portafolio.Add(new ObraPortafolio
        {
            ObraId = "leah-0012",
            Estado = estadoInicial,
        });

        var respuesta = new Respuesta
        {
            Frase = "Llevaba días queriendo dártelo. Es tuyo.",
            Actos = new() { new Acto { Tipo = TiposActo.Regala, ObraId = "leah-0012", Para = "farmer:1" } },
        };

        var resultado = AplicadorTurnoDialogo.Aplicar(respuesta, ContextoPara(estado, corazones: 9));

        Assert.True(resultado.Aceptado);
        Assert.Equal(Resultado.Aceptada, resultado.AResultadoContrato().Estado);
        Assert.Equal(respuesta.Frase, resultado.FraseVisible);

        var obra = estado.Portafolio.Single();
        Assert.Equal(EstadosObra.Regalada, obra.Estado);
        Assert.Equal("farmer:1", obra.Destinatario);
        Assert.Single(resultado.ObrasRegaladas, obra);
    }

    [Fact]
    public void Regalar_dos_veces_la_misma_obra_en_un_lote_rechaza_todo_el_lote()
    {
        var estado = new EstadoPersonaje();
        estado.Portafolio.Add(new ObraPortafolio { ObraId = "leah-0015", Estado = EstadosObra.Terminada });

        var respuesta = new Respuesta
        {
            Frase = "Toma este cuadro dos veces.",
            Actos = new()
            {
                new Acto { Tipo = TiposActo.Regala, ObraId = "leah-0015", Para = "farmer:1" },
                new Acto { Tipo = TiposActo.Regala, ObraId = "leah-0015", Para = "farmer:1" },
            },
        };

        var resultado = AplicadorTurnoDialogo.Aplicar(respuesta, ContextoPara(estado, corazones: 9));

        AfirmarMotivo(resultado, "obra_ya_regalada", "actos[1].obra_id");
        Assert.Equal(EstadosObra.Terminada, estado.Portafolio.Single().Estado);
        Assert.Null(estado.Portafolio.Single().Destinatario);
    }
}
