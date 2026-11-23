using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CorazonPelican.Adaptador;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Observabilidad;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Módulo anfitrión del bucle por tick de cada personaje gobernado (ADS §2, §4, CP-109):
    /// observar → saliencia/novedad/bandas → búfer/huella → disparadores → escena/pulso/diario y telemetría.
    /// Corre incluso con <c>MenteHabilitada = false</c> (fase 1).
    /// </summary>
    internal sealed class HabitanteModule : IModule, IDisposable
    {
        private readonly ObservadorJuego observador = new();
        private readonly Stopwatch cronometro = new();

        private ModContext? context;
        private IPelicanApi? pelicanApi;
        private Action<int, int>? hookSaltoHorarioPelican;
        private TelemetriaJsonl? telemetria;
        private long ultimoPasoMs;
        private long siguientePulsoLocalId = 1;

        public string Nombre => "Habitante";

        public LadoModulo Lado => LadoModulo.Anfitrion;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;

            contexto.Helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            contexto.Helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            contexto.Helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            contexto.Helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            contexto.Helper.Events.GameLoop.DayEnding += this.OnDayEnding;
            contexto.Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
        }

        public void Dispose()
        {
            if (this.pelicanApi is not null && this.hookSaltoHorarioPelican is not null)
            {
                try
                {
                    this.pelicanApi.UnregisterTimeJumpHook(this.hookSaltoHorarioPelican);
                }
                catch
                {
                }
            }

            this.hookSaltoHorarioPelican = null;

            if (this.context is null)
            {
                return;
            }

            this.context.Helper.Events.GameLoop.GameLaunched -= this.OnGameLaunched;
            this.context.Helper.Events.GameLoop.SaveLoaded -= this.OnSaveLoaded;
            this.context.Helper.Events.GameLoop.DayStarted -= this.OnDayStarted;
            this.context.Helper.Events.GameLoop.UpdateTicked -= this.OnUpdateTicked;
            this.context.Helper.Events.GameLoop.DayEnding -= this.OnDayEnding;
            this.context.Helper.Events.GameLoop.ReturnedToTitle -= this.OnReturnedToTitle;
            this.LimpiarPartida();
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var api = this.IntentarObtenerPelicanApi();
            if (api is null || this.context is null)
            {
                return;
            }

            try
            {
                this.hookSaltoHorarioPelican ??= this.EnSaltoHorarioPelican;
                api.RegisterTimeJumpHook(this.hookSaltoHorarioPelican);
                this.pelicanApi = api;
                this.context.Monitor.Log("[Habitante] Suscrito al hook off-role de salto horario de Pelican 4Ever (CP-611).", LogLevel.Trace);
            }
            catch (Exception ex)
            {
                this.context.Monitor.Log(
                    $"[Habitante] Pelican 4Ever no expone RegisterTimeJumpHook todavía ({ex.Message}).",
                    LogLevel.Trace);
            }
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            if (this.context is null || !Context.IsMainPlayer)
            {
                return;
            }

            foreach (var perfil in this.context.PersonajesGobernados.Values)
            {
                var npc = Game1.getCharacterFromName(perfil.Npc);
                ReclamacionNpc.LiberarAlIniciarJornada(npc, "DayStarted", this.context.Monitor);
                DialogoModule.LimpiarEstadoDialogoNpc(npc);
            }
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            if (this.context is null || !Context.IsMainPlayer)
            {
                return;
            }

            this.LimpiarPartida();

            var directorioBaseTelemetria = Path.Combine(this.context.Helper.DirectoryPath, "telemetria");
            var saveId = string.IsNullOrWhiteSpace(Constants.SaveFolderName) ? "partida" : Constants.SaveFolderName;
            this.telemetria = new TelemetriaJsonl(
                directorioBaseTelemetria,
                saveId,
                this.context.Config.Telemetria);
            this.context.Telemetria = this.telemetria;

            this.cronometro.Restart();
            this.ultimoPasoMs = 0;
            this.siguientePulsoLocalId = 1;
            this.pelicanApi ??= this.IntentarObtenerPelicanApi();

            foreach (var (id, perfil) in this.context.PersonajesGobernados)
            {
                var plantillas = this.CargarPlantillas(id, perfil);
                if (plantillas is null)
                {
                    continue;
                }

                var estado = this.context.ObtenerOIniciarEstado(id);
                var motor = new MotorHabitante(
                    perfil,
                    plantillas,
                    estado,
                    this.context.ModoRelojEfectivo,
                    this.telemetria,
                    this.context.CostesBucle,
                    this.PublicarPulso,
                    tamanoMaxMemoriaKB: this.context.Config.TamanoMaxMemoriaKB,
                    tiempoMaxRespuestaMs: this.context.Config.TiempoMaxRespuestaMs,
                    modoValidacionMenciones: this.context.Config.ModoValidacionMenciones)
                {
                    Epoca = this.context.Api.EpocaVigente ?? "local",
                    Idioma = ObtenerCodigoIdiomaJuego(),
                    MenteHabilitada = this.context.Config.MenteHabilitada,
                    AnticipacionHabilitada = this.context.Config.AnticipacionHabilitada,
                };

                motor.DialogoPreparadoDisponible += (diaPreparado, preparado) =>
                {
                    DialogoModule.PublicarDialogoPreparadoEnNpc(perfil.Npc, diaPreparado, preparado);
                };

                motor.AcercamientoAlJugadorCompletado += objetivoRef =>
                {
                    var dia = Math.Max(1, Game1.Date?.TotalDays ?? 1);
                    DialogoModule.EntregarIniciativaDesdeMotor(perfil.Npc, motor, dia, objetivoRef);
                    motor.DegradarARutina("iniciativa_entregada", this.cronometro.ElapsedMilliseconds);
                };

                motor.AnticipacionCancelada += (pulsoId, interlocutorRef, motivo) =>
                {
                    this.context.Api.RegistrarEvento(
                        "anticipacion_cancelada",
                        id,
                        $"Anticipación cancelada ({motivo}) para {interlocutorRef}",
                        new System.Collections.Generic.Dictionary<string, object?>
                        {
                            ["pulso_id"] = pulsoId,
                            ["interlocutor"] = interlocutorRef,
                            ["motivo"] = motivo,
                        });
                };

                var cuerpo = new CuerpoNpc(
                    perfil,
                    motor.LugaresOfrecidos,
                    this.context.ModoRelojEfectivo,
                    this.context.Monitor,
                    () => this.ObservarPersonaje(id));
                motor.ConectarEjecutorCuerpo(cuerpo.Ejecutor, () => this.cronometro.ElapsedMilliseconds);
                motor.DegradadoARutina += (motivo, _) =>
                {
                    this.context.Monitor.Log(
                        $"[Habitante:{id}] Degradación automática a rutina canónica (motivo={motivo}).",
                        LogLevel.Info);
                };

                var npc = Game1.getCharacterFromName(perfil.Npc);
                ReclamacionNpc.LiberarAlIniciarJornada(npc, "SaveLoaded", this.context.Monitor);

                this.context.MotoresHabitante[id] = motor;
                this.context.CuerposHabitante[id] = cuerpo;
            }

            CoordinadorApiAnfitrion.Conectar(
                this.context.Api,
                id => this.context.MotoresHabitante.TryGetValue(id, out var m) ? m : null,
                id => this.context.EstadoPartida.ObtenerSiExiste(id),
                enHiloPrincipal: true,
                observarEnVivo: this.ObservarPersonaje);
            this.context.ProveedorObservacion = this.ObservarPersonaje;
            this.context.RelojMs = () => this.cronometro.ElapsedMilliseconds;

            this.context.Monitor.Log(
                $"HabitanteModule activo (motores: {this.context.MotoresHabitante.Count}, telemetría: {(this.context.Config.Telemetria ? this.telemetria.DirectorioPartida : "desactivada")}).",
                LogLevel.Trace);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            var ahoraTickMs = this.cronometro.ElapsedMilliseconds;
            using (this.context.CostesBucle.Medir("cuerpo"))
            {
                foreach (var cuerpo in this.context.CuerposHabitante.Values)
                {
                    cuerpo.AvanzarTick(ahoraTickMs);
                }
            }

            if (!e.IsMultipleOf((uint)this.context.Config.SensorioCadenciaTicks))
            {
                return;
            }

            var ahoraMs = this.cronometro.ElapsedMilliseconds;
            var transcurridoMs = Math.Max(0, ahoraMs - this.ultimoPasoMs);
            this.ultimoPasoMs = ahoraMs;

            var enNightHold = this.EstaEnNightHoldPelican();
            var minutosJuego = Game1.shouldTimePass() && !enNightHold
                ? transcurridoMs * 10.0 / HoraJuego.MilisegundosPorDiezMinutos(this.context.ModoRelojEfectivo)
                : 0.0;
            var esDiaFestival = this.EsDiaFestivalEnCalendario();

            foreach (var (id, motor) in this.context.MotoresHabitante)
            {
                try
                {
                    if (!this.context.Config.MenteHabilitada && motor.MenteHabilitada)
                    {
                        motor.DesactivarMente(ahoraMs);
                    }
                    else
                    {
                        motor.MenteHabilitada = this.context.Config.MenteHabilitada;
                    }

                    var npc = Game1.getCharacterFromName(motor.Perfil.Npc);
                    if (npc is null)
                    {
                        continue;
                    }

                    Observacion obs;
                    using (this.context.CostesBucle.Medir("observar"))
                    {
                        obs = this.observador.Observar(npc, npc.currentLocation);
                    }

                    obs = this.context.SucesosPelican.AplicarAObservacion(obs, id);
                    this.context.UltimasObservaciones[id] = obs;
                    var horario = LeerHorario(npc);
                    var condicionesDia = CalendarioFestivales.ConstruirCondicionesDia(
                        obs.Clima,
                        esDiaFestival,
                        esBoda: Game1.weddingToday);
                    motor.Epoca = this.context.Api.EpocaVigente ?? motor.Epoca;
                    motor.Idioma = ObtenerCodigoIdiomaJuego();
                    motor.ProcesarPaso(obs, horario, ahoraMs, minutosJuego, condicionesDia: condicionesDia);
                }
                catch (Exception ex)
                {
                    this.context.Monitor.Log(
                        $"[Habitante:{id}] Error en paso del motor ({ex.Message}); degradando a rutina vanilla.",
                        LogLevel.Error);
                    motor.NotificarErrorModulo(ex.Message, ahoraMs);
                }
            }
        }

        private void OnDayEnding(object? sender, DayEndingEventArgs e)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            var ahoraMs = this.cronometro.ElapsedMilliseconds;
            foreach (var (id, motor) in this.context.MotoresHabitante)
            {
                var obs = this.ObservarPersonaje(id)
                    ?? (this.context.UltimasObservaciones.TryGetValue(id, out var previa) ? previa : null);
                if (obs is null)
                {
                    continue;
                }

                motor.Epoca = this.context.Api.EpocaVigente ?? motor.Epoca;
                motor.ProcesarFinDeDia(obs, ahoraMs, horario: LeerHorarioPorId(motor.Perfil.Npc));
            }

            var diaActual = Math.Max(1, Game1.Date?.TotalDays ?? 1);
            var horaActual = Math.Clamp(Game1.timeOfDay, HoraJuego.InicioDia, HoraJuego.FinDia);
            this.telemetria?.RegistrarCostes(
                this.context.CostesBucle.Resumen(),
                diaActual,
                horaActual,
                this.context.Api.EpocaVigente);
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
            => this.LimpiarPartida();

        private void LimpiarPartida()
        {
            if (this.context is not null)
            {
                CoordinadorApiAnfitrion.Desconectar(this.context.Api);
                this.context.MotoresHabitante.Clear();
                this.context.CuerposHabitante.Clear();
                this.context.ProveedorObservacion = null;
                this.context.RelojMs = null;
                this.context.Telemetria = null;
            }

            this.telemetria = null;
            this.cronometro.Reset();
            this.ultimoPasoMs = 0;
        }

        private void EnSaltoHorarioPelican(int horaAnterior, int horaNueva)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            var diaActual = Math.Max(1, Game1.Date?.TotalDays ?? 1);
            var horaValida = Math.Clamp(horaNueva, HoraJuego.InicioDia, HoraJuego.FinDia);
            var ahoraMs = this.cronometro.ElapsedMilliseconds;

            var nuevaEpoca = ApiMotor.GenerarEpoca();
            this.context.Api.IniciarEpoca(nuevaEpoca, this.context.PersonajesGobernados.Keys);
            if (this.context.Api.MundoCargado && this.context.EstadoApi.StartsWith("activa", StringComparison.OrdinalIgnoreCase))
            {
                this.context.EstadoApi = $"activa en http://127.0.0.1:{this.context.Config.ApiPuerto}/api/v1/ (época {nuevaEpoca})";
            }

            DialogoModule.CancelarTurnosActivos();

            var liberados = 0;
            foreach (var (id, motor) in this.context.MotoresHabitante)
            {
                this.context.UltimasObservaciones.Remove(id);
                motor.Epoca = nuevaEpoca;
                var npc = Game1.getCharacterFromName(motor.Perfil.Npc);
                DialogoModule.LimpiarEstadoDialogoNpc(npc);
                if (motor.RestablecerOffRole(
                    diaActual,
                    horaValida,
                    ahoraMs,
                    horario: npc is null ? null : LeerHorario(npc),
                    restaurarRutinaLocal: false))
                {
                    liberados++;
                }
            }

            this.ultimoPasoMs = ahoraMs;
            this.context.Monitor.Log(
                $"[Habitante] Reinicio técnico off-role ejecutado tras salto horario ({horaAnterior:D4} -> {horaNueva:D4}, " +
                $"personajes={this.context.MotoresHabitante.Count}, liberados={liberados}, época={nuevaEpoca}).",
                LogLevel.Info);
        }

        private IPelicanApi? IntentarObtenerPelicanApi()
        {
            if (this.context is null || !this.context.Helper.ModRegistry.IsLoaded(ModContext.Pelican4EverUniqueId))
            {
                return null;
            }

            try
            {
                return this.context.Helper.ModRegistry.GetApi<IPelicanApi>(ModContext.Pelican4EverUniqueId);
            }
            catch
            {
                return null;
            }
        }

        private bool EstaEnNightHoldPelican()
        {
            if (this.pelicanApi is null)
            {
                return false;
            }

            try
            {
                return this.pelicanApi.IsNightHold();
            }
            catch
            {
                return false;
            }
        }

        private bool EsDiaFestivalEnCalendario()
        {
            if (Game1.isFestival())
            {
                return true;
            }

            if (this.context is null)
            {
                return false;
            }

            try
            {
                var fechas = this.context.Helper.GameContent.Load<System.Collections.Generic.Dictionary<string, string>>("Data/Festivals/FestivalDates");
                return CalendarioFestivales.ResolverFestivalEnFecha(fechas, Game1.currentSeason, Game1.dayOfMonth) is not null;
            }
            catch
            {
                return false;
            }
        }

        private PlantillasProsa? CargarPlantillas(string personajeId, PerfilPersonaje perfil)
        {
            if (this.context is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(perfil.Plantillas))
            {
                this.context.Monitor.Log(
                    $"Personaje «{personajeId}»: no declara ruta de plantillas de prosa.",
                    LogLevel.Error);
                return null;
            }

            var ruta = Path.IsPathRooted(perfil.Plantillas)
                ? perfil.Plantillas
                : Path.Combine(this.context.Helper.DirectoryPath, perfil.Plantillas);

            try
            {
                var json = File.ReadAllText(ruta, Encoding.UTF8);
                var plantillas = PlantillasProsa.DesdeJson(json);
                var validacion = PlantillasProsa.Validar(plantillas);
                foreach (var aviso in validacion.Avisos)
                {
                    this.context.Monitor.Log($"Plantillas «{personajeId}»: {aviso}", LogLevel.Warn);
                }

                if (!validacion.EsValido)
                {
                    foreach (var error in validacion.Errores)
                    {
                        this.context.Monitor.Log($"Plantillas «{personajeId}»: {error}", LogLevel.Error);
                    }

                    return null;
                }

                return plantillas;
            }
            catch (Exception ex)
            {
                this.context.Monitor.Log(
                    $"No se pudieron cargar las plantillas de «{personajeId}» desde «{ruta}»: {ex.Message}",
                    LogLevel.Error);
                return null;
            }
        }

        private Pulso PublicarPulso(Pulso pulso)
        {
            if (this.context is not null && this.context.Config.MenteHabilitada && this.context.Api.MundoCargado)
            {
                pulso.Epoca = this.context.Api.EpocaVigente ?? pulso.Epoca;
                return this.context.Api.EmitirPulso(pulso);
            }

            if (pulso.Id <= 0)
            {
                pulso.Id = this.siguientePulsoLocalId++;
            }

            return pulso;
        }

        private Observacion? ObservarPersonaje(string personajeId)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                return null;
            }

            if (!this.context.MotoresHabitante.TryGetValue(personajeId, out var motor))
            {
                return null;
            }

            var npc = Game1.getCharacterFromName(motor.Perfil.Npc);
            if (npc is null)
            {
                return null;
            }

            var obs = this.observador.Observar(npc, npc.currentLocation);
            obs = this.context.SucesosPelican.AplicarAObservacion(obs, personajeId);
            this.context.UltimasObservaciones[personajeId] = obs;
            return obs;
        }

        private static string ObtenerCodigoIdiomaJuego()
        {
            var idiomaDialogo = DialogoModule.ObtenerIdiomaClientePreferido();
            if (!string.IsNullOrWhiteSpace(idiomaDialogo))
            {
                return idiomaDialogo;
            }

            var codigo = LocalizedContentManager.LanguageCodeString(LocalizedContentManager.CurrentLanguageCode);
            return string.IsNullOrWhiteSpace(codigo) ? "en" : codigo.Trim().ToLowerInvariant();
        }

        private static HorarioDia? LeerHorarioPorId(string nombreNpc)
        {
            var npc = Game1.getCharacterFromName(nombreNpc);
            return npc is null ? null : LeerHorario(npc);
        }

        private static HorarioDia? LeerHorario(NPC npc)
        {
            var schedule = npc.Schedule;
            if (schedule is null || schedule.Count == 0)
            {
                return null;
            }

            var clave = string.IsNullOrWhiteSpace(npc.ScheduleKey) ? "rutina" : npc.ScheduleKey;
            var entradas = schedule
                .OrderBy(kv => kv.Key)
                .Select(kv => new EntradaHorario(
                    kv.Key,
                    kv.Value.targetLocationName ?? string.Empty,
                    kv.Value.targetTile.X,
                    kv.Value.targetTile.Y,
                    kv.Value.endOfRouteBehavior))
                .ToList();
            return new HorarioDia(clave, entradas);
        }
    }
}
