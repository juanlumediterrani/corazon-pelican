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
        private const string ClaveDialogoActivoModData = "CorazonPelican.DialogoActivo";

        private static DialogoModule? instanciaActiva;

        [ThreadStatic]
        private static bool enPrefixCheckAction;

        private readonly Dictionary<string, TurnoEnVivo> turnosActivos = new(StringComparer.Ordinal);
        private readonly HashSet<string> avisosValleyTalkEmitidos = new(StringComparer.Ordinal);
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
            public long TargetPlayerId { get; init; }
        }

        public string Nombre => "Dialogo";

        public LadoModulo Lado => LadoModulo.Ambos;

        /// <summary>Cierra turnos de diálogo activos en el anfitrión durante un reinicio técnico off-role (CP-611).</summary>
        public static void CancelarTurnosActivos()
        {
            var instancia = instanciaActiva;
            if (instancia is null)
            {
                return;
            }

            foreach (var turno in instancia.turnosActivos.Values.ToList())
            {
                var npc = Game1.getCharacterFromName(turno.NpcName);
                MarcarDialogoActivo(npc, false);
            }

            instancia.turnosActivos.Clear();
        }

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;
            instanciaActiva = this;
            this.AplicarParches();

            contexto.Helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
            contexto.Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            contexto.Helper.Events.Multiplayer.ModMessageReceived += this.OnModMessageReceived;
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
                this.context.Helper.Events.Multiplayer.ModMessageReceived -= this.OnModMessageReceived;
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

        private void OnModMessageReceived(object? sender, ModMessageReceivedEventArgs e)
        {
            if (this.context is null
                || !Context.IsWorldReady
                || !string.Equals(e.FromModID, this.context.Manifest.UniqueID, StringComparison.Ordinal))
            {
                return;
            }

            if (Context.IsMainPlayer && e.Type == ProtocoloMultijugador.MsgDialogoIniciar)
            {
                var solicitud = e.ReadAs<DialogoSolicitudRemota>();
                if (!ProtocoloMultijugador.EsPeticionDialogoValida(solicitud) || !this.context.MotorActivoEnPartida)
                {
                    return;
                }

                string? personajeId = null;
                foreach (var (id, perfil) in this.context.PersonajesGobernados)
                {
                    if (string.Equals(perfil.Npc, solicitud!.NpcName, StringComparison.Ordinal))
                    {
                        personajeId = id;
                        break;
                    }
                }

                if (personajeId is null || !this.context.MotoresHabitante.TryGetValue(personajeId, out var motor))
                {
                    return;
                }

                var npc = Game1.getCharacterFromName(motor.Perfil.Npc);
                var obs = this.context.ProveedorObservacion?.Invoke(personajeId)
                    ?? motor.UltimaObservacion
                    ?? new Observacion(
                        Math.Max(1, Game1.Date?.TotalDays ?? 1),
                        Game1.timeOfDay,
                        npc?.currentLocation?.NameOrUniqueName ?? string.Empty,
                        npc?.TilePoint.X ?? 0,
                        npc?.TilePoint.Y ?? 0,
                        1.0,
                        "soleado",
                        Array.Empty<EntidadObservada>());

                var ahoraMs = this.context.RelojMs?.Invoke() ?? 0;
                var interlocutorRef = ProtocoloMultijugador.ConstruirReferenciaGranjero(e.FromPlayerID);
                var interlocutorNombre = string.IsNullOrWhiteSpace(solicitud!.FarmerName) ? "granjero" : solicitud.FarmerName;
                this.SuscribirResultadoDialogoSiFalta(personajeId, motor);

                var enEvento = Game1.CurrentEvent != null || Game1.isFestival();
                var reaccion = motor.IniciarTurnoDialogo(
                    obs,
                    interlocutorRef,
                    interlocutorNombre,
                    ahoraMs,
                    reaccionMs: 0,
                    enEvento: enEvento,
                    valleyTalkActivo: this.EstaValleyTalkActivo(personajeId, motor.Perfil.Npc));

                if (!string.IsNullOrWhiteSpace(reaccion.FraseAnticipada))
                {
                    this.context.Helper.Multiplayer.SendMessage(
                        new DialogoResultadoRemoto
                        {
                            NpcName = motor.Perfil.Npc,
                            Tipo = "frase",
                            Frase = reaccion.FraseAnticipada,
                        },
                        ProtocoloMultijugador.MsgDialogoResultado,
                        new[] { this.context.Manifest.UniqueID },
                        new[] { e.FromPlayerID });
                    return;
                }

                this.turnosActivos[personajeId] = new TurnoEnVivo
                {
                    PersonajeId = personajeId,
                    NpcName = motor.Perfil.Npc,
                    InterlocutorRef = interlocutorRef,
                    Ubicacion = npc?.currentLocation?.NameOrUniqueName ?? string.Empty,
                    PulsoId = reaccion.Pulso?.Id,
                    TargetPlayerId = e.FromPlayerID,
                };
                return;
            }

            if (!Context.IsMainPlayer && e.Type == ProtocoloMultijugador.MsgDialogoResultado)
            {
                var resultado = e.ReadAs<DialogoResultadoRemoto>();
                if (resultado is null || string.IsNullOrWhiteSpace(resultado.NpcName))
                {
                    return;
                }

                var npc = Game1.getCharacterFromName(resultado.NpcName);
                if (npc is null)
                {
                    return;
                }

                if (Game1.activeClickableMenu is DialogueBox)
                {
                    Game1.exitActiveMenu();
                }

                npc.CurrentDialogue.Clear();
                if (string.Equals(resultado.Tipo, "frase", StringComparison.Ordinal) && !string.IsNullOrWhiteSpace(resultado.Frase))
                {
                    npc.CurrentDialogue.Push(new Dialogue(npc, null, resultado.Frase));
                }
                else
                {
                    npc.doEmote(GestorDialogo.EmoteCaducado);
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
                }

                Game1.drawDialogue(npc);
            }
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
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer)
            {
                return;
            }

            if (Game1.activeClickableMenu is not DialogueBox)
            {
                this.LimpiarMarcasDialogoActivo();
            }

            if (this.turnosActivos.Count == 0)
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
                var jugadorTurno = turno.TargetPlayerId == Game1.player.UniqueMultiplayerID
                    ? Game1.player
                    : Game1.GetPlayer(turno.TargetPlayerId, onlyOnline: true);
                var presente = EstaInterlocutorPresente(npc, jugadorTurno);

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
                        $"valleyTalk={this.EstaValleyTalkActivo(id, motor.Perfil.Npc)}",
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

            if (sub == "texto_libre")
            {
                var primerPersonaje = this.context.MotoresHabitante.FirstOrDefault();
                if (primerPersonaje.Value is null)
                {
                    this.context.Monitor.Log("[Dialogo] No hay personaje gobernado cargado.", LogLevel.Warn);
                    return;
                }

                var obs = this.context.ProveedorObservacion?.Invoke(primerPersonaje.Key)
                    ?? primerPersonaje.Value.UltimaObservacion;
                if (obs is null)
                {
                    this.context.Monitor.Log("[Dialogo] No hay observación disponible.", LogLevel.Warn);
                    return;
                }

                var interlocutorRef = $"farmer:{Game1.player.UniqueMultiplayerID}";
                this.AbrirMenuTextoLibre(primerPersonaje.Key, primerPersonaje.Value.Perfil.Npc, obs, interlocutorRef);
                this.context.Monitor.Log("[Dialogo] MenuTextoLibre abierto para prueba interactiva.", LogLevel.Info);
                return;
            }

            if (sub == "hablar")
            {
                var objetivo = args.Length >= 2 ? args[1] : "Leah";
                var npc = Game1.getCharacterFromName(objetivo);
                if (npc is null || Game1.currentLocation is null)
                {
                    this.context.Monitor.Log($"[Dialogo] NPC '{objetivo}' no encontrado.", LogLevel.Warn);
                    return;
                }

                npc.checkAction(Game1.player, Game1.currentLocation);
                return;
            }

            this.context.Monitor.Log("[Dialogo] Uso: cp_dialogo estado | cp_dialogo simular_respuesta <texto> | cp_dialogo texto_libre | cp_dialogo hablar [npc]", LogLevel.Warn);
        }

        private bool EstaValleyTalkActivo(string personajeId, string npcName)
        {
            if (this.context is null)
            {
                return false;
            }

            var mods = this.context.Helper.ModRegistry
                .GetAll()
                .Select(m => new ModInstaladoResumen(m.Manifest.UniqueID, m.Manifest.Name, m.Manifest.Author))
                .ToList();

            var ceder = ConvivenciaValleyTalk.DebeCederAVanilla(
                personajeId,
                npcName,
                mods,
                this.context.Config.ValleyTalkDesactivadoPara);

            if (ceder && this.avisosValleyTalkEmitidos.Add(personajeId))
            {
                var vt = ConvivenciaValleyTalk.DetectarValleyTalk(mods);
                this.context.Monitor.Log(
                    ConvivenciaValleyTalk.ConstruirAvisoConsola(personajeId, npcName, vt?.UniqueId),
                    LogLevel.Warn);
            }

            return ceder;
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
                if (primerTurno.TargetPlayerId != Game1.player.UniqueMultiplayerID)
                {
                    this.context.Helper.Multiplayer.SendMessage(
                        new DialogoResultadoRemoto
                        {
                            NpcName = primerTurno.NpcName,
                            Tipo = "frase",
                            Frase = res.Frase,
                        },
                        ProtocoloMultijugador.MsgDialogoResultado,
                        new[] { this.context.Manifest.UniqueID },
                        new[] { primerTurno.TargetPlayerId });
                    this.context.Monitor.Log(
                        $"[Dialogo:{primerTurno.PersonajeId}] Frase pensada enviada a farmhand {primerTurno.TargetPlayerId}: \"{res.Frase}\"",
                        LogLevel.Info);
                }
                else if (npc is not null)
                {
                    if (Game1.activeClickableMenu is DialogueBox)
                    {
                        Game1.exitActiveMenu();
                    }

                    npc.CurrentDialogue.Clear();
                    npc.CurrentDialogue.Push(new Dialogue(npc, null, res.Frase));
                    MarcarDialogoActivo(npc, true);
                    Game1.drawDialogue(npc);
                    this.context.Monitor.Log(
                        $"[Dialogo:{primerTurno.PersonajeId}] Frase pensada mostrada para {npc.Name}: \"{res.Frase}\"",
                        LogLevel.Info);
                }

                this.turnosActivos.Remove(primerTurno.PersonajeId);
            }
        }

        private static void MarcarDialogoActivo(NPC? npc, bool activo)
        {
            if (npc is null)
            {
                return;
            }

            if (activo)
            {
                npc.modData[ClaveDialogoActivoModData] = "1";
            }
            else
            {
                npc.modData.Remove(ClaveDialogoActivoModData);
            }
        }

        private void LimpiarMarcasDialogoActivo()
        {
            if (this.context is null)
            {
                return;
            }

            foreach (var perfil in this.context.PersonajesGobernados.Values)
            {
                var npc = Game1.getCharacterFromName(perfil.Npc);
                if (npc is not null && npc.modData.ContainsKey(ClaveDialogoActivoModData))
                {
                    npc.modData.Remove(ClaveDialogoActivoModData);
                }
            }
        }

        private void MostrarCaidaAVanilla(NPC? npc, TurnoEnVivo turno)
        {
            if (this.context is not null && turno.TargetPlayerId != Game1.player.UniqueMultiplayerID)
            {
                this.context.Helper.Multiplayer.SendMessage(
                    new DialogoResultadoRemoto
                    {
                        NpcName = turno.NpcName,
                        Tipo = "vanilla",
                    },
                    ProtocoloMultijugador.MsgDialogoResultado,
                    new[] { this.context.Manifest.UniqueID },
                    new[] { turno.TargetPlayerId });
                return;
            }

            if (npc is null)
            {
                return;
            }

            MarcarDialogoActivo(npc, false);
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

        /// <summary>Construye las opciones de diálogo del jugador leyendo inventario real y amistad en Stardew (CP-504).</summary>
        public IReadOnlyList<OpcionJugador> ConstruirOpcionesParaJugadorEnVivo(
            MotorHabitante motor,
            Farmer who,
            IReadOnlyList<PropuestaEncargo>? encargosDisponibles = null)
        {
            var corazones = who.getFriendshipHeartLevelForNPC(motor.Perfil.Npc);
            var inventario = new List<ObjetoInventarioJugador>();
            foreach (var item in who.Items)
            {
                if (item is null || item.Stack <= 0 || string.IsNullOrWhiteSpace(item.QualifiedItemId))
                {
                    continue;
                }

                inventario.Add(new ObjetoInventarioJugador(
                    item.QualifiedItemId,
                    item.DisplayName,
                    EsRegalable: item.canBeGivenAsGift(),
                    Cantidad: item.Stack));
            }

            return motor.ConstruirOpcionesJugador(
                corazones,
                inventario,
                encargosDisponibles ?? Array.Empty<PropuestaEncargo>());
        }

        /// <summary>Abre el menú nativo de texto libre para «Decir otra cosa…» y envía la frase tal cual al motor (CP-504).</summary>
        public void AbrirMenuTextoLibre(
            string personajeId,
            string npcName,
            Observacion observacion,
            string interlocutorRef)
        {
            if (this.context is null || !this.context.MotoresHabitante.TryGetValue(personajeId, out var motor))
            {
                return;
            }

            Game1.activeClickableMenu = new MenuTextoLibre(
                $"¿Qué quieres decirle a {npcName}?",
                texto =>
                {
                    var ahoraMs = this.context.RelojMs?.Invoke() ?? 0;
                    motor.EnviarTextoLibreJugador(observacion, interlocutorRef, texto, ahoraMs);
                });
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

            if (!instancia.context.EstaActivo)
            {
                return true;
            }

            if (Context.IsMainPlayer && !instancia.context.MotorActivoEnPartida)
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

            if (personajeId is null)
            {
                return true;
            }

            var enEvento = Game1.CurrentEvent != null || Game1.isFestival();
            MotorHabitante? motor = null;
            if (Context.IsMainPlayer)
            {
                if (!instancia.context.MotoresHabitante.TryGetValue(personajeId, out motor))
                {
                    return true;
                }

                var decision = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
                    motor.MenteHabilitada && instancia.context.Config.MenteHabilitada,
                    motor.EstaMenteCaida,
                    enEvento,
                    instancia.EstaValleyTalkActivo(personajeId, __instance.Name)));

                if (!decision.Interceptar)
                {
                    return true;
                }
            }
            else
            {
                var decisionCliente = GestorDialogo.EvaluarIntercepcion(new ContextoIntercepcionDialogo(
                    instancia.context.Config.MenteHabilitada,
                    MenteCaida: false,
                    enEvento,
                    instancia.EstaValleyTalkActivo(personajeId, __instance.Name)));

                if (!decisionCliente.Interceptar)
                {
                    return true;
                }
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

                if (!Context.IsMainPlayer)
                {
                    __instance.doEmote(GestorDialogo.EmotePensando);
                    __instance.CurrentDialogue.Clear();
                    __instance.CurrentDialogue.Push(new Dialogue(__instance, null, GestorDialogo.ReflejoPorDefecto));
                    Game1.drawDialogue(__instance);

                    instancia.context.Helper.Multiplayer.SendMessage(
                        new DialogoSolicitudRemota
                        {
                            NpcName = __instance.Name,
                            FarmerName = who.Name,
                        },
                        ProtocoloMultijugador.MsgDialogoIniciar,
                        new[] { instancia.context.Manifest.UniqueID },
                        new[] { Game1.MasterPlayer.UniqueMultiplayerID });

                    __result = true;
                    return false;
                }

                var obs = instancia.context.ProveedorObservacion?.Invoke(personajeId)
                    ?? motor!.UltimaObservacion
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
                var interlocutorRef = ProtocoloMultijugador.ConstruirReferenciaGranjero(who.UniqueMultiplayerID);
                instancia.SuscribirResultadoDialogoSiFalta(personajeId, motor!);

                var reaccion = motor!.IniciarTurnoDialogo(
                    obs,
                    interlocutorRef,
                    who.Name,
                    ahoraMs,
                    reaccionMs: crono.Elapsed.TotalMilliseconds,
                    enEvento: enEvento,
                    valleyTalkActivo: instancia.EstaValleyTalkActivo(personajeId, __instance.Name));

                if (!string.IsNullOrWhiteSpace(reaccion.FraseAnticipada))
                {
                    __instance.CurrentDialogue.Clear();
                    __instance.CurrentDialogue.Push(new Dialogue(__instance, null, reaccion.FraseAnticipada));
                    MarcarDialogoActivo(__instance, true);
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
                MarcarDialogoActivo(__instance, true);
                Game1.drawDialogue(__instance);
                crono.Stop();

                instancia.turnosActivos[personajeId] = new TurnoEnVivo
                {
                    PersonajeId = personajeId,
                    NpcName = __instance.Name,
                    InterlocutorRef = interlocutorRef,
                    Ubicacion = __instance.currentLocation?.NameOrUniqueName ?? string.Empty,
                    PulsoId = reaccion.Pulso?.Id,
                    TargetPlayerId = who.UniqueMultiplayerID,
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
