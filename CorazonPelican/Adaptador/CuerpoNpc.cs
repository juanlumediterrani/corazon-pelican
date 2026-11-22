using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Pathfinding;

namespace CorazonPelican.Adaptador
{
    /// <summary>
    /// Adaptador en hilo principal que ejecuta las acciones del vocabulario v1 sobre un <see cref="NPC"/>
    /// de Stardew Valley usando medios nativos (<c>PathFindController</c>, <c>StartActivityRouteEndBehavior</c>,
    /// <c>EndActivityRouteEndBehavior</c>, <c>doEmote</c>, <c>showTextAboveHead</c>, <c>faceGeneralDirection</c>)
    /// según lo validado en CP-011 y CP-012 (CP-302).
    /// </summary>
    public sealed class CuerpoNpc : IMediosCuerpo
    {
        public const string ClaveControlModData = "CorazonPelican.Control";

        private static readonly MethodInfo? MetodoGetRouteEndBehavior =
            AccessTools.Method(typeof(NPC), "getRouteEndBehaviorFunction", new[] { typeof(string), typeof(string) });

        private readonly PerfilPersonaje perfil;
        private readonly IMonitor monitor;
        private readonly Func<Observacion?> proveedorObservacion;

        private bool rutaActiva;
        private bool rutaCompletada;
        private string? ubicacionDestinoRuta;
        private Point casillaDestinoRuta;

        public CuerpoNpc(
            PerfilPersonaje perfil,
            LugaresOfrecidos lugares,
            ModoReloj modoReloj,
            IMonitor monitor,
            Func<Observacion?> proveedorObservacion)
        {
            this.perfil = perfil ?? throw new ArgumentNullException(nameof(perfil));
            this.monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            this.proveedorObservacion = proveedorObservacion ?? throw new ArgumentNullException(nameof(proveedorObservacion));

            var contexto = new ContextoEjecucionAccion(this, lugares, modoReloj);
            this.Ejecutor = new EjecutorCuerpo(contexto);
        }

        public EjecutorCuerpo Ejecutor { get; }

        public string UbicacionActual => this.ObtenerNpc()?.currentLocation?.NameOrUniqueName ?? string.Empty;

        public int CasillaX => this.ObtenerNpc()?.TilePoint.X ?? 0;

        public int CasillaY => this.ObtenerNpc()?.TilePoint.Y ?? 0;

        public bool EstaEnAnimacionActividad
        {
            get
            {
                var npc = this.ObtenerNpc();
                return npc is not null && (npc.doingEndOfRouteAnimation.Value || npc.goingToDoEndOfRouteAnimation.Value);
            }
        }

        public EstadoRutaCuerpo EstadoRutaActual
        {
            get
            {
                if (this.rutaCompletada)
                {
                    return EstadoRutaCuerpo.Completada;
                }

                if (!string.IsNullOrWhiteSpace(this.MotivoBloqueoRuta))
                {
                    return EstadoRutaCuerpo.Bloqueada;
                }

                if (!this.rutaActiva)
                {
                    return EstadoRutaCuerpo.Inactiva;
                }

                var npc = this.ObtenerNpc();
                if (npc is null || npc.currentLocation is null)
                {
                    this.MotivoBloqueoRuta = "NPC no disponible en el mundo";
                    this.rutaActiva = false;
                    return EstadoRutaCuerpo.Bloqueada;
                }

                if (npc.controller?.pathToEndPoint is { Count: > 0 })
                {
                    return EstadoRutaCuerpo.EnCurso;
                }

                var mismaUbicacion = string.Equals(npc.currentLocation.NameOrUniqueName, this.ubicacionDestinoRuta, StringComparison.Ordinal);
                var dist = Math.Abs(npc.TilePoint.X - this.casillaDestinoRuta.X) + Math.Abs(npc.TilePoint.Y - this.casillaDestinoRuta.Y);
                if (mismaUbicacion && dist <= 1)
                {
                    this.rutaActiva = false;
                    this.rutaCompletada = true;
                    return EstadoRutaCuerpo.Completada;
                }

                this.MotivoBloqueoRuta = $"controlador detenido en {npc.currentLocation.NameOrUniqueName}@{npc.TilePoint} antes de alcanzar {this.ubicacionDestinoRuta}@{this.casillaDestinoRuta}";
                this.rutaActiva = false;
                return EstadoRutaCuerpo.Bloqueada;
            }
        }

