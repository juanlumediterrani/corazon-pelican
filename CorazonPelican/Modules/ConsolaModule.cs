using System;
using System.Globalization;
using System.Linq;
using CorazonPelican.Core;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Framework;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace CorazonPelican.Modules
{
    /// <summary>Comandos de consola <c>cp_*</c> para diagnóstico y control en caliente (ADS §12.3, CP-005, CP-109).</summary>
    internal sealed class ConsolaModule : IModule
    {
        private ModContext context = null!;

        public string Nombre => "Consola";

        public LadoModulo Lado => LadoModulo.Ambos;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;
            contexto.Helper.ConsoleCommands.Add(
                "cp_estado",
                "Muestra el estado de Corazón Pelican: personajes, MenteHabilitada, ModoDesarrollo, presupuesto y API.",
                this.OnEstado);
            contexto.Helper.ConsoleCommands.Add(
                "cp_escena",
                "Genera e imprime la escena actual de un personaje sin emitir pulso ni gastar presupuesto: cp_escena <personaje>.",
                this.OnEscena);
            contexto.Helper.ConsoleCommands.Add(
                "cp_pulso",
                "Fuerza un pulso para un personaje respetando el presupuesto diario: cp_pulso <personaje>.",
                this.OnPulso);
            contexto.Helper.ConsoleCommands.Add(
                "cp_mente",
                "Activa o desactiva MenteHabilitada en caliente: cp_mente <on|off>.",
                this.OnMente);
            contexto.Helper.ConsoleCommands.Add(
                "cp_posicion",
                "Imprime ubicación, casilla, orientación y transitabilidad del jugador (y de un NPC opcional): cp_posicion [npc].",
                this.OnPosicion);
            contexto.Helper.ConsoleCommands.Add(
                "cp_lugares",
                "Comprueba en vivo que todas las áreas del perfil de un personaje están medidas y son transitables: cp_lugares [personaje].",
                this.OnLugares);
        }

        private void OnEstado(string comando, string[] args)
        {
            var log = this.context.Monitor;
            var cfg = this.context.Config;
            var modoReloj = this.context.ModoRelojEfectivo;

            var sesion = !Context.IsWorldReady
                ? "sin partida cargada"
                : Context.IsMainPlayer
                    ? (this.context.MotorActivoEnPartida ? "anfitrión (motor activo)" : "anfitrión (motor inactivo)")
                    : "cliente (motor inactivo)";

            log.Log(
                $"=== Corazón Pelican (contrato {CorazonCore.VersionContrato}, activo={this.context.EstaActivo}, sesión={sesion}) ===",
                LogLevel.Info);
            log.Log(
                $"  Config: MenteHabilitada={cfg.MenteHabilitada}, ModoDesarrollo={cfg.ModoDesarrollo}, " +
                $"ModoReloj={cfg.ModoReloj}->{modoReloj}, Cadencia={cfg.SensorioCadenciaTicks} ticks, " +
                $"TiempoMaxRespuesta={cfg.TiempoMaxRespuestaMs} ms, Telemetria={cfg.Telemetria}",
                LogLevel.Info);

            var validosAsset = this.context.PerfilesValidos.Count == 0
                ? "ninguno"
                : string.Join(", ", this.context.PerfilesValidos.Keys.OrderBy(k => k, StringComparer.Ordinal));
            var gobernados = this.context.PersonajesGobernados.Count == 0
                ? "ninguno"
                : string.Join(", ", this.context.PersonajesGobernados.Keys.OrderBy(k => k, StringComparer.Ordinal));

            log.Log($"  Personajes configurados: {string.Join(", ", cfg.Personajes)}", LogLevel.Info);
            log.Log($"  Perfiles válidos en asset: {validosAsset}", LogLevel.Info);
            log.Log($"  Personajes gobernados: {gobernados}", LogLevel.Info);

            if (this.context.PersonajesSinPerfil.Count > 0)
            {
                log.Log(
                    $"  Personajes sin perfil válido: {string.Join(", ", this.context.PersonajesSinPerfil)}",
                    LogLevel.Warn);
            }

            foreach (var (id, perfil) in this.context.PersonajesGobernados.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            {
                var p = modoReloj == ModoReloj.Pelican ? perfil.Presupuesto.Pelican : perfil.Presupuesto.Vanilla;
                log.Log(
                    $"  Presupuesto [{id} / NPC {perfil.Npc}] ({modoReloj}): " +
                    $"{p.PulsosPorDiaJuego} pulsos/día ({p.ReservadosCadenas} cadenas reservadas, +{p.ExtraCompromisos} extra compromisos).",
                    LogLevel.Info);
            }

            var tokenInfo = string.IsNullOrEmpty(cfg.ApiToken) ? "sin token" : "token configurado";
            log.Log(
                $"  API motor <-> mente: {this.context.EstadoApi} | endpoint=http://127.0.0.1:{cfg.ApiPuerto}/api/{CorazonCore.VersionContrato}/ ({tokenInfo})",
                LogLevel.Info);

            var mediciones = this.context.CostesBucle.Resumen();
            if (mediciones.Count == 0)
            {
                log.Log(
                    $"  Costes del bucle: sin mediciones aún (paso «observar» cada {cfg.SensorioCadenciaTicks} ticks).",
                    LogLevel.Info);
            }
            else
            {
                foreach (var medicion in mediciones)
                {
                    log.Log(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "  Coste [{0}]: medio={1:0.000} ms, último={2:0.000} ms, máx={3:0.000} ms ({4} pasos).",
                            medicion.Paso,
                            medicion.MedioMs,
                            medicion.UltimoMs,
                            medicion.MaximoMs,
                            medicion.Muestras),
                        LogLevel.Info);
                }
            }

            if (this.context.EstadoModulos.Fallidos.Count > 0)
            {
                var fallidos = string.Join(", ", this.context.EstadoModulos.Fallidos.Select(f => $"{f.Nombre} ({f.Error.GetType().Name})"));
                log.Log($"  Módulos desactivados por fallo: {fallidos}", LogLevel.Warn);
            }
        }

        private void OnEscena(string comando, string[] args)
        {
            if (!this.TryResolverMotorYObservacion(comando, args, out var id, out var motor, out var obs))
            {
                return;
            }

            var escena = motor!.PrevisualizarEscena(obs!);
            var log = this.context.Monitor;
            log.Log($"=== Escena [{id}] (día {escena.Apendice.Dia}, hora {escena.Apendice.Hora}) ===", LogLevel.Info);
            log.Log(escena.TextoCompleto, LogLevel.Info);
        }

        private void OnPulso(string comando, string[] args)
        {
            if (!this.TryResolverMotorYObservacion(comando, args, out var id, out var motor, out var obs))
            {
                return;
            }

            var log = this.context.Monitor;
            var ahoraMs = this.context.RelojMs?.Invoke() ?? Environment.TickCount64;
            motor!.Epoca = this.context.Api.EpocaVigente ?? motor.Epoca;
            var resultado = motor.ForzarPulso(obs!, ahoraMs, "manual");

            if (!resultado.Emitido || resultado.Pulso is null || resultado.Escena is null)
            {
                log.Log(
                    $"No se pudo emitir pulso manual para «{id}»: {resultado.MotivoRechazo ?? "presupuesto agotado"}",
                    LogLevel.Warn);
                return;
            }

            var modo = this.context.ModoRelojEfectivo;
            var p = modo == ModoReloj.Pelican ? motor.Perfil.Presupuesto.Pelican : motor.Perfil.Presupuesto.Vanilla;
            var limiteOrdinario = Math.Max(0, p.PulsosPorDiaJuego - p.ReservadosCadenas);
            var gastados = motor.Estado.Contadores?.OrdinariosLibresGastados ?? 0;

            log.Log(
                $"Pulso #{resultado.Pulso.Id} emitido para «{id}» (motivo={resultado.Pulso.Motivo}, día {resultado.Pulso.TiempoJuego.Dia}, hora {resultado.Pulso.TiempoJuego.Hora}, ordinarios={gastados}/{limiteOrdinario}).",
                LogLevel.Info);
            log.Log(resultado.Escena.Escena, LogLevel.Info);
        }

        private bool TryResolverMotorYObservacion(
            string comando,
            string[] args,
            out string personajeId,
            out MotorHabitante? motor,
            out Observacion? observacion)
        {
            var log = this.context.Monitor;
            personajeId = string.Empty;
            motor = null;
            observacion = null;

            if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
            {
                var disponibles = this.context.PersonajesGobernados.Count == 0
                    ? "ninguno"
                    : string.Join(", ", this.context.PersonajesGobernados.Keys.OrderBy(k => k, StringComparer.Ordinal));
                log.Log($"Uso: {comando} <personaje> (gobernados: {disponibles}).", LogLevel.Info);
                return false;
            }

            if (!Context.IsWorldReady)
            {
                log.Log($"No hay ninguna partida cargada para ejecutar {comando}.", LogLevel.Warn);
                return false;
            }

            if (!Context.IsMainPlayer)
            {
                log.Log($"{comando} solo está disponible en el anfitrión.", LogLevel.Warn);
                return false;
            }

            var solicitado = args[0].Trim();
            if (!this.context.MotoresHabitante.TryGetValue(solicitado, out motor))
            {
                var normalizado = solicitado.ToLowerInvariant();
                if (!this.context.MotoresHabitante.TryGetValue(normalizado, out motor))
                {
                    log.Log($"Personaje «{solicitado}» desconocido o no gobernado en la partida actual.", LogLevel.Warn);
                    return false;
                }

                personajeId = normalizado;
            }
            else
            {
                personajeId = solicitado;
            }

            observacion = this.context.ProveedorObservacion?.Invoke(personajeId)
                ?? (this.context.UltimasObservaciones.TryGetValue(personajeId, out var ultima) ? ultima : null);

            if (observacion is null)
            {
                log.Log($"No hay observación disponible todavía para «{personajeId}».", LogLevel.Warn);
                return false;
            }

            return true;
        }

        private void OnMente(string comando, string[] args)
        {
            var log = this.context.Monitor;
            if (args.Length == 0)
            {
                log.Log($"MenteHabilitada = {this.context.Config.MenteHabilitada}. Uso: cp_mente <on|off>", LogLevel.Info);
                return;
            }

            var arg = args[0].Trim().ToLowerInvariant();
            switch (arg)
            {
                case "on":
                    this.context.Config.MenteHabilitada = true;
                    log.Log("MenteHabilitada = true (activada en caliente).", LogLevel.Info);
                    break;
                case "off":
                    this.context.Config.MenteHabilitada = false;
                    log.Log("MenteHabilitada = false (desactivada en caliente).", LogLevel.Info);
                    break;
                default:
                    log.Log($"Argumento no válido «{args[0]}». Uso: cp_mente <on|off>", LogLevel.Warn);
                    break;
            }
        }

        private void OnPosicion(string comando, string[] args)
        {
            var log = this.context.Monitor;
            if (!Context.IsWorldReady || Game1.player is null)
            {
                log.Log($"No hay ninguna partida cargada para ejecutar {comando}.", LogLevel.Warn);
                return;
            }

            var locJugador = Game1.currentLocation;
            var nombreLocJugador = locJugador?.NameOrUniqueName ?? locJugador?.Name ?? "(sin ubicación)";
            var casillaJugador = Game1.player.TilePoint;
            var orientJugador = NombreOrientacion(Game1.player.FacingDirection);
            var transJugador = locJugador is not null && EsCasillaTransitable(locJugador, casillaJugador.X, casillaJugador.Y);

            log.Log(
                $"Jugador en {nombreLocJugador} ({casillaJugador.X}, {casillaJugador.Y}), orient={Game1.player.FacingDirection} ({orientJugador}), transitable={transJugador}. " +
                $"JSON: \"ubicacion\": \"{nombreLocJugador}\", \"area\": [{casillaJugador.X}, {casillaJugador.Y}, 1, 1]",
                LogLevel.Info);

            if (args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
            {
                var nombreNpc = args[0].Trim();
                var npc = Game1.getCharacterFromName(nombreNpc);
                if (npc is null)
                {
                    log.Log($"NPC «{nombreNpc}» no encontrado en el juego.", LogLevel.Warn);
                    return;
                }

                var locNpc = npc.currentLocation;
                var nombreLocNpc = locNpc?.NameOrUniqueName ?? locNpc?.Name ?? "(sin ubicación)";
                var casillaNpc = npc.TilePoint;
                var orientNpc = NombreOrientacion(npc.FacingDirection);
                var transNpc = locNpc is not null && EsCasillaTransitable(locNpc, casillaNpc.X, casillaNpc.Y);

                log.Log(
                    $"NPC {npc.Name} en {nombreLocNpc} ({casillaNpc.X}, {casillaNpc.Y}), orient={npc.FacingDirection} ({orientNpc}), transitable={transNpc}. " +
                    $"JSON: \"ubicacion\": \"{nombreLocNpc}\", \"area\": [{casillaNpc.X}, {casillaNpc.Y}, 1, 1]",
                    LogLevel.Info);
            }
        }

        private void OnLugares(string comando, string[] args)
        {
            var log = this.context.Monitor;
            if (!Context.IsWorldReady)
            {
                log.Log($"No hay ninguna partida cargada para ejecutar {comando}.", LogLevel.Warn);
                return;
            }

            string? personajeId = args.Length > 0 && !string.IsNullOrWhiteSpace(args[0])
                ? args[0].Trim().ToLowerInvariant()
                : this.context.PerfilesValidos.Keys.OrderBy(k => k, StringComparer.Ordinal).FirstOrDefault();

            if (string.IsNullOrEmpty(personajeId) || !this.context.PerfilesValidos.TryGetValue(personajeId, out var perfil))
            {
                log.Log($"No se encontró un perfil válido para «{personajeId ?? "(ninguno)"}».", LogLevel.Warn);
                return;
            }

            var npc = Game1.getCharacterFromName(perfil.Npc);
            var casillaJugador = Game1.player?.TilePoint;
            var locJugadorNombre = Game1.currentLocation?.NameOrUniqueName ?? Game1.currentLocation?.Name;
            var casillaNpc = npc?.TilePoint;
            var locNpcNombre = npc?.currentLocation?.NameOrUniqueName ?? npc?.currentLocation?.Name;

            log.Log($"=== Comprobación de lugares [{perfil.Id} / NPC {perfil.Npc}] ({perfil.Lexico.Lugares.Count} lugares) ===", LogLevel.Info);
            var ok = 0;

            foreach (var lugar in perfil.Lexico.Lugares)
            {
                if (PerfilValidator.AreaPendiente(lugar))
                {
                    log.Log($"  [{lugar.Id}] PENDIENTE ({lugar.Ubicacion} [0,0,0,0])", LogLevel.Warn);
                    continue;
                }

                if (lugar.Area.Count != 4 || lugar.Area[2] <= 0 || lugar.Area[3] <= 0)
                {
                    log.Log($"  [{lugar.Id}] AREA_INVALIDA ({lugar.Ubicacion})", LogLevel.Error);
                    continue;
                }

                var loc = Game1.getLocationFromName(lugar.Ubicacion);
                if (loc is null)
                {
                    log.Log($"  [{lugar.Id}] UBICACION_NO_ENCONTRADA ({lugar.Ubicacion})", LogLevel.Error);
                    continue;
                }

                var todasTransitables = true;
                var detalleBloqueo = string.Empty;
                for (var dx = 0; dx < lugar.Area[2] && todasTransitables; dx++)
                {
                    for (var dy = 0; dy < lugar.Area[3]; dy++)
                    {
                        var tx = lugar.Area[0] + dx;
                        var ty = lugar.Area[1] + dy;
                        if (!EsCasillaTransitable(loc, tx, ty))
                        {
                            todasTransitables = false;
                            detalleBloqueo = $"casilla ({tx},{ty}) bloqueada o fuera de mapa";
                            break;
                        }
                    }
                }

                var jugadorDentro = casillaJugador is Point pj
                    && string.Equals(locJugadorNombre, lugar.Ubicacion, StringComparison.Ordinal)
                    && ContieneCasilla(lugar.Area, pj.X, pj.Y);
                var npcDentro = casillaNpc is Point pn
                    && string.Equals(locNpcNombre, lugar.Ubicacion, StringComparison.Ordinal)
                    && ContieneCasilla(lugar.Area, pn.X, pn.Y);

                var marcaPie = jugadorDentro && npcDentro
                    ? " [jugador+NPC dentro]"
                    : jugadorDentro
                        ? " [jugador dentro]"
                        : npcDentro
                            ? " [NPC dentro]"
                            : string.Empty;

                if (todasTransitables)
                {
                    ok++;
                    log.Log(
                        $"  [{lugar.Id}] OK {lugar.Ubicacion} [{string.Join(",", lugar.Area)}]{marcaPie}",
                        LogLevel.Info);
                }
                else
                {
                    log.Log(
                        $"  [{lugar.Id}] NO_TRANSITABLE {lugar.Ubicacion} [{string.Join(",", lugar.Area)}] -> {detalleBloqueo}{marcaPie}",
                        LogLevel.Warn);
                }
            }

            log.Log($"Resumen lugares [{perfil.Id}]: OK {ok}/{perfil.Lexico.Lugares.Count}.", ok == perfil.Lexico.Lugares.Count ? LogLevel.Info : LogLevel.Warn);
        }

        private static bool ContieneCasilla(System.Collections.Generic.IReadOnlyList<int> area, int x, int y)
            => x >= area[0] && x < area[0] + area[2] && y >= area[1] && y < area[1] + area[3];

        private static bool EsCasillaTransitable(GameLocation loc, int x, int y)
            => loc.isTileOnMap(x, y)
            && loc.isTilePassable(new Vector2(x, y))
            && !loc.isCollidingPosition(
                new Rectangle((x * 64) + 8, (y * 64) + 8, 48, 48),
                Game1.viewport,
                isFarmer: false,
                damagesFarmer: 0,
                glider: false,
                character: null,
                pathfinding: true,
                projectile: false,
                ignoreCharacterRequirement: true,
                skipCollisionEffects: true);

        private static string NombreOrientacion(int orientacion) => orientacion switch
        {
            0 => "norte",
            1 => "este",
            2 => "sur",
            3 => "oeste",
            _ => "desconocida",
        };
    }
}
