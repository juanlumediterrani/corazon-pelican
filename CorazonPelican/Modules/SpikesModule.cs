using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Framework;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Quests;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Mecanismos y parches de verificación controlada para las spikes en vivo (V-01, V-02) bajo
    /// <c>ModoDesarrollo = true</c>. Permanecen inertes mientras no se activen por consola.
    /// </summary>
    internal sealed class SpikesModule : IModule, IDisposable
    {
        private const string TextoV01PorDefecto = "Hola desde Corazón Pelican: hoy hace buena luz junto al río.";

        private static SpikesModule? instanciaActiva;

        [ThreadStatic]
        private static bool enPrefixCheckAction;

        private ModContext? context;
        private Harmony? harmony;
        private bool parchesAplicados;
        private bool v01Activo;
        private string v01Texto = TextoV01PorDefecto;

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

            contexto.Helper.ConsoleCommands.Add(
                "cp_v01",
                "Controla el prototipo de Spike V-01 (checkAction): cp_v01 <on|off|estado|texto <frase>>.",
                this.OnComandoV01);
            contexto.Helper.ConsoleCommands.Add(
                "cp_v01_diag",
                "Muestra amistad, estado de conversación, regalos y misiones para un NPC (por defecto el primer gobernado): cp_v01_diag [npc].",
                this.OnComandoV01Diag);
        }

        public void Dispose()
        {
            if (ReferenceEquals(instanciaActiva, this))
            {
                instanciaActiva = null;
            }

            if (this.parchesAplicados && this.harmony is not null)
            {
                this.harmony.UnpatchAll(this.harmony.Id);
                this.parchesAplicados = false;
            }

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

            if (metodoCheckAction is null)
            {
                this.context.Monitor.Log("[V01] No se encontró NPC.checkAction(Farmer, GameLocation).", LogLevel.Error);
                return;
            }

            this.harmony = new Harmony($"{this.context.Manifest.UniqueID}.Spikes");
            this.harmony.Patch(
                original: metodoCheckAction,
                prefix: new HarmonyMethod(typeof(SpikesModule), nameof(PrefixCheckAction)));
            this.parchesAplicados = true;
            this.context.Monitor.Log("[Spikes] Parche V-01 registrado en modo desarrollo (inactivo hasta 'cp_v01 on').", LogLevel.Trace);
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
