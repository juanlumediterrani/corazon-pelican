using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Tests.Simulacion;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Dialogo;

/// <summary>
/// Guardas de prórroga por carta ante cupos agotados y destinatario ausente, validación en vivo y persistencia (R-09, CP-509).
/// </summary>
public class ProrrogaCompromisoTests
{
    private static PerfilPersonaje CargarPerfilConCupo(int pulsosTotalesDia, int extraCompromisos)
    {
        var perfil = JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;
        perfil.Presupuesto.Vanilla.PulsosPorDiaJuego = pulsosTotalesDia; // 2 reservados para cadenas.
        perfil.Presupuesto.Vanilla.ExtraCompromisos = extraCompromisos;
        return perfil;
    }

    private static PlantillasProsa CargarPlantillas()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    private static CompromisoEstado CrearCompromiso(
        string id = "comp-carta-1",
        string destinatario = "farmer:1",
        string objeto = "boceto del embarcadero",
        int dia = 68,
        int hora = 1900)
        => new()
        {
            Id = id,
            Tipo = TiposActo.Promete,
            Destinatario = destinatario,
            Objeto = objeto,
            PlazoVigente = new TiempoJuego { Dia = dia, Hora = hora },
            Estado = EstadosCompromiso.Pendiente,
        };

    private static FakeWorld CrearMundoDestinatarioAusente(int dia = 68, int hora = 1600)
        => Escenario.Pasarela()
            .ALas(dia, hora)
            // La observación de FakeWorld solo devuelve entidades presentes en la ubicación actual;
            // situando al destinatario fuera ("Farm") queda ausente ante el personaje ("Mountain").
            .ConNpc("Sam", "Town", 10, 10, deambula: false)
            .Construir();

    [Fact]
    public void Cupos_agotados_con_destinatario_ausente_prepara_una_sola_carta_sin_pulso_y_conserva_plazo_y_cadenas()
    {
        // A las 09:00 faltan 10h para el plazo (saliencia < 0.60): aún no se dispara comp-1.
        var mundo = CrearMundoDestinatarioAusente(dia: 68, hora: 900);
        var perfil = CargarPerfilConCupo(pulsosTotalesDia: 3, extraCompromisos: 1); // 1 ordinario libre + 1 extra
        var estado = new EstadoPersonaje();
        var comp = CrearCompromiso("comp-1", destinatario: "farmer:1", dia: 68, hora: 1900);
        estado.Compromisos.Add(comp);

        var motor = new MotorHabitante(perfil, CargarPlantillas(), estado, ModoReloj.Vanilla);

        // Gastamos la única plaza ordinaria libre y la plaza extra con pulsos previos.
        Assert.True(motor.ForzarPulso(mundo.Observar(), ahoraMs: 100).Emitido);
        motor.Disparadores.Compromiso("comp-previo", mundo.Observar(), ahoraMs: 200);
        var pasoExtra = motor.ProcesarPaso(mundo.Observar(), horario: null, ahoraMs: 200, minutosJuego: 10);
        Assert.Contains(pasoExtra.Pulsos, p => p.Motivo == Motivos.Compromiso);
        Assert.Equal(1, motor.Estado.Contadores!.OrdinariosLibresGastados);
        Assert.Equal(1, motor.Estado.Contadores.ExtrasCompromisosGastados);

        // Avanzamos 6 horas de juego en Vanilla (6 * 42s -> 15:00): comp-1 supera el umbral de saliencia
        // con ambos cupos agotados y el destinatario ausente -> prepara UNA carta sin emitir otro pulso.
        mundo.Avanzar(TimeSpan.FromSeconds(6 * 42));
        var obsSinDest = mundo.Observar();
        Assert.Equal(1500, obsSinDest.Hora);
        var resSinCupo = motor.ProcesarPaso(obsSinDest, horario: null, ahoraMs: mundo.Reloj.MilisegundosReales, minutosJuego: 10);

        Assert.DoesNotContain(resSinCupo.Pulsos, p => p.Motivo == Motivos.Compromiso);
        Assert.NotNull(comp.Prorroga);
        Assert.Equal(EstadosProrroga.Pendiente, comp.Prorroga!.Estado);
        Assert.False(string.IsNullOrWhiteSpace(comp.Prorroga.CartaId));
        Assert.Equal(68, comp.PlazoVigente.Dia);
        Assert.Equal(1900, comp.PlazoVigente.Hora);
        Assert.Equal(70, comp.Prorroga.PlazoPropuesto.Dia);

        // Las reservas de despertar y acostarse siguen intactas; el acostarse se sigue admitiendo al cierre del día.
        Assert.False(motor.Estado.Contadores!.DespertarEmitido);
        Assert.False(motor.Estado.Contadores.AcostarseEmitido);

        // Repetir pasos mientras sigue ausente conserva exactamente esa misma única carta sin emitir pulsos ni cambiar plazo.
        var cartaIdInicial = comp.Prorroga.CartaId;
        var segundaPreparacion = GestorProrrogaCartas.PrepararCartasPendientes(
            estado,
            perfil,
            obsSinDest.Entidades.Select(e => e.Ref));
        var carta = Assert.Single(segundaPreparacion);
        Assert.Equal(cartaIdInicial, carta.CartaId);
        Assert.Equal("farmer:1", carta.Destinatario);
        Assert.Contains(comp.Objeto, carta.CuerpoTexto);
        Assert.Contains("día 68, 19:00", carta.CuerpoTexto);
        Assert.Contains("día 70, 19:00", carta.CuerpoTexto);

        // Además comprobamos que la reserva de fin de día sigue libre para acostarse.
        var cierreDia = motor.ProcesarFinDeDia(obsSinDest, ahoraMs: mundo.Reloj.MilisegundosReales + 100);
        Assert.NotNull(cierreDia);
        Assert.True(motor.Estado.Contadores.AcostarseEmitido);
    }

