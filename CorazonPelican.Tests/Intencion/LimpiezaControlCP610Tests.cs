using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using Newtonsoft.Json;

namespace CorazonPelican.Tests.Intencion;

/// <summary>Pruebas de liberación tras bloqueo de AccionIrA / animación nocturna sin reemplazo (CP-610).</summary>
public class LimpiezaControlCP610Tests
{
    private sealed class MediosAnimacionAtascada : IMediosCuerpo
    {
        public string UbicacionActual => "LeahHouse";
        public int CasillaX => 2;
        public int CasillaY => 4;
        public bool EstaReclamado { get; private set; }
        public bool EstaEnAnimacionActividad => true; // Simula animación de actividad que no se limpia inmediatamente.
        public EstadoRutaCuerpo EstadoRutaActual { get; private set; } = EstadoRutaCuerpo.Inactiva;
        public string? MotivoBloqueoRuta { get; private set; }
        public long DuracionAsentamientoSentarseMs => 0;

        public int VecesIniciarRuta { get; private set; }
        public int VecesVolverARutina { get; private set; }

        public void ReclamarControl(string motivo) => this.EstaReclamado = true;

        public bool IniciarRutaHacia(Lugar destino, int orientacionFinal)
        {
            this.VecesIniciarRuta++;
            this.EstadoRutaActual = EstadoRutaCuerpo.Bloqueada;
            this.MotivoBloqueoRuta = "ruta bloqueada";
            return false;
        }

        public void DetenerMovimiento()
        {
        }

        public bool AdoptarPosturaSentada(Lugar destino, int orientacion) => false;

        public bool IntentarMirarObjetivo(string objetivoRef) => false;

        public bool PuedeDibujar(out string? motivoBloqueo)
        {
            motivoBloqueo = "no";
            return false;
        }

        public bool IniciarDibujo(string? obraId) => false;

        public void DetenerAnimacionActividad()
        {
        }

        public bool HacerGesto(string tipo) => false;

        public bool Murmurar(string texto) => false;

        public bool VolverARutina()
        {
            this.VecesVolverARutina++;
            this.EstaReclamado = false;
            return true;
        }

        public bool AplicarCambioHorario(EntradaAgendaDia entrada) => false;

        public void RestaurarHorarioCanonico()
        {
        }
    }

    private static PerfilPersonaje CargarPerfilLeah()
        => JsonConvert.DeserializeObject<PerfilPersonaje>(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.json")))!;

    private static PlantillasProsa CargarPlantillasLeah()
        => PlantillasProsa.DesdeJson(
            File.ReadAllText(Repo.Ruta("CorazonPelican", "assets", "personajes", "leah.plantillas.json")));

    [Fact]
    public void AccionIrA_bloqueada_sin_reemplazo_libera_control_tras_tiempo_max_respuesta_CP610()
    {
        var perfil = CargarPerfilLeah();
        var estado = new EstadoPersonaje();
        var medios = new MediosAnimacionAtascada();
        var lugares = new LugaresOfrecidos(perfil.Lexico.Lugares);
        var ejecutor = new EjecutorCuerpo(new ContextoEjecucionAccion(medios, lugares, ModoReloj.Pelican));

        var motor = new MotorHabitante(
            perfil,
            CargarPlantillasLeah(),
            estado,
            ModoReloj.Pelican,
            publicarPulso: p =>
            {
                p.Id = 1;
                return p;
            },
            tiempoMaxRespuestaMs: 4_000)
        {
            Epoca = "ep-610",
            MenteHabilitada = true,
        };
        motor.ConectarEjecutorCuerpo(ejecutor, () => 10_000);

        // 1. Iniciar plan con ir_a mientras la animación sigue devolviendo true.
        ejecutor.EjecutarPlan(new[] { new PasoPlan { Accion = "ir_a", Lugar = "tu_casa" } }, ahoraMs: 1_000, idPlan: "int-3");
        Assert.True(medios.EstaReclamado);

        // 2. Antes de MaxEsperaSalidaAnimacionMs (2_000 ms) espera salida sin bloquear todavía.
        ejecutor.AvanzarTick(ahoraMs: 2_000);
        Assert.Equal(0, medios.VecesIniciarRuta);

        // 3. Al superar 2_000 ms intenta la ruta (bloqueada en el test) sin esperar 180 s.
        ejecutor.AvanzarTick(ahoraMs: 3_500);
        Assert.Equal(1, medios.VecesIniciarRuta);
        Assert.False(ejecutor.Ocupado);

        // 4. Si no llega plan de reemplazo tras tiempoMaxRespuestaMs (4_000 ms), degrada y libera control.
        var obs = new Observacion(1, 2550, "LeahHouse", 2, 4, 0.2, "soleado", Array.Empty<EntidadObservada>());
        motor.ProcesarPaso(obs, horario: null, ahoraMs: 8_000, minutosJuego: 0);

        Assert.False(medios.EstaReclamado);
        Assert.True(medios.VecesVolverARutina >= 1);
    }
}