        public string? MotivoBloqueoRuta { get; private set; }

        public long DuracionAsentamientoSentarseMs => 0;

        public static int ResolverCodigoEmote(string tipo) => tipo switch
        {
            "duda" => 8,
            "enfado" => 12,
            "sorpresa" => 16,
            "corazon" => 20,
            "tristeza" => 28,
            "alegria" => 32,
            "saludo" => 32,
            "pensativa" => 40,
            _ => -1,
        };

        public void AvanzarTick(long ahoraMs)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            this.Ejecutor.AvanzarTick(ahoraMs);
        }

        public void EjecutarPlan(IEnumerable<PasoPlan> pasos, long ahoraMs, string? idPlan = null)
        {
            this.Ejecutor.EjecutarPlan(pasos, ahoraMs, idPlan);
        }

        public void ReclamarControl(string motivo)
        {
            var npc = this.ObtenerNpc();
            if (npc is null)
            {
                return;
            }

            npc.modData[ClaveControlModData] = $"{motivo}@{Game1.timeOfDay:D4}";
            npc.ignoreScheduleToday = true;
            npc.temporaryController = null;
            npc.queuedSchedulePaths.Clear();
        }

        public bool IniciarRutaHacia(Lugar destino, int orientacionFinal)
        {
            this.MotivoBloqueoRuta = null;
            this.rutaCompletada = false;
            this.rutaActiva = false;

            var npc = this.ObtenerNpc();
            if (npc is null || npc.currentLocation is null)
            {
                this.MotivoBloqueoRuta = "NPC sin ubicación actual";
                return false;
            }

            var casillaDestino = ElegirCasillaDestino(destino);
            var origenUbicacion = npc.currentLocation.NameOrUniqueName;
            var origenCasilla = npc.TilePoint;

            this.ubicacionDestinoRuta = destino.Ubicacion;
            this.casillaDestinoRuta = casillaDestino;

            if (string.Equals(origenUbicacion, destino.Ubicacion, StringComparison.Ordinal) && origenCasilla == casillaDestino)
            {
                npc.controller = null;
                npc.Halt();
                npc.faceDirection(orientacionFinal);
                this.rutaCompletada = true;
                return true;
            }

            SchedulePathDescription ruta;
            try
            {
                ruta = npc.pathfindToNextScheduleLocation(
                    npc.ScheduleKey ?? "corazon",
                    origenUbicacion,
                    origenCasilla.X,
                    origenCasilla.Y,
                    destino.Ubicacion,
                    casillaDestino.X,
                    casillaDestino.Y,
                    orientacionFinal,
                    null,
                    null);
            }
            catch (Exception ex)
            {
                this.MotivoBloqueoRuta = $"error calculando ruta hacia {destino.Id}: {ex.Message}";
                return false;
            }

            if (ruta?.route is null || ruta.route.Count == 0)
            {
                this.MotivoBloqueoRuta = $"no existe camino transitable hacia {destino.Id} ({destino.Ubicacion}@{casillaDestino})";
                return false;
            }

            var totalPasos = ruta.route.Count;
            npc.DirectionsToNewLocation = ruta;
            npc.controller = new PathFindController(ruta.route, npc, npc.currentLocation)
            {
                finalFacingDirection = orientacionFinal,
                endBehaviorFunction = (c, loc) =>
                {
                    this.rutaActiva = false;
                    this.rutaCompletada = true;
                    this.monitor.Log(
                        $"[CuerpoNpc] {c.Name} completó ruta hacia {destino.Id} en {loc?.NameOrUniqueName}@{c.TilePoint} (dir={c.FacingDirection}).",
                        LogLevel.Trace);
                },
            };

            this.rutaActiva = true;
            this.monitor.Log(
                $"[CuerpoNpc] {npc.Name} iniciando ruta nativa {origenUbicacion}@{origenCasilla} -> {destino.Id} ({destino.Ubicacion}@{casillaDestino}, pasos={totalPasos}).",
                LogLevel.Trace);
            return true;
        }