    [Fact]
    public void Antes_del_vencimiento_aceptar_aplica_una_sola_vez_rechazar_mantiene_plazo_y_otro_jugador_no_autorizado()
    {
        var perfil = CargarPerfilConCupo(pulsosTotalesDia: 3, extraCompromisos: 0);
        var estado = new EstadoPersonaje();
        var compAceptado = CrearCompromiso("comp-acep", "farmer:1", "talla de madera", dia: 68, hora: 1900);
        var compRechazado = CrearCompromiso("comp-rech", "farmer:1", "cuadro del río", dia: 69, hora: 2000);
        estado.Compromisos.Add(compAceptado);
        estado.Compromisos.Add(compRechazado);

        var tCreacion = new TiempoJuego { Dia = 68, Hora = 1600 };
        GestorProrrogaCartas.PrepararCartasPendientes(
            estado,
            perfil,
            presentesEnEscena: Array.Empty<string>()); // Aún no hay peticiones registradas
        GestorCompromisos.RegistrarPeticionProrroga(estado, compAceptado.Id, tCreacion);
        GestorCompromisos.RegistrarPeticionProrroga(estado, compRechazado.Id, tCreacion);

        var cartas = GestorProrrogaCartas.PrepararCartasPendientes(estado, perfil, Array.Empty<string>());
        Assert.Equal(2, cartas.Count);

        var cartaAcepId = compAceptado.Prorroga!.CartaId!;
        var cartaRechId = compRechazado.Prorroga!.CartaId!;
        var ahoraAntesPlazo = new TiempoJuego { Dia = 68, Hora = 1700 };

        // Un segundo jugador no puede responder ni con carta válida.
        var vistaSegundo = GestorProrrogaCartas.AbrirCarta(estado, perfil, cartaAcepId, "farmer:2", ahoraAntesPlazo);
        Assert.NotNull(vistaSegundo);
        Assert.False(vistaSegundo!.PuedeResponder);

        var intentoSegundo = GestorProrrogaCartas.ResponderCarta(
            estado,
            cartaAcepId,
            "farmer:2",
            aceptar: true,
            ahoraAntesPlazo);
        Assert.False(intentoSegundo.Aplicada);
        Assert.Equal("no_autorizado", intentoSegundo.Codigo);
        Assert.Equal(EstadosProrroga.Pendiente, compAceptado.Prorroga.Estado);
        Assert.Equal(68, compAceptado.PlazoVigente.Dia);

        // El destinatario acepta antes del vencimiento -> aplica una sola vez.
        var vistaDest = GestorProrrogaCartas.AbrirCarta(estado, perfil, cartaAcepId, "farmer:1", ahoraAntesPlazo);
        Assert.NotNull(vistaDest);
        Assert.True(vistaDest!.PuedeResponder);

        var resAcep = GestorProrrogaCartas.ResponderCarta(
            estado,
            cartaAcepId,
            "farmer:1",
            aceptar: true,
            ahoraAntesPlazo);
        Assert.True(resAcep.Aplicada);
        Assert.Equal("prorroga_aceptada", resAcep.Codigo);
        Assert.Equal(EstadosProrroga.Aceptada, compAceptado.Prorroga.Estado);
        Assert.Equal(70, compAceptado.PlazoVigente.Dia);
        Assert.Equal(1900, compAceptado.PlazoVigente.Hora);

        // Repetir la aceptación no vuelve a desplazar el plazo.
        var resRepetida = GestorProrrogaCartas.ResponderCarta(
            estado,
            cartaAcepId,
            "farmer:1",
            aceptar: true,
            new TiempoJuego { Dia = 68, Hora = 1800 });
        Assert.False(resRepetida.Aplicada);
        Assert.Equal("ya_resuelta", resRepetida.Codigo);
        Assert.Equal(70, compAceptado.PlazoVigente.Dia);

        // Rechazar mantiene el plazo original intacto.
        var resRech = GestorProrrogaCartas.ResponderCarta(
            estado,
            cartaRechId,
            "farmer:1",
            aceptar: false,
            ahoraAntesPlazo);
        Assert.True(resRech.Aplicada);
        Assert.Equal("prorroga_rechazada", resRech.Codigo);
        Assert.Equal(EstadosProrroga.Rechazada, compRechazado.Prorroga.Estado);
        Assert.Equal(69, compRechazado.PlazoVigente.Dia);
        Assert.Equal(2000, compRechazado.PlazoVigente.Hora);
    }

