using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CorazonPelican.Adaptador;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Framework;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Módulo de diálogo (GSS §8, ADS §8, R-12, CP-010, CP-503):
    /// intercepta la conversación ordinaria en <c>NPC.checkAction</c> conservando amistad (+20), regalos y misiones,
    /// muestra el reflejo inmediato en &lt; 300 ms y espera la frase pensada o cae a vanilla si caduca.
    /// </summary>
    internal sealed class DialogoModule : IModule, IDisposable
    {
        private static readonly string[] PosiblesIdsValleyTalk =
        {
            "ValleyTalk",
            "purrplingcat.ValleyTalk",
            "Satozaki.ValleyTalk",
        };

        private static DialogoModule? instanciaActiva;

        [ThreadStatic]
        private static bool enPrefixCheckAction;

        private readonly Dictionary<string, TurnoEnVivo> turnosActivos = new(StringComparer.Ordinal);
        private ModContext? context;
        private Harmony? harmony;
        private bool parchesAplicados;

        private sealed class TurnoEnVivo
        {
            public string PersonajeId { get; init; } = "";
            public string NpcName { get; init; } = "";
            public string InterlocutorRef { get; init; } = "";
            public string Ubicacion { get; init; } = "";
            public long? PulsoId { get; init; }
        }

        public string Nombre => "Dialogo";

        public LadoModulo Lado => LadoModulo.Ambos;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;
            instanciaActiva = this;
            this.AplicarParches();

            contexto.Helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            contexto.Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            contexto.Helper.ConsoleCommands.Add(
                "cp_dialogo",
                "Inspecciona o prueba turnos de DialogoModule en vivo: cp_dialogo <estado|simular_respuesta <texto>|forzar_caducidad>.",
                this.OnComandoDialogo);
        }

        public void Dispose()
        {
            if (this.context is not null)
            {
                this.context.Helper.Events.GameLoop.UpdateTicked -= this.OnUpdateTicked;
                this.context.Helper.Events.GameLoop.ReturnedToTitle -= this.OnReturnedToTitle;
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

            this.turnosActivos.Clear();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
            => this.turnosActivos.Clear();

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

            if (metodoCheckAction is null)
            {
                this.context.Monitor.Log("[DialogoModule] No se encontró NPC.checkAction(Farmer, GameLocation).", LogLevel.Error);
                return;
            }

            this.harmony = new Harmony($"{this.context.Manifest.UniqueID}.Dialogo");
            this.harmony.Patch(
                original: metodoCheckAction,
                prefix: new HarmonyMethod(typeof(DialogoModule), nameof(PrefixCheckAction)));
            this.parchesAplicados = true;
            this.context.Monitor.Log("[DialogoModule] Prefijo sobre NPC.checkAction registrado.", LogLevel.Trace);
        }

        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || this.turnosActivos.Count == 0)
            {
                return;
            }

            var ahoraMs = this.context.RelojMs?.Invoke() ?? 0;
            var dia = Math.Max(1, Game1.Date?.TotalDays ?? 1);
            var hora = Game1.timeOfDay;

            foreach (var personajeId in this.turnosActivos.Keys.ToList())
            {
                if (!this.turnosActivos.TryGetValue(personajeId, out var turno)
                    || !this.context.MotoresHabitante.TryGetValue(personajeId, out var motor))
                {
                    this.turnosActivos.Remove(personajeId);
                    continue;
                }

                var npc = Game1.getCharacterFromName(turno.NpcName);
                var presente = EstaInterlocutorPresente(npc, Game1.player);

                var res = motor.EvaluarTickDialogo(ahoraMs, presente, dia, hora, turno.InterlocutorRef);
                if (res.Desenlace == DesenlaceTurnoDialogo.CaducadoCaerAVanilla)
                {
                    this.turnosActivos.Remove(personajeId);
                    this.MostrarCaidaAVanilla(npc, turno);
                }
                else if (!motor.Dialogo.HayTurnoActivo)
                {
                    this.turnosActivos.Remove(personajeId);
                }
            }
        }

        private void OnComandoDialogo(string comando, string[] args)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer)
            {
                this.context?.Monitor.Log("[Dialogo] Carga una partida en el anfitrión primero.", LogLevel.Warn);
                return;
            }

            var sub = args.Length > 0 ? args[0].ToLowerInvariant() : "estado";
            if (sub == "estado")
            {
                foreach (var (id, motor) in this.context.MotoresHabitante)
                {
                    this.context.Monitor.Log(
                        $"[Dialogo:{id}] menteHabilitada={motor.MenteHabilitada} menteCaida={motor.EstaMenteCaida} " +
                        $"turnoActivo={motor.Dialogo.HayTurnoActivo} pulsoId={motor.Dialogo.PulsoTurnoActivoId?.ToString() ?? "-"} " +
                        $"valleyTalk={this.EstaValleyTalkActivo()}",
                        LogLevel.Info);
                }

                return;
            }

            if (sub == "simular_respuesta" && args.Length >= 2)
            {
                var texto = string.Join(' ', args.Skip(1));
                var primero = this.turnosActivos.Values.FirstOrDefault();
                if (primero is null || !this.context.MotoresHabitante.TryGetValue(primero.PersonajeId, out var motor) || primero.PulsoId is null)
                {
                    this.context.Monitor.Log("[Dialogo] No hay turno activo esperando respuesta.", LogLevel.Warn);
                    return;
                }

                var resultado = this.context.Api.ProcesarRespuestaEnHiloPrincipal(new Respuesta
                {
                    Epoca = this.context.Api.EpocaVigente ?? string.Empty,
                    BasadaEn = primero.PulsoId.Value,
                    Pensamiento = "Respuesta simulada en vivo para CP-503.",
                    Frase = texto,
                });

                this.context.Monitor.Log($"[Dialogo] Respuesta aplicada: estado={resultado.Estado}", LogLevel.Info);
                return;
            }

            this.context.Monitor.Log("[Dialogo] Uso: cp_dialogo estado | cp_dialogo simular_respuesta <texto>", LogLevel.Warn);
        }

        private bool EstaValleyTalkActivo()
        {
            if (this.context is null)
            {
                return false;
            }

            return PosiblesIdsValleyTalk.Any(id => this.context.Helper.ModRegistry.IsLoaded(id));
        }

        private void SuscribirResultadoDialogoSiFalta(string personajeId, MotorHabitante motor)
        {
            motor.ResultadoDialogoDisponible -= this.OnResultadoDialogoDisponible;
            motor.ResultadoDialogoDisponible += this.OnResultadoDialogoDisponible;
        }

        private void OnResultadoDialogoDisponible(DesenlaceDialogoTurno res)
        {
            if (this.context is null)
            {
                return;
            }

            if (res.Desenlace == DesenlaceTurnoDialogo.MostrarFrasePensada && !string.IsNullOrWhiteSpace(res.Frase))
            {
                var primerTurno = this.turnosActivos.Values.FirstOrDefault();
                if (primerTurno is null)
                {
                    return;
                }

                var npc = Game1.getCharacterFromName(primerTurno.NpcName);
                if (npc is not null)
                {
                    if (Game1.activeClickableMenu is DialogueBox)
                    {
                        Game1.exitActiveMenu();
                    }

                    npc.CurrentDialogue.Clear();
                    npc.CurrentDialogue.Push(new Dialogue(npc, null, res.Frase));
                    Game1.drawDialogue(npc);
                    this.context.Monitor.Log(
                        $"[Dialogo:{primerTurno.PersonajeId}] Frase pensada mostrada para {npc.Name}: \"{res.Frase}\"",
                        LogLevel.Info);
                }

                this.turnosActivos.Remove(primerTurno.PersonajeId);
            }
        }

        private void MostrarCaidaAVanilla(NPC? npc, TurnoEnVivo turno)
        {
            if (npc is null)
            {
                return;
            }

            npc.doEmote(GestorDialogo.EmoteCaducado);
            if (Game1.activeClickableMenu is DialogueBox)
            {
                Game1.exitActiveMenu();
            }

            npc.CurrentDialogue.Clear();
            if (Game1.player is not null && Game1.player.friendshipData.TryGetValue(npc.Name, out var amistad))
            {
                var corazones = amistad.Points / 250;
                if (!npc.checkForNewCurrentDialogue(corazones))
                {
                    npc.checkForNewCurrentDialogue(corazones, noPreface: true);
                }
            }

            if (npc.CurrentDialogue.Count == 0)
            {
                npc.CurrentDialogue.Push(new Dialogue(npc, null, GestorDialogo.TextoCaducado));
            }

            Game1.drawDialogue(npc);
            this.context?.Monitor.Log(
                $"[Dialogo:{turno.PersonajeId}] Pulso caducado -> caída a diálogo vanilla para {npc.Name}.",
                LogLevel.Info);
        }

        private static bool EstaInterlocutorPresente(NPC? npc, Farmer? player)
        {
            if (npc?.currentLocation is null || player?.currentLocation is null)
            {
                return false;
            }

            if (!string.Equals(npc.currentLocation.NameOrUniqueName, player.currentLocation.NameOrUniqueName, StringComparison.Ordinal))
            {
                return false;
            }

            var dist = Math.Abs(npc.TilePoint.X - player.TilePoint.X) + Math.Abs(npc.TilePoint.Y - player.TilePoint.Y);
            return dist <= 4;
        }

        private static bool PrefixCheckAction(NPC __instance, Farmer who, GameLocation l, ref bool __result)
        {
            var instancia = instanciaActiva;
            if (instancia?.context is null || enPrefixCheckAction || !Context.IsWorldReady || who is null || !who.IsLocalPlayer || __instance is null)
            {
                return true;
            }

            if (!instancia.context.EstaActivo || !Context.IsMainPlayer || !instancia.context.MotorActivoEnPartida)
            {
                return true;
            }

            string? personajeId = null;
            foreach (var (id, perfil) in instancia.context.PersonajesGobernados)
            {
                if (string.Equals(perfil.Npc, __instance.Name, StringComparison.Ordinal))
                {
                    personajeId = id;
                    break;
                }
            }

            if (personajeId is null || !instancia.context.MotoresHabitante.TryGetValue(personajeId, out var motor))
            {
                return true;
            }

            var enEvento = Game1.CurrentEvent != null || Game1.isFestival();
            var decision = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
                motor.MenteHabilitada && instancia.context.Config.MenteHabilitada,
                motor.EstaMenteCaida,
                enEvento,
                instancia.EstaValleyTalkActivo()));

            if (!decision.Interceptar)
            {
                return true;
            }

            enPrefixCheckAction = true;
            try
            {
                // 1. Guardas iniciales vanilla (NPC.cs:2467-2483, 2669, 873).
                if (__instance.IsInvisible
                    || __instance.isSleeping.Value
                    || !who.CanMove
                    || __instance.SimpleNonVillagerNPC
                    || !__instance.canTalk())
                {
                    return true;
                }

                // 2. Primer contacto: inicializar entrada de amistad (NPC.cs:2512-2514).
                if (__instance.CanReceiveGifts() && !who.friendshipData.ContainsKey(__instance.Name))
                {
                    who.friendshipData.Add(__instance.Name, new Friendship(0));
                }

                // 3. Misiones con diálogo propio al socializar (NPC.cs:2522-2526).
                if (who.NotifyQuests(quest => quest.OnNpcSocialized(__instance)) && Game1.dialogueUp)
                {
                    __instance.faceTowardFarmerForPeriod(6000, 3, faceAway: false, who);
                    __result = true;
                    return false;
                }

                // 4. Cónyuge: Stardrop, beso matinal o diálogo matrimonial (NPC.cs:2569-2668, 2726-2739).
                who.friendshipData.TryGetValue(__instance.Name, out var amistad);
                if (string.Equals(__instance.Name, who.spouse, StringComparison.Ordinal))
                {
                    var tieneStardrop = amistad is not null && amistad.Points >= 3125 && !who.mailReceived.Contains("CF_Spouse");
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
                        return true;
                    }
                }

                // 5. Regalos, ramos, colgantes y entregas de misión (NPC.cs:1713-1775, 2761, 2782).
                if (who.ActiveObject != null && !who.isRidingHorse() && __instance.tryToReceiveActiveObject(who, probe: true))
                {
                    return true;
                }

                // 6. Enfriamiento de mensaje silencioso temporal (NPC.cs:4335).
                if (__instance.isOnSilentTemporaryMessage())
                {
                    return true;
                }

                // 7. Reflejo inmediato (< 300 ms) + pulso de interlocución con caducidad.
                var crono = Stopwatch.StartNew();

                var textura = __instance.Sprite.Texture;
                if (textura != null && textura.Bounds.Height > 32)
                {
                    __instance.faceTowardFarmerForPeriod(5000, 4, faceAway: false, who);
                }

                __instance.grantConversationFriendship(who);

                var obs = instancia.context.ProveedorObservacion?.Invoke(personajeId)
                    ?? motor.UltimaObservacion
                    ?? new Observacion(
                        Math.Max(1, Game1.Date?.TotalDays ?? 1),
                        Game1.timeOfDay,
                        __instance.currentLocation?.NameOrUniqueName ?? string.Empty,
                        __instance.TilePoint.X,
                        __instance.TilePoint.Y,
                        1.0,
                        "soleado",
                        Array.Empty<EntidadObservada>());

                var ahoraMs = instancia.context.RelojMs?.Invoke() ?? 0;
                var interlocutorRef = $"farmer:{who.UniqueMultiplayerID}";
                instancia.SuscribirResultadoDialogoSiFalta(personajeId, motor);

                var reaccion = motor.IniciarTurnoDialogo(
                    obs,
                    interlocutorRef,
                    who.Name,
                    ahoraMs,
                    reaccionMs: crono.Elapsed.TotalMilliseconds,
                    enEvento: enEvento,
                    valleyTalkActivo: instancia.EstaValleyTalkActivo());

                if (!string.IsNullOrWhiteSpace(reaccion.FraseAnticipada))
                {
                    __instance.CurrentDialogue.Clear();
                    __instance.CurrentDialogue.Push(new Dialogue(__instance, null, reaccion.FraseAnticipada));
                    Game1.drawDialogue(__instance);
                    crono.Stop();

                    instancia.context.Monitor.Log(
                        $"[Dialogo:{personajeId}] Respuesta anticipada entregada en {crono.Elapsed.TotalMilliseconds:0.##} ms | " +
                        $"pulso={reaccion.Pulso?.Id}",
                        LogLevel.Info);

                    __result = true;
                    return false;
                }

                __instance.doEmote(GestorDialogo.EmotePensando);
                __instance.CurrentDialogue.Clear();
                __instance.CurrentDialogue.Push(new Dialogue(__instance, null, reaccion.LineaBreve));
                Game1.drawDialogue(__instance);
                crono.Stop();

                instancia.turnosActivos[personajeId] = new TurnoEnVivo
                {
                    PersonajeId = personajeId,
                    NpcName = __instance.Name,
                    InterlocutorRef = interlocutorRef,
                    Ubicacion = __instance.currentLocation?.NameOrUniqueName ?? string.Empty,
                    PulsoId = reaccion.Pulso?.Id,
                };

                instancia.context.Monitor.Log(
                    $"[Dialogo:{personajeId}] Reflejo visible en {crono.Elapsed.TotalMilliseconds:0.##} ms | " +
                    $"pulso={reaccion.Pulso?.Id} anticipado={reaccion.ReutilizoAnticipado} caduca={reaccion.CaducaEnMs} ms",
                    LogLevel.Info);

                __result = true;
                return false;
            }
            finally
            {
                enPrefixCheckAction = false;
            }
        }
    }
}