        public void DetenerMovimiento()
        {
            this.rutaActiva = false;
            var npc = this.ObtenerNpc();
            if (npc is null)
            {
                return;
            }

            npc.controller = null;
            npc.temporaryController = null;
            if (!npc.doingEndOfRouteAnimation.Value)
            {
                npc.Halt();
            }
        }

        public bool AdoptarPosturaSentada(Lugar destino, int orientacion)
        {
            var npc = this.ObtenerNpc();
            if (npc is null)
            {
                return false;
            }

            npc.controller = null;
            npc.Halt();
            npc.faceDirection(orientacion);
            this.monitor.Log(
                $"[CuerpoNpc] {npc.Name} adoptó postura en punto sentable '{destino.Id}' ({npc.currentLocation?.NameOrUniqueName}@{npc.TilePoint}, dir={orientacion}).",
                LogLevel.Trace);
            return true;
        }

        public bool IntentarMirarObjetivo(string objetivoRef)
        {
            var npc = this.ObtenerNpc();
            if (npc is null || npc.currentLocation is null)
            {
                return false;
            }

            Vector2? posicionObjetivo = null;

            if (objetivoRef.StartsWith("farmer:", StringComparison.Ordinal))
            {
                var idCola = objetivoRef.Substring("farmer:".Length);
                var granjero = npc.currentLocation.farmers.FirstOrDefault(f =>
                    string.Equals(f.UniqueMultiplayerID.ToString(), idCola, StringComparison.Ordinal)
                    || string.Equals(f.Name, idCola, StringComparison.OrdinalIgnoreCase))
                    ?? (ReferenceEquals(Game1.player?.currentLocation, npc.currentLocation) ? Game1.player : null);

                if (granjero is not null)
                {
                    posicionObjetivo = granjero.getStandingPosition();
                }
            }
            else if (objetivoRef.StartsWith("npc:", StringComparison.Ordinal))
            {
                var nombreNpc = objetivoRef.Substring("npc:".Length);
                var otro = npc.currentLocation.characters.FirstOrDefault(c =>
                    string.Equals(c.Name, nombreNpc, StringComparison.OrdinalIgnoreCase));
                if (otro is not null)
                {
                    posicionObjetivo = otro.getStandingPosition();
                }
            }

            if (posicionObjetivo is null)
            {
                var obs = this.proveedorObservacion();
                var entidad = obs?.Entidades.FirstOrDefault(e => string.Equals(e.Ref, objetivoRef, StringComparison.Ordinal));
                if (entidad is not null)
                {
                    posicionObjetivo = new Vector2((entidad.X * 64f) + 32f, (entidad.Y * 64f) + 32f);
                }
            }

            if (posicionObjetivo is null)
            {
                return false;
            }

            if (!npc.doingEndOfRouteAnimation.Value)
            {
                npc.faceGeneralDirection(posicionObjetivo.Value, 0, opposite: false, useTileCalculations: false);
            }

            return true;
        }

        public bool PuedeDibujar(out string? motivoBloqueo)
        {
            var npc = this.ObtenerNpc();
            if (npc is null)
            {
                motivoBloqueo = "NPC no encontrado";
                return false;
            }

            if (npc.shouldWearIslandAttire.Value)
            {
                motivoBloqueo = "atuendo de isla incompatible con los frames de dibujo (CP-012)";
                return false;
            }

            var claveAnim = ObtenerClaveAnimacionDibujo(npc);
            var descripciones = DataLoader.AnimationDescriptions(Game1.content);
            if (!descripciones.ContainsKey(claveAnim))
            {
                motivoBloqueo = $"no existe '{claveAnim}' en Data/animationDescriptions";
                return false;
            }

            motivoBloqueo = null;
            return true;
        }

        public bool IniciarDibujo(string? obraId)
        {
            var npc = this.ObtenerNpc();
            if (npc is null)
            {
                return false;
            }

            var claveAnim = ObtenerClaveAnimacionDibujo(npc);
            npc.controller = null;
            npc.Halt();
            npc.faceDirection(2);
            npc.StartActivityRouteEndBehavior(claveAnim, null);
            return npc.doingEndOfRouteAnimation.Value || npc.goingToDoEndOfRouteAnimation.Value;
        }

