using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Tests.Simulacion;

/// <summary>
/// Bucle de atención de prueba sobre el FakeWorld (ADS §4, pasos 3–7): observa, calcula saliencia y
/// bandas, actualiza la novedad, pasa los sucesos del cuerpo y recoge los disparos. Es un arnés de
/// tests, no el módulo del motor (que llega con HabitanteModule).
/// </summary>
public sealed class BucleAtencion
{
    private readonly ContextoSaliencia contexto;
    private readonly ClasificadorBandas bandas;
    private readonly Novedad novedad = new();
    private int sucesosVistos;
    private string? planEnCurso;

    public BucleAtencion(FakeWorld mundo, PerfilPersonaje perfil, ContextoSaliencia? contexto = null)
    {
        this.Mundo = mundo;
        this.Disparadores = new Disparadores(perfil);
        this.bandas = new ClasificadorBandas(perfil.Umbrales);
        this.contexto = contexto ?? new ContextoSaliencia(perfil.Rasgos, EstadoAtencion.Abierta, perfil.Umbrales.Periferia);
    }

    public FakeWorld Mundo { get; }

    public Disparadores Disparadores { get; }

    public List<Disparo> Disparos { get; } = new();

    /// <summary>Lanza un plan de la mente con su id (para el hito de intención).</summary>
    public void EjecutarPlan(string idPlan, params PasoPlan[] pasos)
    {
        this.planEnCurso = idPlan;
        this.Disparadores.IniciarPlanIntencion(idPlan, this.Mundo.Reloj.MilisegundosReales);
        this.Mundo.Cuerpo.Ejecutar(pasos);
    }

    public void Interlocucion(string interlocutor)
        => this.Disparos.Add(this.Disparadores.Interlocucion(interlocutor, this.Mundo.Observar(), this.Mundo.Reloj.MilisegundosReales));

    public void Avanzar(TimeSpan duracion)
    {
        var minutosPorPaso = FakeWorld.PasoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(this.Mundo.Reloj.Modo);
        var pasos = (long)duracion.TotalMilliseconds / FakeWorld.PasoMs;
        for (var i = 0; i < pasos; i++)
        {
            var observacion = this.Mundo.AvanzarObservando(TimeSpan.FromMilliseconds(FakeWorld.PasoMs)).Single();
            var ahora = this.Mundo.Reloj.MilisegundosReales;
            this.PasarSucesosDelCuerpo(observacion, ahora);

            var saliencias = Saliencia.Calcular(observacion, this.contexto, this.novedad);
            this.novedad.Exponer(observacion.Entidades.Select(e => e.Ref), minutosPorPaso);
            var enBandas = this.bandas.Clasificar(saliencias);
            this.Disparos.AddRange(this.Disparadores.Observar(observacion, enBandas, this.Mundo.HorarioHoy, ahora, minutosPorPaso));
        }
    }

    private void PasarSucesosDelCuerpo(Observacion observacion, long ahora)
    {
        var sucesos = this.Mundo.Cuerpo.Sucesos;
        for (; this.sucesosVistos < sucesos.Count; this.sucesosVistos++)
        {
            if (sucesos[this.sucesosVistos].Tipo == TipoSuceso.PlanTerminado && this.planEnCurso is not null)
            {
                this.Disparadores.TerminarPlanIntencion(this.planEnCurso, observacion, ahora);
                this.planEnCurso = null;
            }
        }
    }
}
