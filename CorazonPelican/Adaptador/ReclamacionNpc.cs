using System;
using System.Linq;
using System.Reflection;
using CorazonPelican.Core.Intencion;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

namespace CorazonPelican.Adaptador
{
    /// <summary>
    /// Toma y devolución del control de un <see cref="NPC"/> respecto a su horario nativo
    /// usando <c>modData["CorazonPelican.Control"]</c> y rutas con <see cref="PathFindController"/>
    /// sin teletransporte visible (GSS §5.3, §11; ADS §2; CP-011, CP-303).
    /// </summary>
    public static class ReclamacionNpc
    {
        public const string ClaveControlModData = "CorazonPelican.Control";

        private static readonly MethodInfo? MetodoGetRouteEndBehavior =
            AccessTools.Method(typeof(NPC), "getRouteEndBehaviorFunction", new[] { typeof(string), typeof(string) });

        public static bool EstaReclamado(NPC? npc)
            => npc is not null && npc.modData.ContainsKey(ClaveControlModData);

        public static string? ObtenerEstadoControl(NPC? npc)
            => npc is not null && npc.modData.TryGetValue(ClaveControlModData, out var valor) ? valor : null;

        /// <summary>
        /// Reclama el control del NPC deteniendo su ruta de horario actual sin perder <see cref="NPC.Schedule"/>
        /// y marcando <c>modData["CorazonPelican.Control"]</c> e <c>ignoreScheduleToday = true</c>.
        /// Nunca teletransporta al NPC.
        /// </summary>
        public static bool Reclamar(NPC? npc, string motivo, IMonitor? monitor = null)
        {
            if (npc is null)
            {
                return false;
            }

            var etiqueta = string.IsNullOrWhiteSpace(motivo) ? "reclamado" : motivo.Trim();
            var valor = $"{etiqueta}@{Game1.timeOfDay:D4}";
            var yaReclamado = npc.modData.ContainsKey(ClaveControlModData);

            npc.modData[ClaveControlModData] = valor;
            npc.ignoreScheduleToday = true;
            npc.isSleeping.Value = false;
            npc.temporaryController = null;
            npc.queuedSchedulePaths.Clear();

            if (!yaReclamado)
            {
                monitor?.Log(
                    $"[ReclamacionNpc] {npc.Name} reclamada ({ClaveControlModData}={valor}) en {npc.currentLocation?.NameOrUniqueName}@{npc.TilePoint}.",
                    LogLevel.Trace);
            }

            return true;
        }