        public void DetenerAnimacionActividad()
        {
            var npc = this.ObtenerNpc();
            if (npc is null)
            {
                return;
            }

            npc.EndActivityRouteEndBehavior();
            npc.endOfRouteMessage.Value = null;
        }

        public bool HacerGesto(string tipo)
        {
            var npc = this.ObtenerNpc();
            var codigo = ResolverCodigoEmote(tipo);
            if (npc is null || codigo < 0)
            {
                return false;
            }

            npc.doEmote(codigo);
            return true;
        }

        public bool Murmurar(string texto)
        {
            var npc = this.ObtenerNpc();
            if (npc is null || string.IsNullOrWhiteSpace(texto))
            {
                return false;
            }

            npc.showTextAboveHead(texto);
            return true;
        }

        public bool VolverARutina()
        {
            var npc = this.ObtenerNpc();
            if (npc is null || npc.currentLocation is null)
            {
                return false;
            }

            this.rutaActiva = false;
            this.rutaCompletada = false;
            this.MotivoBloqueoRuta = null;

            npc.modData.Remove(ClaveControlModData);
            npc.ignoreScheduleToday = false;
            npc.followSchedule = true;
            npc.lastAttemptedSchedule = Game1.timeOfDay;
            npc.queuedSchedulePaths.Clear();

            if (npc.Schedule is null || npc.Schedule.Count == 0)
            {
                return true;
            }

            var vigentes = npc.Schedule.Where(kv => kv.Key <= Game1.timeOfDay).OrderBy(kv => kv.Key).ToList();
            var tramo = vigentes.Count > 0 ? vigentes[^1].Value : npc.Schedule.OrderBy(kv => kv.Key).First().Value;

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

                return true;
            }

            var ruta = npc.pathfindToNextScheduleLocation(
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

            var endBehavior = MetodoGetRouteEndBehavior?.Invoke(npc, new object?[] { tramo.endOfRouteBehavior, tramo.endOfRouteMessage }) as PathFindController.endBehavior;
            npc.DirectionsToNewLocation = ruta;
            npc.controller = new PathFindController(ruta.route, npc, npc.currentLocation)
            {
                finalFacingDirection = tramo.facingDirection,
                endBehaviorFunction = endBehavior,
            };
            npc.previousEndPoint = tramo.targetTile;
            return true;
        }

        public NPC? ObtenerNpc() => Game1.getCharacterFromName(this.perfil.Npc);

        private static string ObtenerClaveAnimacionDibujo(NPC npc)
            => $"{npc.Name.ToLowerInvariant()}_draw";

        private static Point ElegirCasillaDestino(Lugar destino)
        {
            if (destino.Area.Count < 4)
            {
                return Point.Zero;
            }

            var x0 = destino.Area[0];
            var y0 = destino.Area[1];
            var ancho = Math.Max(1, destino.Area[2]);
            var alto = Math.Max(1, destino.Area[3]);
            var centro = new Point(x0 + (ancho / 2), y0 + (alto / 2));

            var loc = Game1.getLocationFromName(destino.Ubicacion);
            if (loc is null)
            {
                return centro;
            }

            if (EsCasillaTransitable(loc, centro.X, centro.Y))
            {
                return centro;
            }

            for (var dy = 0; dy < alto; dy++)
            {
                for (var dx = 0; dx < ancho; dx++)
                {
                    var cx = x0 + dx;
                    var cy = y0 + dy;
                    if (EsCasillaTransitable(loc, cx, cy))
                    {
                        return new Point(cx, cy);
                    }
                }
            }

            return new Point(x0, y0);
        }

        private static bool EsCasillaTransitable(GameLocation loc, int x, int y)
        {
            var rect = new Rectangle((x * 64) + 1, (y * 64) + 1, 62, 62);
            return !loc.isCollidingPosition(rect, Game1.viewport, isFarmer: false, damagesFarmer: 0, glider: false, character: null, pathfinding: true);
        }
    }
}