    [Fact]
    public void Vencimiento_marca_incumplido_con_diario_carta_sigue_legible_y_respuesta_tardia_no_reactiva()
    {
        var perfil = CargarPerfilConCupo(pulsosTotalesDia: 12, extraCompromisos: 1);

        // Caso A: el plazo vence sin acuerdo; la carta sigue siendo legible pero sin opción de responder.
        var estadoA = new EstadoPersonaje();
        var compA = CrearCompromiso("comp-a", "farmer:1", "acuarela del río", dia: 68, hora: 1800);
        estadoA.Compromisos.Add(compA);
        GestorCompromisos.RegistrarPeticionProrroga(estadoA, compA.Id, new TiempoJuego { Dia = 68, Hora = 1500 });
        GestorProrrogaCartas.PrepararCartasPendientes(estadoA, perfil, Array.Empty<string>());

        var motorA = new MotorHabitante(perfil, CargarPlantillas(), estadoA, ModoReloj.Vanilla);
        motorA.ProcesarPaso(
            new Observacion(68, 1800, "Forest", 10, 10, 0.9, "soleado", Array.Empty<EntidadObservada>()),
            horario: null,
            ahoraMs: 1_000,
            minutosJuego: 10);

        Assert.Equal(EstadosCompromiso.Incumplido, compA.Estado);
        Assert.Equal(EstadosProrroga.Vencida, compA.Prorroga!.Estado);
        Assert.NotEmpty(estadoA.Diario);
        Assert.Contains(estadoA.Diario, ep => ep.ResumenProsa.Contains("acuarela del río", StringComparison.Ordinal));

        var vistaTrasVencimiento = GestorProrrogaCartas.AbrirCarta(
            estadoA,
            perfil,
            compA.Prorroga.CartaId!,
            "farmer:1",
            new TiempoJuego { Dia = 69, Hora = 900 });
        Assert.NotNull(vistaTrasVencimiento);
        Assert.False(vistaTrasVencimiento!.PuedeResponder);
        Assert.Contains("acuarela del río", vistaTrasVencimiento.Carta.CuerpoTexto);

        // También permanece en Data/Mail para lectura desde Colecciones.
        var enAsset = GestorProrrogaCartas.ObtenerCartasParaAsset(estadoA, perfil);
        Assert.Single(enAsset);
        Assert.Contains("[#]", GestorProrrogaCartas.FormatearParaDataMail(enAsset[0]));

        // Caso B: el jugador abre la carta ANTES del vencimiento, pasa el plazo en FakeWorld y responde tarde.
        var mundoB = CrearMundoDestinatarioAusente(dia: 68, hora: 1750);
        var estadoB = new EstadoPersonaje();
        var compB = CrearCompromiso("comp-b", "farmer:1", "escultura pequeña", dia: 68, hora: 1800);
        estadoB.Compromisos.Add(compB);
        GestorCompromisos.RegistrarPeticionProrroga(estadoB, compB.Id, new TiempoJuego { Dia = 68, Hora = 1500 });
        GestorProrrogaCartas.PrepararCartasPendientes(estadoB, perfil, Array.Empty<string>());
        var cartaBId = compB.Prorroga!.CartaId!;

        var vistaAbierta = GestorProrrogaCartas.AbrirCarta(estadoB, perfil, cartaBId, "farmer:1", mundoB.Reloj.Juego);
        Assert.NotNull(vistaAbierta);
        Assert.True(vistaAbierta!.PuedeResponder);

        // El jugador tarda y el reloj alcanza las 18:00 antes de confirmar la respuesta.
        mundoB.Avanzar(TimeSpan.FromSeconds(7)); // 1750 -> 1800
        Assert.Equal(1800, mundoB.Reloj.Juego.Hora);

        var respTardia = GestorProrrogaCartas.ResponderCarta(
            estadoB,
            cartaBId,
            "farmer:1",
            aceptar: true,
            mundoB.Reloj.Juego);

        Assert.False(respTardia.Aplicada);
        Assert.Equal("plazo_vencido", respTardia.Codigo);
        Assert.Equal(EstadosCompromiso.Incumplido, compB.Estado);
        Assert.Equal(EstadosProrroga.Vencida, compB.Prorroga.Estado);
        Assert.Equal(68, compB.PlazoVigente.Dia);
        Assert.Equal(1800, compB.PlazoVigente.Hora);
        Assert.Contains(estadoB.Diario, ep => ep.ResumenProsa.Contains("escultura pequeña", StringComparison.Ordinal));
    }

