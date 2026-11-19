using System;
using System.Globalization;
using System.Linq;
using CorazonPelican.Core;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Framework;
using StardewModdingAPI;

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
    }
}
