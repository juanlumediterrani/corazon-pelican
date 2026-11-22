using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

/// <summary>Pruebas de protocolo multijugador para diálogo y cartas de prórroga en farmhands (CP-606).</summary>
public class MultijugadorDialogoProrrogaTests
{
    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void ProtocoloMultijugador_valida_identidades_solicitudes_y_cartas_prorroga()
    {
        Assert.Equal("farmer:42001", ProtocoloMultijugador.ConstruirReferenciaGranjero(42001));
        Assert.True(ProtocoloMultijugador.EsPeticionDialogoValida(new DialogoSolicitudRemota { NpcName = "Leah", FarmerName = "Invitado" }));
        Assert.False(ProtocoloMultijugador.EsPeticionDialogoValida(new DialogoSolicitudRemota { NpcName = " " }));
        Assert.True(ProtocoloMultijugador.EsCartaProrroga($"{GestorProrrogaCartas.PrefijoCartaId}comp-1"));
        Assert.False(ProtocoloMultijugador.EsCartaProrroga("Robin_Mail"));
    }

    [Fact]
    public void Turno_de_dialogo_y_prorroga_remotos_funcionan_para_farmhand_distinto_al_host()
    {
        var perfil = CargarPerfilLeah();
        perfil.Presupuesto.Pelican.PulsosPorDiaJuego = 2; // solo reserva cadenas -> cupos ordinarios/extra agotados
        perfil.Presupuesto.Pelican.ExtraCompromisos = 0;
        var estado = new EstadoPersonaje();
        var pulsos = new List<Pulso>();
        var motor = new MotorHabitante(
            perfil,
            CargarPlantillasLeah(),
            estado,
            ModoReloj.Pelican,
            publicarPulso: p =>
            {
                p.Id = pulsos.Count + 1;
                pulsos.Add(p);
                return p;
            })
        {
            Epoca = "ep-mp-1",
            MenteHabilitada = true,
        };

        var farmhandRef = ProtocoloMultijugador.ConstruirReferenciaGranjero(999001);
        var obs = new Observacion(
            1,
            1100,
            "LeahHouse",
            8,
            5,
            1.0,
            "soleado",
            new[]
            {
                new EntidadObservada(farmhandRef, CategoriaEntidad.Granjero, "Invitado", "LeahHouse", 8, 6, new[] { "persona", "granjero" }, Corazones: 4, Visible: true, Distancia: 1.0),
            });

        var reaccion = motor.IniciarTurnoDialogo(obs, farmhandRef, "Invitado", ahoraMs: 1000, reaccionMs: 5, enEvento: false, valleyTalkActivo: false);
        Assert.NotNull(reaccion.Pulso);
        Assert.Equal(farmhandRef, reaccion.Pulso!.Interlocutor!.Ref);

        DesenlaceDialogoTurno? desenlace = null;
        motor.ResultadoDialogoDisponible += r => desenlace = r;

        var res = motor.AplicarRespuesta(
            reaccion.Pulso,
            new Respuesta
            {
                Epoca = "ep-mp-1",
                BasadaEn = reaccion.Pulso.Id,
                Pensamiento = "Invitado ha pasado a saludar.",
                Frase = "Hola Invitado, pasa sin problema.",
                Actos = new List<Acto>
                {
                    new() { Tipo = TiposActo.Cita, Para = farmhandRef, Lugar = "tu_casa", Cuando = new TiempoJuego { Dia = 2, Hora = 1200 } },
                },
            });

        Assert.Equal("aceptada", res.Estado);
        Assert.NotNull(desenlace);
        Assert.Equal("Hola Invitado, pasa sin problema.", desenlace!.Frase);
        Assert.Single(estado.Compromisos);

        // Con cupos agotados y farmhand ausente cerca del plazo, ProcesarPaso genera carta exclusiva para el farmhand.
        var comp = Assert.Single(estado.Compromisos);
        var obsSinFarmhand = new Observacion(2, 900, "LeahHouse", 8, 5, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obsSinFarmhand, horario: null, ahoraMs: 2000, minutosJuego: 10);

        Assert.NotNull(comp.Prorroga);
        var cartas = GestorProrrogaCartas.PrepararCartasPendientes(estado, perfil, Array.Empty<string>());
        var carta = Assert.Single(cartas);
        Assert.Equal(farmhandRef, carta.Destinatario);

        var rechazoOtro = GestorProrrogaCartas.ResponderCarta(
            estado,
            carta.CartaId,
            ProtocoloMultijugador.ConstruirReferenciaGranjero(1),
            aceptar: true,
            new TiempoJuego { Dia = 1, Hora = 1200 });
        Assert.False(rechazoOtro.Aplicada);

        var aceptada = GestorProrrogaCartas.ResponderCarta(
            estado,
            carta.CartaId,
            farmhandRef,
            aceptar: true,
            new TiempoJuego { Dia = 1, Hora = 1200 });
        Assert.True(aceptada.Aplicada);
    }
}