    [Fact]
    public void Guardar_y_cargar_conserva_plazo_carta_y_resolucion_y_reintentos_son_idempotentes()
    {
        var perfil = CargarPerfilConCupo(pulsosTotalesDia: 3, extraCompromisos: 0);
        var estado = new EstadoPersonaje();
        var comp = CrearCompromiso("comp-save", "farmer:5", "marco tallado", dia: 72, hora: 1700);
        estado.Compromisos.Add(comp);

        GestorCompromisos.RegistrarPeticionProrroga(estado, comp.Id, new TiempoJuego { Dia = 72, Hora = 1400 });
        var preparadas1 = GestorProrrogaCartas.PrepararCartasPendientes(estado, perfil, Array.Empty<string>());
        var carta = Assert.Single(preparadas1);
        Assert.True(GestorProrrogaCartas.MarcarEntregada(estado, carta.CartaId));
        Assert.False(GestorProrrogaCartas.MarcarEntregada(estado, carta.CartaId)); // reintentar entrega es idempotente

        // Guardar y cargar antes de la respuesta.
        var partida1 = new EstadoPartida();
        partida1.Personajes["leah"] = estado;
        var json1 = string.Empty;
        var g1 = new GestorPersistenciaPartida();
        g1.Cargar("save_prorroga", () => partida1, new[] { "leah" });
        g1.Guardar(p => json1 = PersistenciaEstado.Serializar(p));

        var g2 = new GestorPersistenciaPartida();
        var recargada1 = g2.Cargar("save_prorroga", () => PersistenciaEstado.Deserializar(json1), new[] { "leah" });
        var estadoTrasCarga1 = recargada1.Estado.ObtenerSiExiste("leah")!;

        // Reintentar preparación/entrega tras cargar conserva la misma única carta.
        var preparadasTrasCarga = GestorProrrogaCartas.PrepararCartasPendientes(estadoTrasCarga1, perfil, Array.Empty<string>());
        var cartaTrasCarga = Assert.Single(preparadasTrasCarga);
        Assert.Equal(carta.CartaId, cartaTrasCarga.CartaId);
        Assert.True(cartaTrasCarga.YaEntregada);
        Assert.False(GestorProrrogaCartas.MarcarEntregada(estadoTrasCarga1, cartaTrasCarga.CartaId));

        // Responder aceptando, guardar de nuevo y recargar.
        var aceptada = GestorProrrogaCartas.ResponderCarta(
            estadoTrasCarga1,
            cartaTrasCarga.CartaId,
            "farmer:5",
            aceptar: true,
            new TiempoJuego { Dia = 72, Hora = 1500 });
        Assert.True(aceptada.Aplicada);
        Assert.Equal(74, aceptada.NuevoPlazo!.Dia);

        var json2 = string.Empty;
        g2.Guardar(p => json2 = PersistenciaEstado.Serializar(p));

        var g3 = new GestorPersistenciaPartida();
        var recargada2 = g3.Cargar("save_prorroga", () => PersistenciaEstado.Deserializar(json2), new[] { "leah" });
        var compFinal = Assert.Single(recargada2.Estado.ObtenerSiExiste("leah")!.Compromisos);

        Assert.Equal(EstadosCompromiso.Pendiente, compFinal.Estado);
        Assert.Equal(74, compFinal.PlazoVigente.Dia);
        Assert.Equal(1700, compFinal.PlazoVigente.Hora);
        Assert.NotNull(compFinal.Prorroga);
        Assert.Equal(EstadosProrroga.Aceptada, compFinal.Prorroga!.Estado);
        Assert.Equal(carta.CartaId, compFinal.Prorroga.CartaId);
        Assert.True(compFinal.Prorroga.Entregada);
        Assert.NotNull(compFinal.Prorroga.ResueltaEn);
        Assert.Equal(72, compFinal.Prorroga.ResueltaEn!.Dia);
        Assert.Equal(1500, compFinal.Prorroga.ResueltaEn.Hora);

        // Repetir la aceptación tras recargar sigue conservando un solo desplazamiento del plazo.
        var reintentoAcep = GestorProrrogaCartas.ResponderCarta(
            recargada2.Estado.ObtenerSiExiste("leah")!,
            carta.CartaId,
            "farmer:5",
            aceptar: true,
            new TiempoJuego { Dia = 72, Hora = 1600 });
        Assert.False(reintentoAcep.Aplicada);
        Assert.Equal("ya_resuelta", reintentoAcep.Codigo);
        Assert.Equal(74, compFinal.PlazoVigente.Dia);
    }
}
