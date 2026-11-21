using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;
using CorazonPelican.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network.NetEvents;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Prepara y entrega cartas individuales cuando se agotan los cupos de compromiso con el destinatario
    /// ausente y ofrece la vía de respuesta al cerrar la carta (P-12, R-09, CP-509).
    /// </summary>
    internal sealed class CorreoProrrogaModule : IModule, IDisposable
    {
        private readonly HashSet<string> cartasRegistradasEnAsset = new(StringComparer.Ordinal);
        private ModContext? context;

        public string Nombre => "CorreoProrroga";

        public LadoModulo Lado => LadoModulo.Anfitrion;

        public void Registrar(ModContext contexto)
        {
            this.context = contexto;

            contexto.Helper.Events.Content.AssetRequested += this.OnAssetRequested;
            contexto.Helper.Events.GameLoop.DayStarted += this.OnDayStarted;
            contexto.Helper.Events.GameLoop.OneSecondUpdateTicked += this.OnOneSecondUpdateTicked;
            contexto.Helper.Events.GameLoop.ReturnedToTitle += this.OnReturnedToTitle;
            contexto.Helper.Events.Display.MenuChanged += this.OnMenuChanged;
        }

        public void Dispose()
        {
            if (this.context is null)
            {
                return;
            }

            this.context.Helper.Events.Content.AssetRequested -= this.OnAssetRequested;
            this.context.Helper.Events.GameLoop.DayStarted -= this.OnDayStarted;
            this.context.Helper.Events.GameLoop.OneSecondUpdateTicked -= this.OnOneSecondUpdateTicked;
            this.context.Helper.Events.GameLoop.ReturnedToTitle -= this.OnReturnedToTitle;
            this.context.Helper.Events.Display.MenuChanged -= this.OnMenuChanged;
            this.cartasRegistradasEnAsset.Clear();
        }

        private void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e)
            => this.cartasRegistradasEnAsset.Clear();

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            if (this.context is null || !e.NameWithoutLocale.IsEquivalentTo("Data/Mail"))
            {
                return;
            }

            e.Edit(asset =>
            {
                var mail = asset.AsDictionary<string, string>().Data;
                foreach (var (id, perfil) in this.context.PersonajesGobernados)
                {
                    var estado = this.context.EstadoPartida.ObtenerSiExiste(id);
                    if (estado is null)
                    {
                        continue;
                    }

                    foreach (var carta in GestorProrrogaCartas.ObtenerCartasParaAsset(estado, perfil))
                    {
                        mail[carta.CartaId] = GestorProrrogaCartas.FormatearParaDataMail(carta);
                    }
                }
            }, AssetEditPriority.Late);
        }

        private void OnDayStarted(object? sender, DayStartedEventArgs e)
            => this.SincronizarYEntregarCartas();

        private void OnOneSecondUpdateTicked(object? sender, OneSecondUpdateTickedEventArgs e)
            => this.SincronizarYEntregarCartas();

        private void SincronizarYEntregarCartas()
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            var invalidarMail = false;
            var agricultores = Game1.getAllFarmers()?.ToList();

            foreach (var (id, perfil) in this.context.PersonajesGobernados)
            {
                var estado = this.context.EstadoPartida.ObtenerSiExiste(id);
                if (estado is null)
                {
                    continue;
                }

                IEnumerable<string>? presentes = this.context.UltimasObservaciones.TryGetValue(id, out var obs)
                    ? obs.Entidades.Select(ent => ent.Ref)
                    : null;

                var cartas = GestorProrrogaCartas.PrepararCartasPendientes(estado, perfil, presentes);
                foreach (var carta in cartas)
                {
                    if (this.cartasRegistradasEnAsset.Add(carta.CartaId))
                    {
                        invalidarMail = true;
                    }

                    if (carta.YaEntregada || agricultores is null)
                    {
                        continue;
                    }

                    var destinatario = agricultores.FirstOrDefault(
                        f => string.Equals(
                            IdentidadesObservacion.Granjero(f.UniqueMultiplayerID),
                            carta.Destinatario,
                            StringComparison.Ordinal));
                    if (destinatario is null)
                    {
                        continue;
                    }

                    if (!destinatario.hasOrWillReceiveMail(carta.CartaId))
                    {
                        Game1.player.team.RequestSetMail(
                            PlayerActionTarget.All,
                            carta.CartaId,
                            MailType.Now,
                            add: true,
                            onlyPlayerId: destinatario.UniqueMultiplayerID);
                        this.context.Monitor.Log(
                            $"Carta de prórroga «{carta.CartaId}» enviada a {carta.Destinatario}.",
                            LogLevel.Trace);
                    }

                    GestorProrrogaCartas.MarcarEntregada(estado, carta.CartaId);
                }
            }

            if (invalidarMail)
            {
                this.context.Helper.GameContent.InvalidateCache("Data/Mail");
            }
        }

        private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
        {
            if (this.context is null
                || !Context.IsWorldReady
                || !Context.IsMainPlayer
                || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            if (e.OldMenu is not LetterViewerMenu letter
                || e.NewMenu is not null
                || string.IsNullOrWhiteSpace(letter.mailTitle)
                || !letter.mailTitle.StartsWith(GestorProrrogaCartas.PrefijoCartaId, StringComparison.Ordinal))
            {
                return;
            }

            var lector = Game1.player;
            var ubicacion = Game1.currentLocation;
            if (lector is null || ubicacion is null)
            {
                return;
            }

            var coincidencia = this.BuscarEstadoPorCarta(letter.mailTitle);
            if (coincidencia is null)
            {
                return;
            }

            var (estado, perfil, compromiso) = coincidencia.Value;
            var lectorRef = IdentidadesObservacion.Granjero(lector.UniqueMultiplayerID);
            var vista = GestorProrrogaCartas.AbrirCarta(estado, perfil, letter.mailTitle, lectorRef, TiempoJuegoActual());
            if (vista is null)
            {
                return;
            }

            if (!vista.PuedeResponder)
            {
                this.context.Monitor.Log($"Carta «{letter.mailTitle}» abierta en solo lectura: {vista.NotaEstado}", LogLevel.Trace);
                return;
            }

            var plazoPropuestoTexto = FormatearTiempo(vista.Carta.PlazoPropuesto);
            var plazoOriginalTexto = FormatearTiempo(vista.Carta.PlazoVigente);
            var objeto = string.IsNullOrWhiteSpace(compromiso.Objeto) ? "lo prometido" : compromiso.Objeto;
            var pregunta = $"{vista.Carta.RemitenteNombre} pide aplazar «{objeto}» del {plazoOriginalTexto} al {plazoPropuestoTexto}.";
            var respuestas = new[]
            {
                new Response("aceptar", $"Aceptar prórroga ({plazoPropuestoTexto})"),
                new Response("rechazar", $"Mantener plazo original ({plazoOriginalTexto})"),
            };

            var npc = Game1.getCharacterFromName(perfil.Npc);
            ubicacion.createQuestionDialogue(
                pregunta,
                respuestas,
                (who, clave) => this.AplicarRespuestaDesdeMenu(estado, letter.mailTitle, who, clave),
                speaker: npc);
        }

        private void AplicarRespuestaDesdeMenu(EstadoPersonaje estado, string cartaId, Farmer who, string clave)
        {
            if (this.context is null || !Context.IsWorldReady || !Context.IsMainPlayer || !this.context.MotorActivoEnPartida)
            {
                return;
            }

            var respondienteRef = IdentidadesObservacion.Granjero(who.UniqueMultiplayerID);
            var aceptar = string.Equals(clave, "aceptar", StringComparison.Ordinal);
            var resultado = GestorProrrogaCartas.ResponderCarta(estado, cartaId, respondienteRef, aceptar, TiempoJuegoActual());
            this.context.Monitor.Log(
                $"Respuesta a carta de prórroga «{cartaId}» ({respondienteRef}): {resultado.Codigo} — {resultado.Mensaje}",
                resultado.Aplicada ? LogLevel.Info : LogLevel.Warn);

            if (resultado.Aplicada || resultado.Codigo == "plazo_vencido")
            {
                this.context.Helper.GameContent.InvalidateCache("Data/Mail");
            }
        }

        private (EstadoPersonaje Estado, PerfilPersonaje Perfil, CompromisoEstado Compromiso)? BuscarEstadoPorCarta(string cartaId)
        {
            if (this.context is null)
            {
                return null;
            }

            foreach (var (id, perfil) in this.context.PersonajesGobernados)
            {
                var estado = this.context.EstadoPartida.ObtenerSiExiste(id);
                if (estado is null)
                {
                    continue;
                }

                var compromiso = estado.Compromisos.FirstOrDefault(
                    c => c.Prorroga is not null && string.Equals(c.Prorroga.CartaId, cartaId, StringComparison.Ordinal));
                if (compromiso is not null)
                {
                    return (estado, perfil, compromiso);
                }
            }

            return null;
        }

        private static TiempoJuego TiempoJuegoActual()
            => new()
            {
                Dia = Math.Max(1, Game1.Date?.TotalDays ?? 1),
                Hora = Math.Clamp(Game1.timeOfDay, HoraJuego.InicioDia, HoraJuego.FinDia),
            };

        private static string FormatearTiempo(TiempoJuego tiempo)
            => string.Create(CultureInfo.InvariantCulture, $"día {tiempo.Dia}, {tiempo.Hora / 100:00}:{tiempo.Hora % 100:00}");
    }
}
