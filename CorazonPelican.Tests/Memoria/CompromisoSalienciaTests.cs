using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Memoria;

/// <summary>
/// Guardas de compromisos con plazo, saliencia creciente, prórroga ante cupos agotados e incumplimiento en memoria (R-09, CP-502).
/// </summary>
public class CompromisoSalienciaTests
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

    private static CompromisoEstado CrearCompromiso(string id = "comp-1", string destinatario = "farmer:1", int dia = 10, int hora = 1900)
        => new()
        {
            Id = id,
            Tipo = TiposActo.Promete,
            Destinatario = destinatario,
            Objeto = "boceto del muelle",
            PlazoVigente = new TiempoJuego { Dia = dia, Hora = hora },
            Estado = EstadosCompromiso.Pendiente,
        };

    [Fact]
    public void Saliencia_crece_monotonicamente_al_acercarse_el_plazo()
    {
        var comp = CrearCompromiso(dia: 10, hora: 1800);

        var lejana = GestorCompromisos.CalcularSaliencia(comp, new TiempoJuego { Dia = 8, Hora = 1200 });
        var en24h = GestorCompromisos.CalcularSaliencia(comp, new TiempoJuego { Dia = 9, Hora = 1800 });
        var en12h = GestorCompromisos.CalcularSaliencia(comp, new TiempoJuego { Dia = 10, Hora = 600 });
        var en6h = GestorCompromisos.CalcularSaliencia(comp, new TiempoJuego { Dia = 10, Hora = 1200 });
        var en2h = GestorCompromisos.CalcularSaliencia(comp, new TiempoJuego { Dia = 10, Hora = 1600 });
        var vencido = GestorCompromisos.CalcularSaliencia(comp, new TiempoJuego { Dia = 10, Hora = 1800 });

        Assert.Equal(0.0, lejana);
        Assert.True(en24h < en12h);
        Assert.True(en12h < en6h);
        Assert.True(en6h < en2h);
        Assert.True(en2h <= vencido);
        Assert.Equal(1.0, vencido);
    }

    [Fact]
    public void Compromiso_cercano_al_plazo_usa_ordinario_extra_o_registra_prorroga_unica_con_plazo_original()
    {
        // 1. Cupo ordinario libre -> consume ordinario libre.
        var perfilOrd = CargarPerfilConCupo(pulsosTotalesDia: 3, extraCompromisos: 1); // 1 ordinario libre, 1 extra
        var estadoOrd = new EstadoPersonaje();
        estadoOrd.Compromisos.Add(CrearCompromiso("c-ord", dia: 5, hora: 1900));
        var motorOrd = new MotorHabitante(perfilOrd, CargarPlantillas(), estadoOrd, ModoReloj.Vanilla);

        var resOrd = motorOrd.ProcesarPaso(
            new Observacion(5, 1600, "Forest", 10, 10, 1.0, "soleado", Array.Empty<EntidadObservada>()),
            horario: null,
            ahoraMs: 1_000,
            minutosJuego: 10);
        Assert.Contains(resOrd.Pulsos, p => p.Motivo == Motivos.Compromiso);
        Assert.Equal(1, motorOrd.Estado.Contadores!.OrdinariosLibresGastados);
        Assert.Equal(0, motorOrd.Estado.Contadores.ExtrasCompromisosGastados);

        // 2. Ordinarios agotados, bolsa extra libre -> usa bolsa extra sin tocar cadenas.
        var estadoExtra = new EstadoPersonaje();
        estadoExtra.Compromisos.Add(CrearCompromiso("c-ext", dia: 5, hora: 1900));
        var motorExtra = new MotorHabitante(perfilOrd, CargarPlantillas(), estadoExtra, ModoReloj.Vanilla);
        // Gastamos la única plaza ordinaria del día 5 mediante un pulso manual.
        var obs5 = new Observacion(5, 1500, "Forest", 10, 10, 1.0, "soleado", Array.Empty<EntidadObservada>());
        Assert.True(motorExtra.ForzarPulso(obs5, ahoraMs: 500).Emitido);

        var resExtra = motorExtra.ProcesarPaso(
            new Observacion(5, 1600, "Forest", 10, 10, 1.0, "soleado", Array.Empty<EntidadObservada>()),
            horario: null,
            ahoraMs: 1_000,
            minutosJuego: 10);
        Assert.Contains(resExtra.Pulsos, p => p.Motivo == Motivos.Compromiso);
        Assert.Equal(1, motorExtra.Estado.Contadores!.OrdinariosLibresGastados);
        Assert.Equal(1, motorExtra.Estado.Contadores.ExtrasCompromisosGastados);

        // 3. Ambas bolsas agotadas -> registra UNA sola petición de prórroga y conserva el plazo original.
        var estadoPror = new EstadoPersonaje();
        var compSinCupo = CrearCompromiso("c-pror", dia: 5, hora: 1900);
        estadoPror.Compromisos.Add(compSinCupo);
        var perfilCero = CargarPerfilConCupo(pulsosTotalesDia: 3, extraCompromisos: 0); // 1 ordinario libre, 0 extra
        var motorPror = new MotorHabitante(perfilCero, CargarPlantillas(), estadoPror, ModoReloj.Vanilla);
        Assert.True(motorPror.ForzarPulso(obs5, ahoraMs: 500).Emitido);

        var obs16 = new Observacion(5, 1600, "Forest", 10, 10, 1.0, "soleado", Array.Empty<EntidadObservada>());
        var resSinCupo1 = motorPror.ProcesarPaso(obs16, horario: null, ahoraMs: 1_000, minutosJuego: 10);
        Assert.DoesNotContain(resSinCupo1.Pulsos, p => p.Motivo == Motivos.Compromiso);

        var peticion = compSinCupo.Prorroga;
        Assert.NotNull(peticion);
        Assert.Equal(EstadosProrroga.Pendiente, peticion!.Estado);
        Assert.Equal(5, compSinCupo.PlazoVigente.Dia);
        Assert.Equal(1900, compSinCupo.PlazoVigente.Hora);
        Assert.Equal(7, peticion.PlazoPropuesto.Dia);

        // Repetir la observación conserva esa única petición y el plazo original intacto.
        var obs17 = new Observacion(5, 1700, "Forest", 10, 10, 1.0, "soleado", Array.Empty<EntidadObservada>());
        motorPror.ProcesarPaso(obs17, horario: null, ahoraMs: 2_000, minutosJuego: 60);
        GestorCompromisos.RegistrarPeticionProrroga(estadoPror, compSinCupo.Id, new TiempoJuego { Dia = 5, Hora = 1700 });
        Assert.Same(peticion, compSinCupo.Prorroga);
        Assert.Equal(5, compSinCupo.PlazoVigente.Dia);
        Assert.Equal(1900, compSinCupo.PlazoVigente.Hora);
    }

    [Fact]
    public void Compromiso_vencido_pasa_a_memoria_como_incumplido_y_aparece_en_siguiente_pulso_con_esa_persona()
    {
        var perfil = CargarPerfilConCupo(pulsosTotalesDia: 12, extraCompromisos: 1);
        var estado = new EstadoPersonaje();
        estado.Compromisos.Add(CrearCompromiso("c-venc", "farmer:77", dia: 6, hora: 1800));
        var motor = new MotorHabitante(perfil, CargarPlantillas(), estado, ModoReloj.Vanilla);

        // Llega las 18:00 del día 6 sin cumplir el compromiso.
        motor.ProcesarPaso(
            new Observacion(6, 1800, "Forest", 10, 10, 0.9, "soleado", Array.Empty<EntidadObservada>()),
            horario: null,
            ahoraMs: 1_000,
            minutosJuego: 10);

        Assert.Equal(EstadosCompromiso.Incumplido, estado.Compromisos[0].Estado);
        var recuerdo = Assert.Single(estado.Recuerdos);
        Assert.Contains("Incumplí", recuerdo.Texto);
        Assert.NotEmpty(recuerdo.OrigenDiario);

        // Con otra persona (o a solas), ese compromiso incumplido no aparece si no está el destinatario.
        var pulsoSolo = motor.ForzarPulso(
            new Observacion(7, 1000, "Forest", 10, 10, 1.0, "soleado", Array.Empty<EntidadObservada>()),
            ahoraMs: 2_000).Pulso!;
        Assert.Null(pulsoSolo.CompromisosProximos);

        // Al coincidir o hablar con el destinatario ("farmer:77"), aparece en el pulso para poder disculparse.
        var obsConDest = new Observacion(
            7,
            1100,
            "Town",
            20,
            20,
            1.0,
            "soleado",
            new[] { new EntidadObservada("farmer:77", CategoriaEntidad.Granjero, "Mateo", "Town", 20, 21, new[] { "persona" }, 6) });
        var pulsoConDest = motor.ForzarPulso(obsConDest, ahoraMs: 3_000).Pulso!;
        Assert.NotNull(pulsoConDest.CompromisosProximos);
        var proyectado = Assert.Single(pulsoConDest.CompromisosProximos!);
        Assert.Equal("c-venc", proyectado.Id);
        Assert.Contains("incumplido", proyectado.Prosa);
    }

    [Fact]
    public void Guardar_y_cargar_conserva_compromisos_estado_y_peticion_de_prorroga()
    {
        var estado = new EstadoPersonaje();
        var comp = CrearCompromiso("c-persist", "farmer:9", dia: 12, hora: 1700);
        estado.Compromisos.Add(comp);
        GestorCompromisos.RegistrarPeticionProrroga(estado, comp.Id, new TiempoJuego { Dia = 12, Hora = 1500 });

        var partida = new EstadoPartida();
        partida.Personajes["leah"] = estado;

        var guardadoJson = string.Empty;
        var gestor1 = new GestorPersistenciaPartida();
        gestor1.Cargar("comp_save", () => partida, new[] { "leah" });
        gestor1.Guardar(p => guardadoJson = PersistenciaEstado.Serializar(p));

        var gestor2 = new GestorPersistenciaPartida();
        var carga2 = gestor2.Cargar("comp_save", () => PersistenciaEstado.Deserializar(guardadoJson), new[] { "leah" });
        var recargado = Assert.Single(carga2.Estado.ObtenerSiExiste("leah")!.Compromisos);
        Assert.Equal("c-persist", recargado.Id);
        Assert.Equal(EstadosCompromiso.Pendiente, recargado.Estado);
        Assert.Equal(12, recargado.PlazoVigente.Dia);
        Assert.NotNull(recargado.Prorroga);
        Assert.Equal(EstadosProrroga.Pendiente, recargado.Prorroga!.Estado);
        Assert.Equal(14, recargado.Prorroga.PlazoPropuesto.Dia);
    }
}
