using CorazonPelican.Core.Dialogo;

namespace CorazonPelican.Tests.Dialogo;

public class ConvivenciaValleyTalkTests
{
    private static readonly ModInstaladoResumen ValleyTalkOficial =
        new("dandm1.ValleyTalk", "ValleyTalk", "dandm1");

    [Fact]
    public void Con_ValleyTalk_presente_y_Leah_no_listada_cede_a_vanilla_y_no_intercepta()
    {
        var mods = new[]
        {
            new ModInstaladoResumen("Pathoschild.LookupAnything", "Lookup Anything", "Pathoschild"),
            ValleyTalkOficial,
        };

        var ceder = ConvivenciaValleyTalk.DebeCederAVanilla(
            personajeId: "leah",
            npcName: "Leah",
            modsCargados: mods,
            valleyTalkDesactivadoPara: Array.Empty<string>());

        Assert.True(ceder);

        var decision = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
            MenteHabilitada: true,
            MenteCaida: false,
            EnEvento: false,
            ValleyTalkActivo: ceder));

        Assert.False(decision.Interceptar);
        Assert.Equal("valleytalk_activo", decision.Motivo);
    }

    [Fact]
    public void Con_ValleyTalk_presente_y_Leah_en_ValleyTalkDesactivadoPara_permite_interceptar()
    {
        var mods = new[] { ValleyTalkOficial };

        Assert.False(ConvivenciaValleyTalk.DebeCederAVanilla("leah", "Leah", mods, new[] { "leah" }));
        Assert.False(ConvivenciaValleyTalk.DebeCederAVanilla("leah", "Leah", mods, new[] { "Leah" }));

        var decision = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
            MenteHabilitada: true,
            MenteCaida: false,
            EnEvento: false,
            ValleyTalkActivo: false));

        Assert.True(decision.Interceptar);
    }

    [Fact]
    public void Detecta_ValleyTalk_por_nombre_o_autor_aunque_cambie_el_UniqueID()
    {
        var porNombre = new[] { new ModInstaladoResumen("OtroAutor.ForkVT", "ValleyTalk Custom", "otro") };
        var porAutor = new[] { new ModInstaladoResumen("dandm1.Algo", "Valley Dialogues", "dandm1") };

        Assert.True(ConvivenciaValleyTalk.DebeCederAVanilla("leah", "Leah", porNombre, null));
        Assert.True(ConvivenciaValleyTalk.DebeCederAVanilla("leah", "Leah", porAutor, null));
    }

    [Fact]
    public void Sin_ValleyTalk_cargado_no_cede_y_el_aviso_consola_incluye_clave_config()
    {
        var mods = new[] { new ModInstaladoResumen("Dr4co.Pelican4Ever", "Pelican 4Ever", "Dr4co") };
        Assert.False(ConvivenciaValleyTalk.DebeCederAVanilla("leah", "Leah", mods, null));

        var aviso = ConvivenciaValleyTalk.ConstruirAvisoConsola("leah", "Leah", ValleyTalkOficial.UniqueId);
        Assert.Contains("dandm1.ValleyTalk", aviso);
        Assert.Contains("ValleyTalkDesactivadoPara", aviso);
        Assert.Contains("Leah", aviso);
    }
}
