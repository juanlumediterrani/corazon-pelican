using System;
using System.Linq;
using CorazonPelican.Core;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Framework;
using StardewModdingAPI;

namespace CorazonPelican.Modules
{
    /// <summary>Comandos de consola <c>cp_*</c> para diagnóstico y control en caliente (ADS §12.3, CP-005).</summary>
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

            if (this.context.EstadoModulos.Fallidos.Count > 0)
            {
                var fallidos = string.Join(", ", this.context.EstadoModulos.Fallidos.Select(f => $"{f.Nombre} ({f.Error.GetType().Name})"));
                log.Log($"  Módulos desactivados por fallo: {fallidos}", LogLevel.Warn);
            }
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
