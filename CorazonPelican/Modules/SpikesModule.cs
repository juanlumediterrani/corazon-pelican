using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using CorazonPelican.Framework;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Pathfinding;
using StardewValley.Quests;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Mecanismos y parches de verificación controlada para las spikes en vivo (V-01, V-02) bajo
    /// <c>ModoDesarrollo = true</c>.
    /// </summary>
    internal sealed class SpikesModule : IModule, IDisposable
    {
        public const string ClaveControlModData = "CorazonPelican.Control";

        private const string TextoV01PorDefecto = "Hola desde Corazón Pelican: hoy hace buena luz junto al río.";

        private static readonly MethodInfo? MetodoGetRouteEndBehavior =
            AccessTools.Method(typeof(NPC), "getRouteEndBehaviorFunction", new[] { typeof(string), typeof(string) });

        private static SpikesModule? instanciaActiva;

        [ThreadStatic]
        private static bool enPrefixCheckAction;

        [ThreadStatic]
        private static bool enCheckSchedule;

        private readonly HashSet<PathFindController> controladoresEnvueltos = new();

        private ModContext? context;
        private Harmony? harmony;
        private bool parchesAplicados;
        private bool v01Activo;
        private string v01Texto = TextoV01PorDefecto;
        private bool v02MonitorActivo = true;

        public string Nombre => "Spikes";

        public LadoModulo Lado => LadoModulo.Ambos;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;
            if (!contexto.Config.ModoDesarrollo)
            {
                return;
            }

            instanciaActiva = this;
            this.AplicarParches();

            contexto.Helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;
            contexto.Helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            contexto.Helper.Events.GameLoop.Saving += this.OnSaving;

            contexto.Helper.ConsoleCommands.Add(
                "cp_v01",
                "Controla el prototipo de Spike V-01 (checkAction): cp_v01 <on|off|estado|texto <frase>>.",
                this.OnComandoV01);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v01_diag",
                "Muestra amistad, estado de conversación, regalos y misiones para un NPC: cp_v01_diag [npc].",
                this.OnComandoV01Diag);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_horario",
                "Inspecciona ScheduleKey, entradas del día, estado de reclamación y ruta actual: cp_v02_horario [npc].",
                this.OnComandoV02Horario);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_reclamar",
                "Suspende el horario del NPC sin teletransportarlo y marca modData[CorazonPelican.Control]: cp_v02_reclamar [npc].",
                this.OnComandoV02Reclamar);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_ir_a",
                "Mueve al NPC reclamado con PathFindController nativo: cp_v02_ir_a <ubicacion> <x> <y> [orientacion] [npc].",
                this.OnComandoV02IrA);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_volver",
                "Devuelve al NPC reclamado al tramo horario vigente con PathFindController nativo: cp_v02_volver [npc].",
                this.OnComandoV02Volver);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_saltar",
                "Salta o sustituye una entrada concreta del horario del día: cp_v02_saltar <hora> [ubicacion x y [orientacion]].",
                this.OnComandoV02Saltar);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_escenario",
                "Configura estación, día y clima a las 06:00 y recarga TryLoadSchedule vanilla: cp_v02_escenario <spring|summer|fall|winter> <dia> <sol|lluvia>.",
                this.OnComandoV02Escenario);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_reloj",
                "Avanza el reloj en pasos de 10 min invocando NPC.checkSchedule sin teletransportar: cp_v02_reloj <HHMM> [caminar].",
                this.OnComandoV02Reloj);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v02_caminar",
                "Avanza ticks del controlador de ruta nativo de un NPC hasta llegar a destino: cp_v02_caminar [maxTicks] [npc].",
                this.OnComandoV02Caminar);
        }

        public void Dispose()
        {
            if (this.context is not null)
            {
                this.context.Helper.Events.GameLoop.SaveLoaded -= this.OnSaveLoaded;
                this.context.Helper.Events.GameLoop.DayStarted -= this.OnDayStarted;
                this.context.Helper.Events.GameLoop.Saving -= this.OnSaving;
            }

            if (ReferenceEquals(instanciaActiva, this))
            {
                instanciaActiva = null;
            }

            if (this.parchesAplicados && this.harmony is not null)
            {
                this.harmony.UnpatchAll(this.harmony.Id);
                this.parchesAplicados = false;
            }

            this.controladoresEnvueltos.Clear();
            this.v01Activo = false;
        }

        private void AplicarParches()
        {
            if (this.context is null || this.parchesAplicados)
            {
                return;
            }

            var metodoCheckAction = AccessTools.Method(
                typeof(NPC),
                nameof(NPC.checkAction),
                new[] { typeof(Farmer), typeof(GameLocation) });
            var metodoCheckSchedule = AccessTools.Method(
                typeof(NPC),
                nameof(NPC.checkSchedule),
                new[] { typeof(int) });

            if (metodoCheckAction is null || metodoCheckSchedule is null)
            {
                this.context.Monitor.Log("[Spikes] No se encontraron NPC.checkAction o NPC.checkSchedule.", LogLevel.Error);
                return;
            }

            this.harmony = new Harmony($"{this.context.Manifest.UniqueID}.Spikes");
            this.harmony.Patch(
                original: metodoCheckAction,
                prefix: new HarmonyMethod(typeof(SpikesModule), nameof(PrefixCheckAction)));
            this.harmony.Patch(
                original: metodoCheckSchedule,
                prefix: new HarmonyMethod(typeof(SpikesModule), nameof(PrefixCheckSchedule)),
                postfix: new HarmonyMethod(typeof(SpikesModule), nameof(PostfixCheckSchedule)));
            this.parchesAplicados = true;
            this.context.Monitor.Log("[Spikes] Parches V-01 y V-02 registrados en modo desarrollo.", LogLevel.Trace);
        }

        private void OnSaveLoaded(object? sender, SaveLoadedEventArgs e)
        {
            this.controladoresEnvueltos.Clear();
            this.NormalizarReclamacionesAlIniciarJornada("SaveLoaded");
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
        {
            this.controladoresEnvueltos.Clear();
            this.NormalizarReclamacionesAlIniciarJornada("DayStarted");
        }

        private void OnSaving(object? sender, SavingEventArgs e)
        {
            if (this.context is null || !Context.IsMainPlayer)
            {
                return;
            }

            foreach (var perfil in this.context.PersonajesGobernados.Values)
            {
                var npc = Game1.getCharacterFromName(perfil.Npc);
                if (npc is not null && npc.modData.TryGetValue(ClaveControlModData, out var estadoControl))
                {
                    this.context.Monitor.Log(
                        $"[V02] Guardando partida con {npc.Name} reclamada ({ClaveControlModData}={estadoControl}) en {npc.currentLocation?.NameOrUniqueName}@{npc.TilePoint}. Se liberará limpiamente al iniciar el nuevo día.",
                        LogLevel.Info);
                }
            }
        }

        private void NormalizarReclamacionesAlIniciarJornada(string evento)
        {
            if (this.context is null || !Context.IsMainPlayer)
            {
                return;
            }

            foreach (var perfil in this.context.PersonajesGobernados.Values)
            {
                var npc = Game1.getCharacterFromName(perfil.Npc);
                if (npc is null)
                {
                    continue;
                }

                var teniaMarca = npc.modData.TryGetValue(ClaveControlModData, out var valorAnterior);
                if (teniaMarca || npc.ignoreScheduleToday)
                {
                    npc.modData.Remove(ClaveControlModData);
                    npc.ignoreScheduleToday = false;
                    npc.followSchedule = true;
                    if (npc.Schedule is null && !npc.isMarried())
                    {
                        npc.TryLoadSchedule();
                    }

                    this.context.Monitor.Log(
                        $"[V02] ({evento}) Reclamación previa de {npc.Name} liberada (marca={valorAnterior ?? "ninguna"}) -> ignoreScheduleToday=False, ScheduleKey={npc.ScheduleKey ?? "(null)"}, entradas={npc.Schedule?.Count ?? 0}.",
                        LogLevel.Info);
                }
            }
        }

        private void OnComandoV01(string comando, string[] args)
        {
            if (this.context is null)
            {
                return;
            }

            if (args.Length == 0 || string.Equals(args[0], "estado", StringComparison.OrdinalIgnoreCase))
            {
                this.context.Monitor.Log(
                    $"[V01] activo={this.v01Activo}, parches={this.parchesAplicados}, texto=\"{this.v01Texto}\"",
                    LogLevel.Info);
                return;
            }

            if (string.Equals(args[0], "on", StringComparison.OrdinalIgnoreCase))
            {
                this.v01Activo = true;
                this.context.Monitor.Log($"[V01] Intercepción de NPC.checkAction ACTIVADA (texto=\"{this.v01Texto}\").", LogLevel.Info);
                return;
            }

            if (string.Equals(args[0], "off", StringComparison.OrdinalIgnoreCase))
            {
                this.v01Activo = false;
                this.context.Monitor.Log("[V01] Intercepción de NPC.checkAction DESACTIVADA (vanilla intacto).", LogLevel.Info);
                return;
            }

            if (string.Equals(args[0], "texto", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    this.context.Monitor.Log("[V01] Uso: cp_v01 texto <frase>", LogLevel.Warn);
                    return;
                }

                this.v01Texto = string.Join(' ', args.Skip(1));
                this.context.Monitor.Log($"[V01] Texto de prueba actualizado: \"{this.v01Texto}\"", LogLevel.Info);
                return;
            }

            this.context.Monitor.Log("[V01] Subcomando desconocido. Usa: on | off | estado | texto <frase>.", LogLevel.Warn);
        }

        private void OnComandoV01Diag(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V01] Carga primero una partida antes de ejecutar cp_v01_diag.", LogLevel.Warn);
                return;
            }

            var objetivo = this.ResolverNombreNpcObjetivo(args.FirstOrDefault());
            if (string.IsNullOrWhiteSpace(objetivo))
            {
                this.context.Monitor.Log("[V01] No hay NPC objetivo especificado ni gobernado.", LogLevel.Warn);
                return;
            }

            var npc = Game1.getCharacterFromName(objetivo);
            var who = Game1.player;
            who.friendshipData.TryGetValue(objetivo, out var amistad);

            var socializeQuests = who.questLog
                .OfType<SocializeQuest>()
                .Select(q => $"complete={q.completed.Value}, remaining={q.whoToGreet.Count} [{string.Join(",", q.whoToGreet)}]")
                .ToList();

            var introInfo = socializeQuests.Count == 0
                ? "sin SocializeQuest"
                : string.Join(" | ", socializeQuests);

            var tieneAmistad = amistad is not null;
            var puntos = amistad?.Points ?? 0;
            var habladoHoy = amistad?.TalkedToToday ?? false;
            var regalosHoy = amistad?.GiftsToday ?? 0;
            var regalosSemana = amistad?.GiftsThisWeek ?? 0;
            var ubicacion = npc?.currentLocation?.NameOrUniqueName ?? "n/d";
            var casilla = npc?.TilePoint.ToString() ?? "n/d";
            var dialogoCola = npc?.CurrentDialogue.Count ?? 0;
            var objetoActivo = who.ActiveObject?.QualifiedItemId ?? "(ninguno)";

            this.context.Monitor.Log(
                $"[V01-Diag] npc={objetivo} loc={ubicacion}@{casilla} dormido={npc?.isSleeping.Value} " +
                $"evento={Game1.CurrentEvent != null} festival={Game1.isFestival()} " +
                $"tieneAmistad={tieneAmistad} puntos={puntos} habladoHoy={habladoHoy} " +
                $"regalosHoy={regalosHoy} regalosSemana={regalosSemana} colaDialogo={dialogoCola} " +
                $"objetoActivo={objetoActivo} intro=[{introInfo}]",
                LogLevel.Info);
        }

        private void OnComandoV02Horario(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_horario.", LogLevel.Warn);
                return;
            }

            var npc = this.ObtenerNpcObjetivo(args.FirstOrDefault());
            if (npc is null)
            {
                return;
            }

            npc.modData.TryGetValue(ClaveControlModData, out var control);
            var ubicacion = npc.currentLocation?.NameOrUniqueName ?? "n/d";
            var pasosRestantes = npc.controller?.pathToEndPoint?.Count ?? 0;

            this.context.Monitor.Log(
                $"[V02-Horario] npc={npc.Name} fecha={Game1.currentSeason}_{Game1.dayOfMonth}_Y{Game1.year} ({Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)}) " +
                $"hora={Game1.timeOfDay} lluvia={Game1.isRaining} | ScheduleKey={npc.ScheduleKey ?? "(null)"} " +
                $"follow={npc.followSchedule} ignoreToday={npc.ignoreScheduleToday} lastAttempted={npc.lastAttemptedSchedule} " +
                $"control={control ?? "(libre)"} | pos={ubicacion}@{npc.TilePoint} dir={npc.FacingDirection} " +
                $"enRuta={npc.controller != null} pasos={pasosRestantes} animFinRuta={npc.doingEndOfRouteAnimation.Value}",
                LogLevel.Info);

            if (npc.Schedule is null || npc.Schedule.Count == 0)
            {
                this.context.Monitor.Log($"[V02-Horario]   (sin entradas en npc.Schedule)", LogLevel.Info);
                return;
            }

            foreach (var kv in npc.Schedule.OrderBy(k => k.Key))
            {
                var d = kv.Value;
                this.context.Monitor.Log(
                    $"[V02-Horario]   {kv.Key:D4} -> {d.targetLocationName}@({d.targetTile.X},{d.targetTile.Y}) dir={d.facingDirection} " +
                    $"anim={d.endOfRouteBehavior ?? "-"} msg={d.endOfRouteMessage ?? "-"} pasosRuta={d.route?.Count ?? 0}",
                    LogLevel.Info);
            }
        }

        private void OnComandoV02Reclamar(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_reclamar.", LogLevel.Warn);
                return;
            }

            var npc = this.ObtenerNpcObjetivo(args.FirstOrDefault());
            if (npc is null)
            {
                return;
            }

            var ubicacionAntes = npc.currentLocation?.NameOrUniqueName ?? "n/d";
            var casillaAntes = npc.TilePoint;

            npc.modData[ClaveControlModData] = $"reclamado@{Game1.timeOfDay}";
            npc.ignoreScheduleToday = true;
            npc.controller = null;
            npc.temporaryController = null;
            npc.queuedSchedulePaths.Clear();
            npc.EndActivityRouteEndBehavior();
            npc.Halt();

            var ubicacionDespues = npc.currentLocation?.NameOrUniqueName ?? "n/d";
            var casillaDespues = npc.TilePoint;
            var sinTeletransporte = string.Equals(ubicacionAntes, ubicacionDespues, StringComparison.Ordinal) && casillaAntes == casillaDespues;

            this.context.Monitor.Log(
                $"[V02-Reclamar] npc={npc.Name} reclamada a las {Game1.timeOfDay:D4} | " +
                $"modData[{ClaveControlModData}]={npc.modData[ClaveControlModData]} | " +
                $"posAntes={ubicacionAntes}@{casillaAntes} posDespues={ubicacionDespues}@{casillaDespues} " +
                $"sinTeletransporte={sinTeletransporte} ScheduleConservado={npc.Schedule?.Count ?? 0} entradas (clave={npc.ScheduleKey})",
                LogLevel.Info);
        }

        private void OnComandoV02IrA(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_ir_a.", LogLevel.Warn);
                return;
            }

            if (args.Length < 3
                || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var destinoX)
                || !int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var destinoY))
            {
                this.context.Monitor.Log("[V02] Uso: cp_v02_ir_a <ubicacion> <x> <y> [orientacion] [npc]", LogLevel.Warn);
                return;
            }

            var destinoUbicacion = args[0];
            var orientacion = args.Length >= 4 && int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dir)
                ? dir
                : 2;
            var nombreNpc = args.Length >= 5 ? args[4] : null;
            var npc = this.ObtenerNpcObjetivo(nombreNpc);
            if (npc is null || npc.currentLocation is null)
            {
                return;
            }

            if (!npc.modData.ContainsKey(ClaveControlModData))
            {
                npc.modData[ClaveControlModData] = $"reclamado@{Game1.timeOfDay}";
                npc.ignoreScheduleToday = true;
            }

            var origenUbicacion = npc.currentLocation.NameOrUniqueName;
            var origenCasilla = npc.TilePoint;

            npc.EndActivityRouteEndBehavior();
            npc.Halt();

            var ruta = npc.pathfindToNextScheduleLocation(
                npc.ScheduleKey ?? "corazon",
                origenUbicacion,
                origenCasilla.X,
                origenCasilla.Y,
                destinoUbicacion,
                destinoX,
                destinoY,
                orientacion,
                null,
                null);

            var totalPasos = ruta.route?.Count ?? 0;
            npc.DirectionsToNewLocation = ruta;
            npc.controller = new PathFindController(ruta.route, npc, npc.currentLocation)
            {
                finalFacingDirection = orientacion,
                endBehaviorFunction = (c, loc) =>
                {
                    this.context?.Monitor.Log(
                        $"[V02-IrA-Fin] npc={c.Name} llegó por PathFindController a {loc?.NameOrUniqueName}@{c.TilePoint} dir={c.FacingDirection}",
                        LogLevel.Info);
                },
            };

            var sinTeletransporte = string.Equals(origenUbicacion, npc.currentLocation.NameOrUniqueName, StringComparison.Ordinal)
                && origenCasilla == npc.TilePoint;

            this.context.Monitor.Log(
                $"[V02-IrA] npc={npc.Name} iniciando ruta nativa {origenUbicacion}@({origenCasilla.X},{origenCasilla.Y}) -> " +
                $"{destinoUbicacion}@({destinoX},{destinoY}) dir={orientacion} | pasos={totalPasos} sinTeletransporte={sinTeletransporte}",
                LogLevel.Info);
        }

        private void OnComandoV02Volver(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_volver.", LogLevel.Warn);
                return;
            }

            var npc = this.ObtenerNpcObjetivo(args.FirstOrDefault());
            if (npc is null || npc.currentLocation is null)
            {
                return;
            }

            var origenUbicacion = npc.currentLocation.NameOrUniqueName;
            var origenCasilla = npc.TilePoint;

            npc.modData.Remove(ClaveControlModData);
            npc.ignoreScheduleToday = false;
            npc.followSchedule = true;
            npc.lastAttemptedSchedule = Game1.timeOfDay;
            npc.queuedSchedulePaths.Clear();
            npc.EndActivityRouteEndBehavior();
            npc.Halt();

            if (npc.Schedule is null || npc.Schedule.Count == 0)
            {
                this.context.Monitor.Log($"[V02-Volver] npc={npc.Name} liberada sin entradas en npc.Schedule.", LogLevel.Info);
                return;
            }

            var entradasOrdenadas = npc.Schedule.OrderBy(k => k.Key).ToList();
            KeyValuePair<int, SchedulePathDescription>? tramoVigente = null;
            KeyValuePair<int, SchedulePathDescription>? siguienteTramo = null;

            foreach (var kv in entradasOrdenadas)
            {
                if (kv.Key <= Game1.timeOfDay)
                {
                    tramoVigente = kv;
                }
                else if (siguienteTramo is null)
                {
                    siguienteTramo = kv;
                }
            }

            var objetivoUbicacion = tramoVigente?.Value.targetLocationName ?? npc.DefaultMap;
            var objetivoCasilla = tramoVigente?.Value.targetTile
                ?? new Point((int)(npc.DefaultPosition.X / 64f), (int)(npc.DefaultPosition.Y / 64f));
            var objetivoDir = tramoVigente?.Value.facingDirection ?? npc.DefaultFacingDirection;
            var objetivoAnim = tramoVigente?.Value.endOfRouteBehavior;
            var objetivoMsg = tramoVigente?.Value.endOfRouteMessage;
            var horaTramo = tramoVigente?.Key ?? 600;

            if (string.Equals(origenUbicacion, objetivoUbicacion, StringComparison.Ordinal) && origenCasilla == objetivoCasilla)
            {
                npc.faceDirection(objetivoDir);
                npc.previousEndPoint = objetivoCasilla;
                if (!string.IsNullOrEmpty(objetivoAnim) || !string.IsNullOrEmpty(objetivoMsg))
                {
                    npc.StartActivityRouteEndBehavior(objetivoAnim, objetivoMsg);
                }

                this.context.Monitor.Log(
                    $"[V02-Volver] npc={npc.Name} ya estaba en el destino del tramo {horaTramo:D4} ({objetivoUbicacion}@{objetivoCasilla}); reanudada sin desplazamiento.",
                    LogLevel.Info);
                return;
            }

            var rutaRetorno = npc.pathfindToNextScheduleLocation(
                npc.ScheduleKey ?? "corazon",
                origenUbicacion,
                origenCasilla.X,
                origenCasilla.Y,
                objetivoUbicacion,
                objetivoCasilla.X,
                objetivoCasilla.Y,
                objetivoDir,
                objetivoAnim,
                objetivoMsg);

            var totalPasos = rutaRetorno.route?.Count ?? 0;
            npc.DirectionsToNewLocation = rutaRetorno;
            var endBehavior = MetodoGetRouteEndBehavior?.Invoke(npc, new object?[] { objetivoAnim, objetivoMsg }) as PathFindController.endBehavior;

            npc.controller = new PathFindController(rutaRetorno.route, npc, npc.currentLocation)
            {
                finalFacingDirection = objetivoDir,
                endBehaviorFunction = (c, loc) =>
                {
                    endBehavior?.Invoke(c, loc);
                    this.context?.Monitor.Log(
                        $"[V02-Volver-Fin] npc={c.Name} reincorporada al tramo {horaTramo:D4} en {loc?.NameOrUniqueName}@{c.TilePoint} dir={c.FacingDirection} anim={objetivoAnim ?? "-"}",
                        LogLevel.Info);
                },
            };

            if (npc.controller is not null)
            {
                this.controladoresEnvueltos.Add(npc.controller);
            }

            npc.previousEndPoint = objetivoCasilla;
            var sinTeletransporte = string.Equals(origenUbicacion, npc.currentLocation.NameOrUniqueName, StringComparison.Ordinal)
                && origenCasilla == npc.TilePoint;

            this.context.Monitor.Log(
                $"[V02-Volver] npc={npc.Name} liberada a las {Game1.timeOfDay:D4} -> retornando al tramo vigente {horaTramo:D4} " +
                $"({objetivoUbicacion}@({objetivoCasilla.X},{objetivoCasilla.Y}), anim={objetivoAnim ?? "-"}, pasos={totalPasos}, " +
                $"siguienteTramo={(siguienteTramo.HasValue ? siguienteTramo.Value.Key.ToString("D4", CultureInfo.InvariantCulture) : "ninguno")}, " +
                $"sinTeletransporte={sinTeletransporte})",
                LogLevel.Info);
        }

        private void OnComandoV02Saltar(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_saltar.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var hora))
            {
                this.context.Monitor.Log("[V02] Uso: cp_v02_saltar <hora> [ubicacion x y [orientacion]]", LogLevel.Warn);
                return;
            }

            var npc = this.ObtenerNpcObjetivo(null);
            if (npc is null || npc.Schedule is null)
            {
                this.context.Monitor.Log("[V02] El NPC no tiene Schedule cargado.", LogLevel.Warn);
                return;
            }

            if (!npc.Schedule.TryGetValue(hora, out var entradaOriginal))
            {
                this.context.Monitor.Log($"[V02-Saltar] No existe entrada a las {hora:D4} en el horario de {npc.Name}.", LogLevel.Warn);
                return;
            }

            var entradasPosteriores = npc.Schedule.Keys.Where(k => k > hora).OrderBy(k => k).ToList();

            if (args.Length >= 4
                && int.TryParse(args[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nuevoX)
                && int.TryParse(args[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var nuevoY))
            {
                var nuevaUbicacion = args[1];
                var nuevaDir = args.Length >= 5 && int.TryParse(args[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out var d)
                    ? d
                    : 2;

                var origenPrevio = this.ResolverOrigenPrevioHorario(npc, hora);
                var nuevaEntrada = npc.pathfindToNextScheduleLocation(
                    npc.ScheduleKey ?? "corazon",
                    origenPrevio.Ubicacion,
                    origenPrevio.Casilla.X,
                    origenPrevio.Casilla.Y,
                    nuevaUbicacion,
                    nuevoX,
                    nuevoY,
                    nuevaDir,
                    null,
                    null);
                nuevaEntrada.time = hora;
                npc.Schedule[hora] = nuevaEntrada;

                this.RecalcularSiguienteEntrada(npc, hora, nuevaUbicacion, new Point(nuevoX, nuevoY));

                this.context.Monitor.Log(
                    $"[V02-Saltar] Sustituida entrada {hora:D4} de {npc.Name}: " +
                    $"{entradaOriginal.targetLocationName}@({entradaOriginal.targetTile.X},{entradaOriginal.targetTile.Y}) -> " +
                    $"{nuevaUbicacion}@({nuevoX},{nuevoY}) | posterioresConservadas=[{string.Join(",", entradasPosteriores.Select(h => h.ToString("D4", CultureInfo.InvariantCulture)))}]",
                    LogLevel.Info);
                return;
            }

            var origenAnterior = this.ResolverOrigenPrevioHorario(npc, hora);
            npc.Schedule.Remove(hora);
            this.RecalcularSiguienteEntrada(npc, hora, origenAnterior.Ubicacion, origenAnterior.Casilla);

            this.context.Monitor.Log(
                $"[V02-Saltar] Eliminada entrada {hora:D4} ({entradaOriginal.targetLocationName}@({entradaOriginal.targetTile.X},{entradaOriginal.targetTile.Y})) de {npc.Name} | " +
                $"se queda en {origenAnterior.Ubicacion}@({origenAnterior.Casilla.X},{origenAnterior.Casilla.Y}) | " +
                $"posterioresConservadas=[{string.Join(",", entradasPosteriores.Select(h => h.ToString("D4", CultureInfo.InvariantCulture)))}]",
                LogLevel.Info);
        }

        private void OnComandoV02Escenario(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_escenario.", LogLevel.Warn);
                return;
            }

            if (args.Length < 3 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var dia))
            {
                this.context.Monitor.Log("[V02] Uso: cp_v02_escenario <spring|summer|fall|winter> <dia> <sol|lluvia>", LogLevel.Warn);
                return;
            }

            var estacion = args[0].ToLowerInvariant();
            var lluvia = string.Equals(args[2], "lluvia", StringComparison.OrdinalIgnoreCase)
                || string.Equals(args[2], "rain", StringComparison.OrdinalIgnoreCase);

            var npc = this.ObtenerNpcObjetivo(null);
            if (npc is null)
            {
                return;
            }

            Game1.currentSeason = estacion;
            Game1.dayOfMonth = dia;
            Game1.isRaining = lluvia;
            if (Game1.netWorldState?.Value is not null)
            {
                Game1.netWorldState.Value.GetWeatherForLocation("Default").IsRaining = lluvia;
                Game1.netWorldState.Value.UpdateFromGame1();
            }

            this.SincronizarOffsetPelican4Ever(600);
            Game1.timeOfDay = 600;
            this.controladoresEnvueltos.Clear();

            npc.modData.Remove(ClaveControlModData);
            npc.ignoreScheduleToday = false;
            npc.followSchedule = true;
            npc.lastAttemptedSchedule = -1;
            npc.dayUpdate(dia);
            npc.resetForNewDay(dia);
            npc.lastAttemptedSchedule = -1;

            this.context.Monitor.Log(
                $"[V02-Escenario] Configurado {estacion} día {dia} ({Game1.shortDayNameFromDayOfSeason(dia)}) lluvia={lluvia} hora=0600 -> " +
                $"npc={npc.Name} ScheduleKey={npc.ScheduleKey ?? "(null)"} entradas={npc.Schedule?.Count ?? 0} pos={npc.currentLocation?.NameOrUniqueName}@{npc.TilePoint}",
                LogLevel.Info);

            this.OnComandoV02Horario("cp_v02_horario", Array.Empty<string>());
        }

        private void OnComandoV02Reloj(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_reloj.", LogLevel.Warn);
                return;
            }

            if (args.Length < 1 || !int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var horaDestino))
            {
                this.context.Monitor.Log("[V02] Uso: cp_v02_reloj <HHMM> [caminar]", LogLevel.Warn);
                return;
            }

            var caminar = args.Length >= 2 && string.Equals(args[1], "caminar", StringComparison.OrdinalIgnoreCase);
            var npc = this.ObtenerNpcObjetivo(null);
            if (npc is null)
            {
                return;
            }

            var horaInicial = Game1.timeOfDay;
            this.SincronizarOffsetPelican4Ever(horaDestino);

            if (horaDestino <= Game1.timeOfDay)
            {
                Game1.timeOfDay = horaDestino;
                Game1.netWorldState?.Value?.UpdateFromGame1();
                npc.checkSchedule(Game1.timeOfDay);
            }
            else
            {
                var seguridad = 0;
                while (Game1.timeOfDay < horaDestino && seguridad < 200)
                {
                    seguridad++;
                    Game1.timeOfDay = Utility.ModifyTime(Game1.timeOfDay, 10);
                    Game1.netWorldState?.Value?.UpdateFromGame1();
                    npc.checkSchedule(Game1.timeOfDay);
                    if (caminar && npc.controller is not null)
                    {
                        this.SimularMarchaNativa(npc, 6000);
                    }
                }
            }

            if (caminar && npc.controller is not null)
            {
                this.SimularMarchaNativa(npc, 6000);
            }

            this.context.Monitor.Log(
                $"[V02-Reloj] Reloj {horaInicial:D4} -> {Game1.timeOfDay:D4} | npc={npc.Name} pos={npc.currentLocation?.NameOrUniqueName}@{npc.TilePoint} " +
                $"enRuta={npc.controller != null} lastAttempted={npc.lastAttemptedSchedule} animFin={npc.doingEndOfRouteAnimation.Value}",
                LogLevel.Info);
        }

        private void OnComandoV02Caminar(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady)
            {
                this.context?.Monitor.Log("[V02] Carga primero una partida antes de ejecutar cp_v02_caminar.", LogLevel.Warn);
                return;
            }

            var maxTicks = args.Length >= 1 && int.TryParse(args[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
                ? m
                : 6000;
            var nombreNpc = args.Length >= 2 ? args[1] : null;
            var npc = this.ObtenerNpcObjetivo(nombreNpc);
            if (npc is null)
            {
                return;
            }

            this.SimularMarchaNativa(npc, maxTicks);
        }

        private void SimularMarchaNativa(NPC npc, int maxTicks)
        {
            if (npc.controller?.pathToEndPoint is not { Count: > 0 } || Game1.currentGameTime is null)
            {
                this.context?.Monitor.Log($"[V02-Caminar] npc={npc.Name} no tiene pasos pendientes en PathFindController.", LogLevel.Info);
                return;
            }

            var origenLoc = npc.currentLocation?.NameOrUniqueName ?? "n/d";
            var origenTile = npc.TilePoint;
            var ubicacionesVisitadas = new List<string> { $"{origenLoc}@{origenTile}" };
            var ultimaLoc = origenLoc;
            var ticks = 0;

            while (npc.controller?.pathToEndPoint is { Count: > 0 } && ticks < maxTicks && npc.currentLocation is not null)
            {
                ticks++;
                npc.update(Game1.currentGameTime, npc.currentLocation);
                var locActual = npc.currentLocation?.NameOrUniqueName ?? "n/d";
                if (!string.Equals(locActual, ultimaLoc, StringComparison.Ordinal))
                {
                    ubicacionesVisitadas.Add($"{locActual}@{npc.TilePoint}");
                    ultimaLoc = locActual;
                }
            }

            var completado = npc.controller?.pathToEndPoint is not { Count: > 0 };
            if (completado && !npc.doingEndOfRouteAnimation.Value)
            {
                npc.controller = null;
            }

            ubicacionesVisitadas.Add($"{npc.currentLocation?.NameOrUniqueName}@{npc.TilePoint}");
            this.context?.Monitor.Log(
                $"[V02-Caminar] npc={npc.Name} ticks={ticks} completado={completado} " +
                $"recorrido=[{string.Join(" -> ", ubicacionesVisitadas)}] dir={npc.FacingDirection} animFin={npc.doingEndOfRouteAnimation.Value}",
                LogLevel.Info);
        }

        private void SincronizarOffsetPelican4Ever(int horaJuegoHHMM)
        {
            try
            {
                var tipoRtc = AccessTools.TypeByName("Pelican4Ever.Modules.RealTimeClockModule");
                var propOffset = tipoRtc is not null ? AccessTools.Property(tipoRtc, "ManualTimeOffset") : null;
                if (propOffset is null)
                {
                    return;
                }

                var horas = (horaJuegoHHMM / 100) % 24;
                var minutos = horaJuegoHHMM % 100;
                var objetivo = new TimeSpan(horas, minutos, 0);
                var tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");
                var ahoraLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
                propOffset.SetValue(null, objetivo - ahoraLocal.TimeOfDay);
            }
            catch
            {
                // Ignorar si Pelican 4Ever no está presente.
            }
        }

        private (string Ubicacion, Point Casilla) ResolverOrigenPrevioHorario(NPC npc, int hora)
        {
            if (npc.Schedule is not null)
            {
                var previas = npc.Schedule.Where(kv => kv.Key < hora).OrderBy(kv => kv.Key).ToList();
                if (previas.Count > 0)
                {
                    var ultima = previas[^1].Value;
                    return (ultima.targetLocationName, ultima.targetTile);
                }
            }

            return (
                npc.currentLocation?.NameOrUniqueName ?? npc.DefaultMap,
                npc.TilePoint != Point.Zero ? npc.TilePoint : new Point((int)(npc.DefaultPosition.X / 64f), (int)(npc.DefaultPosition.Y / 64f)));
        }

        private void RecalcularSiguienteEntrada(NPC npc, int horaModificada, string desdeUbicacion, Point desdeCasilla)
        {
            if (npc.Schedule is null)
            {
                return;
            }

            var siguientes = npc.Schedule.Where(kv => kv.Key > horaModificada).OrderBy(kv => kv.Key).ToList();
            if (siguientes.Count == 0)
            {
                return;
            }

            var sigKey = siguientes[0].Key;
            var sigVal = siguientes[0].Value;
            var recalculada = npc.pathfindToNextScheduleLocation(
                npc.ScheduleKey ?? "corazon",
                desdeUbicacion,
                desdeCasilla.X,
                desdeCasilla.Y,
                sigVal.targetLocationName,
                sigVal.targetTile.X,
                sigVal.targetTile.Y,
                sigVal.facingDirection,
                sigVal.endOfRouteBehavior,
                sigVal.endOfRouteMessage);
            recalculada.time = sigKey;
            npc.Schedule[sigKey] = recalculada;
        }

        private NPC? ObtenerNpcObjetivo(string? arg)
        {
            var nombre = this.ResolverNombreNpcObjetivo(arg);
            if (string.IsNullOrWhiteSpace(nombre))
            {
                this.context?.Monitor.Log("[V02] No hay NPC objetivo especificado ni gobernado.", LogLevel.Warn);
                return null;
            }

            var npc = Game1.getCharacterFromName(nombre);
            if (npc is null)
            {
                this.context?.Monitor.Log($"[V02] No se encontró el NPC '{nombre}' en la partida.", LogLevel.Warn);
            }

            return npc;
        }

        private string? ResolverNombreNpcObjetivo(string? arg)
        {
            if (!string.IsNullOrWhiteSpace(arg))
            {
                return arg;
            }

            return this.context?.PersonajesGobernados.Values.FirstOrDefault()?.Npc;
        }

        private bool EsNpcGobernado(NPC npc)
        {
            if (this.context is null || !npc.IsVillager)
            {
                return false;
            }

            foreach (var perfil in this.context.PersonajesGobernados.Values)
            {
                if (string.Equals(perfil.Npc, npc.Name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool PrefixCheckSchedule(NPC __instance, int timeOfDay)
        {
            var instancia = instanciaActiva;
            if (instancia is null || !instancia.v02MonitorActivo || enCheckSchedule || !Context.IsWorldReady || __instance is null)
            {
                return true;
            }

            if (!instancia.EsNpcGobernado(__instance))
            {
                return true;
            }

            enCheckSchedule = true;
            try
            {
                // Si el NPC está reclamado por Corazón Pelican, congelar el avance de checkSchedule sin borrar Schedule.
                if (__instance.modData.TryGetValue(ClaveControlModData, out var control))
                {
                    __instance.ignoreScheduleToday = true;
                    if (__instance.lastAttemptedSchedule < timeOfDay)
                    {
                        __instance.lastAttemptedSchedule = timeOfDay;
                        if (__instance.Schedule is not null && __instance.Schedule.TryGetValue(timeOfDay, out var entradaOmitida))
                        {
                            instancia.context?.Monitor.Log(
                                $"[V02] Entrada {timeOfDay:D4} ({entradaOmitida.targetLocationName}@{entradaOmitida.targetTile}) omitida mientras {__instance.Name} está reclamada ({control}).",
                                LogLevel.Info);
                        }
                    }

                    return false;
                }

                // Si llega una hora con entrada en Schedule y el NPC parte de una casilla distinta a la prevista al las 06:00,
                // recalcular la ruta desde su posición real para que nunca falle ni camine desde coordenadas desfasadas.
                if (!__instance.ignoreScheduleToday
                    && __instance.Schedule is not null
                    && __instance.lastAttemptedSchedule < timeOfDay
                    && __instance.Schedule.TryGetValue(timeOfDay, out var desc)
                    && __instance.currentLocation is not null)
                {
                    var origenReal = __instance.TilePoint;
                    var primerPaso = desc.route is not null && desc.route.Count > 0 ? desc.route.Peek() : origenReal;
                    var dist = Math.Abs(primerPaso.X - origenReal.X) + Math.Abs(primerPaso.Y - origenReal.Y);
                    if (dist > 1)
                    {
                        var recalculada = __instance.pathfindToNextScheduleLocation(
                            __instance.ScheduleKey ?? "corazon",
                            __instance.currentLocation.NameOrUniqueName,
                            origenReal.X,
                            origenReal.Y,
                            desc.targetLocationName,
                            desc.targetTile.X,
                            desc.targetTile.Y,
                            desc.facingDirection,
                            desc.endOfRouteBehavior,
                            desc.endOfRouteMessage);
                        recalculada.time = desc.time > 0 ? desc.time : timeOfDay;
                        __instance.Schedule[timeOfDay] = recalculada;
                        instancia.context?.Monitor.Log(
                            $"[V02] Ruta de entrada {timeOfDay:D4} recalculada desde posición real {__instance.currentLocation.NameOrUniqueName}@{origenReal} -> {desc.targetLocationName}@{desc.targetTile} (pasos={recalculada.route?.Count ?? 0}).",
                            LogLevel.Info);
                    }
                }

                return true;
            }
            finally
            {
                enCheckSchedule = false;
            }
        }

        private static void PostfixCheckSchedule(NPC __instance, int timeOfDay)
        {
            var instancia = instanciaActiva;
            if (instancia is null || !instancia.v02MonitorActivo || enCheckSchedule || !Context.IsWorldReady || __instance is null)
            {
                return;
            }

            if (!instancia.EsNpcGobernado(__instance))
            {
                return;
            }

            enCheckSchedule = true;
            try
            {
                var ctrl = __instance.controller;
                var dir = __instance.DirectionsToNewLocation;
                if (ctrl is null || dir is null || instancia.controladoresEnvueltos.Contains(ctrl))
                {
                    return;
                }

                instancia.controladoresEnvueltos.Add(ctrl);
                var claveHorario = __instance.ScheduleKey ?? "(null)";
                var horaEntrada = dir.time > 0 ? dir.time : timeOfDay;
                var destinoLoc = dir.targetLocationName ?? "(desconocido)";
                var destinoTile = dir.targetTile;
                var orientacion = dir.facingDirection;
                var animacion = dir.endOfRouteBehavior ?? "-";
                var mensaje = dir.endOfRouteMessage ?? "-";
                var comportamientoAnterior = ctrl.endBehaviorFunction;

                instancia.context?.Monitor.Log(
                    $"[V02-HitoInicio] npc={__instance.Name} clave={claveHorario} hora={horaEntrada:D4} -> destino={destinoLoc}@({destinoTile.X},{destinoTile.Y}) dir={orientacion} anim={animacion} pasos={ctrl.pathToEndPoint?.Count ?? 0}",
                    LogLevel.Info);

                ctrl.endBehaviorFunction = (c, loc) =>
                {
                    comportamientoAnterior?.Invoke(c, loc);
                    instancia.context?.Monitor.Log(
                        $"[V02-HitoFin] npc={c.Name} clave={claveHorario} hora={horaEntrada:D4} destino={destinoLoc}@({destinoTile.X},{destinoTile.Y}) posReal={loc?.NameOrUniqueName}@{c.TilePoint} dir={orientacion} anim={animacion} msg={mensaje}",
                        LogLevel.Info);
                };
            }
            finally
            {
                enCheckSchedule = false;
            }
        }

        private static bool PrefixCheckAction(NPC __instance, Farmer who, GameLocation l, ref bool __result)
        {
            var instancia = instanciaActiva;
            if (instancia is null || !instancia.v01Activo || enPrefixCheckAction)
            {
                return true;
            }

            if (!Context.IsWorldReady || who is null || !who.IsLocalPlayer || __instance is null)
            {
                return true;
            }

            if (!instancia.EsNpcGobernado(__instance))
            {
                return true;
            }

            enPrefixCheckAction = true;
            try
            {
                // 1. Guardas vanilla iniciales de NPC.checkAction (NPC.cs:2467-2483, 2669, 873).
                if (__instance.IsInvisible
                    || __instance.isSleeping.Value
                    || !who.CanMove
                    || __instance.SimpleNonVillagerNPC
                    || !__instance.canTalk())
                {
                    instancia.context?.Monitor.Log(
                        $"[V01] Paso a vanilla por guarda inicial (npc={__instance.Name}, inv={__instance.IsInvisible}, dorm={__instance.isSleeping.Value}, canMove={who.CanMove}).",
                        LogLevel.Info);
                    return true;
                }

                // 2. Eventos y festivales los gobiernan GameLocation.checkAction / Event / Festival.
                if (Game1.CurrentEvent != null || Game1.isFestival())
                {
                    instancia.context?.Monitor.Log(
                        $"[V01] Paso a vanilla por evento/festival activo (npc={__instance.Name}).",
                        LogLevel.Info);
                    return true;
                }

                // 3. Inicialización de entrada de amistad al primer trato (NPC.cs:2512-2514).
                if (__instance.CanReceiveGifts() && !who.friendshipData.ContainsKey(__instance.Name))
                {
                    who.friendshipData.Add(__instance.Name, new Friendship(0));
                }

                who.friendshipData.TryGetValue(__instance.Name, out var amistadAntes);
                var puntosAntes = amistadAntes?.Points ?? -1;
                var habladoAntes = amistadAntes?.TalkedToToday ?? false;
                var regalosAntes = amistadAntes?.GiftsToday ?? 0;
                var porSaludarAntes = ObtenerPorSaludar(who, __instance.Name);

                // 4. Misiones con diálogo propio al socializar (NPC.cs:2522-2526).
                if (who.NotifyQuests(quest => quest.OnNpcSocialized(__instance)) && Game1.dialogueUp)
                {
                    __instance.faceTowardFarmerForPeriod(6000, 3, faceAway: false, who);
                    instancia.context?.Monitor.Log(
                        $"[V01] Misión tomó control con diálogo propio (npc={__instance.Name}).",
                        LogLevel.Info);
                    __result = true;
                    return false;
                }

                // 5. Ramas de cónyuge: stardrop, beso matinal o diálogo matrimonial (NPC.cs:2569-2668, 2726-2739).
                if (string.Equals(__instance.Name, who.spouse, StringComparison.Ordinal))
                {
                    var tieneStardrop = amistadAntes is not null && amistadAntes.Points >= 3125 && !who.mailReceived.Contains("CF_Spouse");
                    var candidatoBeso = __instance.Sprite.CurrentAnimation == null
                        && !__instance.hasTemporaryMessageAvailable()
                        && __instance.currentMarriageDialogue.Count == 0
                        && __instance.CurrentDialogue.Count == 0
                        && Game1.timeOfDay < 2200
                        && !__instance.isMoving()
                        && who.ActiveObject == null;
                    var tieneDialogoMatrimonial = __instance.shouldSayMarriageDialogue.Value && __instance.currentMarriageDialogue.Count > 0;

                    if (tieneStardrop || candidatoBeso || tieneDialogoMatrimonial)
                    {
                        instancia.context?.Monitor.Log(
                            $"[V01] Paso a vanilla por rama de cónyuge (npc={__instance.Name}, stardrop={tieneStardrop}, beso={candidatoBeso}, mat={tieneDialogoMatrimonial}).",
                            LogLevel.Info);
                        return true;
                    }
                }

                // 6. Entrega de objeto activo: regalos, ramos, amuletos o entregas de misión (NPC.cs:2761, 2782).
                if (who.ActiveObject != null && !who.isRidingHorse() && __instance.tryToReceiveActiveObject(who, probe: true))
                {
                    instancia.context?.Monitor.Log(
                        $"[V01] Paso a vanilla por regalo/objeto activo (npc={__instance.Name}, item={who.ActiveObject.QualifiedItemId}).",
                        LogLevel.Info);
                    return true;
                }

                // 7. Cooldown silencioso de mensaje temporal (NPC.cs:4335).
                if (__instance.isOnSilentTemporaryMessage())
                {
                    instancia.context?.Monitor.Log(
                        $"[V01] Paso a vanilla por mensaje temporal silencioso (npc={__instance.Name}).",
                        LogLevel.Info);
                    return true;
                }

                // 8. Conversación ordinaria interceptada conservando amistad, facing y progreso de misiones.
                var textura = __instance.Sprite.Texture;
                if (textura != null && textura.Bounds.Height > 32)
                {
                    __instance.faceTowardFarmerForPeriod(5000, 4, faceAway: false, who);
                }

                __instance.grantConversationFriendship(who);

                __instance.CurrentDialogue.Clear();
                __instance.CurrentDialogue.Push(new Dialogue(__instance, null, instancia.v01Texto));
                Game1.drawDialogue(__instance);

                who.friendshipData.TryGetValue(__instance.Name, out var amistadDespues);
                var puntosDespues = amistadDespues?.Points ?? -1;
                var habladoDespues = amistadDespues?.TalkedToToday ?? false;
                var porSaludarDespues = ObtenerPorSaludar(who, __instance.Name);

                instancia.context?.Monitor.Log(
                    $"[V01] Conversación interceptada npc={__instance.Name} | " +
                    $"amistad {puntosAntes}->{puntosDespues} | habladoHoy {habladoAntes}->{habladoDespues} | " +
                    $"regalosHoy={regalosAntes} | enSocializeAntes={porSaludarAntes} enSocializeDespues={porSaludarDespues} | " +
                    $"dialogueUp={Game1.dialogueUp} texto=\"{instancia.v01Texto}\"",
                    LogLevel.Info);

                __result = true;
                return false;
            }
            finally
            {
                enPrefixCheckAction = false;
            }
        }

        private static bool ObtenerPorSaludar(Farmer who, string npcName)
        {
            foreach (var quest in who.questLog)
            {
                if (quest is SocializeQuest socialize && socialize.whoToGreet.Contains(npcName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