        /// <summary>
        /// Libera la reclamación del NPC, cierra cualquier animación de actividad en curso y calcula
        /// una ruta nativa con <see cref="PathFindController"/> desde su posición actual hasta el destino
        /// del tramo vigente de su horario (<c>Key &lt;= Game1.timeOfDay</c>).
        /// </summary>
        public static bool VolverARutina(NPC? npc, IMonitor? monitor = null, string motivo = "volver_a_rutina")
        {
            if (npc is null || npc.currentLocation is null)
            {
                return false;
            }

            var teniaReclamacion = npc.modData.TryGetValue(ClaveControlModData, out var controlPrevio);
            npc.modData.Remove(ClaveControlModData);
            npc.ignoreScheduleToday = false;
            npc.followSchedule = true;
            npc.lastAttemptedSchedule = Game1.timeOfDay;
            npc.temporaryController = null;
            npc.queuedSchedulePaths.Clear();

            if (npc.doingEndOfRouteAnimation.Value || npc.goingToDoEndOfRouteAnimation.Value)
            {
                npc.EndActivityRouteEndBehavior();
                npc.endOfRouteMessage.Value = null;
            }

            npc.controller = null;
            npc.Halt();

            if (npc.Schedule is null && !npc.isMarried())
            {
                npc.TryLoadSchedule();
            }

            if (npc.Schedule is null || npc.Schedule.Count == 0)
            {
                monitor?.Log(
                    $"[ReclamacionNpc] {npc.Name} devuelta a rutina ({motivo}, previo={controlPrevio ?? "libre"}) sin entradas en Schedule.",
                    LogLevel.Info);
                return true;
            }

            var vigentes = npc.Schedule.Where(kv => kv.Key <= Game1.timeOfDay).OrderBy(kv => kv.Key).ToList();
            var parTramo = vigentes.Count > 0
                ? vigentes[^1]
                : npc.Schedule.OrderBy(kv => kv.Key).First();
            var horaTramo = parTramo.Key;
            var tramo = parTramo.Value;

            var origenLoc = npc.currentLocation.NameOrUniqueName;
            var origenTile = npc.TilePoint;

            if (string.Equals(origenLoc, tramo.targetLocationName, StringComparison.Ordinal) && origenTile == tramo.targetTile)
            {
                npc.faceDirection(tramo.facingDirection);
                npc.previousEndPoint = tramo.targetTile;
                if (!string.IsNullOrEmpty(tramo.endOfRouteBehavior) || !string.IsNullOrEmpty(tramo.endOfRouteMessage))
                {
                    npc.StartActivityRouteEndBehavior(tramo.endOfRouteBehavior, tramo.endOfRouteMessage);
                }

                monitor?.Log(
                    $"[ReclamacionNpc] {npc.Name} ya estaba en el destino del tramo {horaTramo:D4} ({tramo.targetLocationName}@{tramo.targetTile}); reincorporada ({motivo}).",
                    LogLevel.Info);
                return true;
            }

            SchedulePathDescription ruta;
            try
            {
                ruta = npc.pathfindToNextScheduleLocation(
                    npc.ScheduleKey ?? "corazon",
                    origenLoc,
                    origenTile.X,
                    origenTile.Y,
                    tramo.targetLocationName,
                    tramo.targetTile.X,
                    tramo.targetTile.Y,
                    tramo.facingDirection,
                    tramo.endOfRouteBehavior,
                    tramo.endOfRouteMessage);
            }
            catch (Exception ex)
            {
                monitor?.Log(
                    $"[ReclamacionNpc] No se pudo calcular ruta de retorno para {npc.Name} hacia {tramo.targetLocationName}@{tramo.targetTile}: {ex.Message}",
                    LogLevel.Warn);
                return false;
            }

            var endBehavior = MetodoGetRouteEndBehavior?.Invoke(npc, new object?[] { tramo.endOfRouteBehavior, tramo.endOfRouteMessage }) as PathFindController.endBehavior;
            var pasos = ruta.route?.Count ?? 0;
            npc.DirectionsToNewLocation = ruta;
            npc.controller = new PathFindController(ruta.route, npc, npc.currentLocation)
            {
                finalFacingDirection = tramo.facingDirection,
                endBehaviorFunction = endBehavior,
            };
            npc.previousEndPoint = tramo.targetTile;

            if (teniaReclamacion || pasos > 0)
            {
                monitor?.Log(
                    $"[ReclamacionNpc] {npc.Name} devuelta a rutina ({motivo}, previo={controlPrevio ?? "libre"}): " +
                    $"{origenLoc}@{origenTile} -> tramo {horaTramo:D4} {tramo.targetLocationName}@{tramo.targetTile} (pasos={pasos}, anim={tramo.endOfRouteBehavior ?? "-"}).",
                    LogLevel.Info);
            }

            return true;
        }

        /// <summary>
        /// Limpia cualquier marca de reclamación persistida del día o sesión anterior al cargar partida
        /// o iniciar un nuevo día, garantizando que el NPC nunca amanezca congelado (CP-011, CP-303).
        /// </summary>
        public static void LiberarAlIniciarJornada(NPC? npc, string evento, IMonitor? monitor = null)
        {
            if (npc is null)
            {
                return;
            }

            var teniaMarca = npc.modData.TryGetValue(ClaveControlModData, out var valorAnterior);
            if (!teniaMarca && !npc.ignoreScheduleToday)
            {
                return;
            }

            npc.modData.Remove(ClaveControlModData);
            npc.ignoreScheduleToday = false;
            npc.followSchedule = true;
            if (npc.Schedule is null && !npc.isMarried())
            {
                npc.TryLoadSchedule();
            }

            monitor?.Log(
                $"[ReclamacionNpc] ({evento}) Reclamación previa de {npc.Name} liberada (marca={valorAnterior ?? "ninguna"}) -> ignoreScheduleToday=False, ScheduleKey={npc.ScheduleKey ?? "(null)"}.",
                LogLevel.Info);
        }

        /// <summary>
        /// Aplica un cambio validado (saltar, mover o sustituir) sobre <c>npc.Schedule</c> recalculando
        /// el tramo siguiente con <c>pathfindToNextScheduleLocation</c> sin alterar días posteriores (CP-011, CP-306).
        /// </summary>
        public static bool AplicarCambioHorario(NPC? npc, EntradaAgendaDia entrada, IMonitor? monitor = null)
        {
            if (npc is null || npc.Schedule is null || entrada is null)
            {
                return false;
            }

            if (entrada.Estado == EstadoHito.Saltado || entrada.Estado == EstadoHito.Sustituido)
            {
                if (npc.Schedule.Remove(entrada.HoraOriginal))
                {
                    RecalcularTramoPosterior(npc, entrada.HoraOriginal);
                    monitor?.Log(
                        $"[ReclamacionNpc] Cambio de rutina ({entrada.Estado}) aplicado a {npc.Name} en {entrada.Hito} (hora {entrada.HoraOriginal:D4}).",
                        LogLevel.Info);
                }

                return true;
            }

            if (entrada.Estado == EstadoHito.Movido && entrada.Hora != entrada.HoraOriginal)
            {
                if (npc.Schedule.TryGetValue(entrada.HoraOriginal, out var desc))
                {
                    npc.Schedule.Remove(entrada.HoraOriginal);
                    desc.time = entrada.Hora;
                    npc.Schedule[entrada.Hora] = desc;
                    monitor?.Log(
                        $"[ReclamacionNpc] Hito {entrada.Hito} movido para {npc.Name}: {entrada.HoraOriginal:D4} -> {entrada.Hora:D4}.",
                        LogLevel.Info);
                }

                return true;
            }

            return false;
        }

        /// <summary>Recarga el horario vanilla del día cuando se degrada por caída de la mente tras cambios previos (CP-303, CP-306).</summary>
        public static void RestaurarHorarioCanonico(NPC? npc, IMonitor? monitor = null)
        {
            if (npc is null || npc.isMarried())
            {
                return;
            }

            npc.TryLoadSchedule();
            monitor?.Log(
                $"[ReclamacionNpc] Horario canónico recargado para {npc.Name} (ScheduleKey={npc.ScheduleKey ?? "(null)"}).",
                LogLevel.Info);
        }

        private static void RecalcularTramoPosterior(NPC npc, int horaModificada)
        {
            if (npc.Schedule is null || npc.currentLocation is null)
            {
                return;
            }

            var siguientes = npc.Schedule.Where(kv => kv.Key > horaModificada).OrderBy(kv => kv.Key).ToList();
            if (siguientes.Count == 0)
            {
                return;
            }

            var previas = npc.Schedule.Where(kv => kv.Key < horaModificada).OrderBy(kv => kv.Key).ToList();
            var origenLoc = previas.Count > 0 ? previas[^1].Value.targetLocationName : npc.currentLocation.NameOrUniqueName;
            var origenTile = previas.Count > 0 ? previas[^1].Value.targetTile : npc.TilePoint;

            var sigKey = siguientes[0].Key;
            var sigVal = siguientes[0].Value;
            try
            {
                var recalculada = npc.pathfindToNextScheduleLocation(
                    npc.ScheduleKey ?? "corazon",
                    origenLoc,
                    origenTile.X,
                    origenTile.Y,
                    sigVal.targetLocationName,
                    sigVal.targetTile.X,
                    sigVal.targetTile.Y,
                    sigVal.facingDirection,
                    sigVal.endOfRouteBehavior,
                    sigVal.endOfRouteMessage);
                recalculada.time = sigKey;
                npc.Schedule[sigKey] = recalculada;
            }
            catch
            {
                // Si el cálculo de tramo posterior falla, se deja intacta la entrada original para fallback vanilla.
            }
        }
    }
}
