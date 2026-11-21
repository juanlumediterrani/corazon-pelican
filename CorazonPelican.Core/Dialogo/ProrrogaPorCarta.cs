using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>
    /// Representación de dominio de una carta enviada al destinatario para pedir prórroga de un compromiso
    /// cuando se agotaron plazas ordinarias y extra (P-12, R-09, CP-509).
    /// </summary>
    public sealed record CartaProrroga(
        string CartaId,
        string PeticionId,
        string CompromisoId,
        string RemitenteId,
        string RemitenteNombre,
        string Destinatario,
        string Titulo,
        string CuerpoTexto,
        TiempoJuego PlazoVigente,
        TiempoJuego PlazoPropuesto,
        bool YaEntregada);

    /// <summary>Resultado de abrir o inspeccionar una carta de prórroga como lectura o paso previo a responder.</summary>
    public sealed record VistaCartaProrroga(
        CartaProrroga Carta,
        bool PuedeResponder,
        string EstadoPeticion,
        string NotaEstado);

    /// <summary>Resultado de aplicar la respuesta del destinatario ante una carta de prórroga.</summary>
    public sealed record ResultadoRespuestaProrroga(
        bool Aplicada,
        string Codigo,
        string Mensaje,
        TiempoJuego? NuevoPlazo);

    /// <summary>
    /// Prepara, expone y resuelve peticiones de prórroga enviadas por carta sin emitir pulsos nuevos,
    /// manteniendo idempotencia en entrega y aplicación y revalidando plazo y destinatario en vivo (R-09, CP-509).
    /// </summary>
    public static class GestorProrrogaCartas
    {
        public const string PrefijoCartaId = "CorazonPelican.Prorroga.";

        /// <summary>Construye el identificador estable de carta en <c>Data/Mail</c> para un compromiso.</summary>
        public static string ConstruirCartaId(string personajeId, string compromisoId)
        {
            if (string.IsNullOrWhiteSpace(personajeId))
            {
                throw new ArgumentException("El identificador de personaje no puede estar vacío.", nameof(personajeId));
            }

            if (string.IsNullOrWhiteSpace(compromisoId))
            {
                throw new ArgumentException("El identificador de compromiso no puede estar vacío.", nameof(compromisoId));
            }

            return $"{PrefijoCartaId}{LimpiarSegmentoId(personajeId)}.{LimpiarSegmentoId(compromisoId)}";
        }

        /// <summary>
        /// Prepara una única carta por cada petición de prórroga pendiente cuyo destinatario no esté presente
        /// en la escena actual, sin mutar <see cref="CompromisoEstado.PlazoVigente"/> ni emitir pulsión.
        /// </summary>
        public static IReadOnlyList<CartaProrroga> PrepararCartasPendientes(
            EstadoPersonaje estado,
            PerfilPersonaje perfil,
            IEnumerable<string>? presentesEnEscena = null)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            var presentes = new HashSet<string>(StringComparer.Ordinal);
            if (presentesEnEscena is not null)
            {
                foreach (var entidadRef in presentesEnEscena.Where(r => !string.IsNullOrWhiteSpace(r)))
                {
                    presentes.Add(entidadRef);
                }
            }

            var cartas = new List<CartaProrroga>();
            foreach (var compromiso in estado.Compromisos)
            {
                if (!string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal)
                    || compromiso.Prorroga is not { Estado: EstadosProrroga.Pendiente } prorroga)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(compromiso.Destinatario)
                    || presentes.Contains(compromiso.Destinatario))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(prorroga.CartaId))
                {
                    prorroga.CartaId = ConstruirCartaId(perfil.Id, compromiso.Id);
                }

                cartas.Add(ConstruirCarta(compromiso, prorroga, perfil));
            }

            return cartas;
        }

        /// <summary>Marca una carta como ya entregada al buzón del destinatario (idempotente).</summary>
        public static bool MarcarEntregada(EstadoPersonaje estado, string cartaId)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (string.IsNullOrWhiteSpace(cartaId))
            {
                return false;
            }

            var compromiso = BuscarPorCarta(estado, cartaId);
            if (compromiso?.Prorroga is not { } prorroga || prorroga.Entregada)
            {
                return false;
            }

            prorroga.Entregada = true;
            return true;
        }

        /// <summary>
        /// Devuelve todas las cartas asociadas a peticiones ya preparadas (incluidas las resueltas o vencidas)
        /// para que el texto siga accesible en lectura desde <c>Data/Mail</c>.
        /// </summary>
        public static IReadOnlyList<CartaProrroga> ObtenerCartasParaAsset(EstadoPersonaje estado, PerfilPersonaje perfil)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            var salida = new List<CartaProrroga>();
            foreach (var compromiso in estado.Compromisos)
            {
                if (compromiso.Prorroga is { } prorroga && !string.IsNullOrWhiteSpace(prorroga.CartaId))
                {
                    salida.Add(ConstruirCarta(compromiso, prorroga, perfil));
                }
            }

            return salida;
        }

        /// <summary>Formatea una <see cref="CartaProrroga"/> al formato nativo de <c>Data/Mail</c> (<c>cuerpo[#]Título</c>).</summary>
        public static string FormatearParaDataMail(CartaProrroga carta)
        {
            if (carta is null)
            {
                throw new ArgumentNullException(nameof(carta));
            }

            var cuerpo = carta.CuerpoTexto
                .Replace("\r\n", "^", StringComparison.Ordinal)
                .Replace("\n", "^", StringComparison.Ordinal);
            return $"{cuerpo}^^(Al cerrar esta carta podrás responder si sigue abierta.)[#]{carta.Titulo}";
        }

        /// <summary>
        /// Inspecciona una carta al abrirla. Si el plazo ya venció estando pendiente, marca el compromiso
        /// como incumplido y deja la carta igualmente legible sin permitir responder.
        /// </summary>
        public static VistaCartaProrroga? AbrirCarta(
            EstadoPersonaje estado,
            PerfilPersonaje perfil,
            string cartaId,
            string lectorRef,
            TiempoJuego ahora)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            if (ahora is null)
            {
                throw new ArgumentNullException(nameof(ahora));
            }

            if (string.IsNullOrWhiteSpace(cartaId))
            {
                return null;
            }

            var compromiso = BuscarPorCarta(estado, cartaId);
            if (compromiso?.Prorroga is not { } prorroga)
            {
                return null;
            }

            if (string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal)
                && GestorCompromisos.EstaVencido(compromiso, ahora))
            {
                GestorCompromisos.MarcarIncumplido(estado, compromiso, ahora);
            }

            var carta = ConstruirCarta(compromiso, prorroga, perfil);
            var esDestinatario = !string.IsNullOrWhiteSpace(lectorRef)
                && string.Equals(lectorRef, compromiso.Destinatario, StringComparison.Ordinal);
            var pendiente = string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal)
                && string.Equals(prorroga.Estado, EstadosProrroga.Pendiente, StringComparison.Ordinal)
                && !GestorCompromisos.EstaVencido(compromiso, ahora);
            var puedeResponder = esDestinatario && pendiente;

            var nota = prorroga.Estado switch
            {
                EstadosProrroga.Aceptada => "Prórroga ya aceptada; no admite una segunda modificación.",
                EstadosProrroga.Rechazada => "Prórroga ya rechazada; se conserva el plazo original.",
                EstadosProrroga.Vencida => "El plazo vigente venció; esta carta permanece solo para lectura.",
                _ when string.Equals(compromiso.Estado, EstadosCompromiso.Incumplido, StringComparison.Ordinal)
                    => "El compromiso quedó incumplido; esta carta permanece solo para lectura.",
                _ when !esDestinatario => "Esta carta está dirigida únicamente a la persona del compromiso.",
                _ when puedeResponder => "Petición vigente: al cerrar la carta puedes aceptar o rechazar el nuevo plazo.",
                _ => "La petición ya no está abierta para responder.",
            };

            return new VistaCartaProrroga(carta, puedeResponder, prorroga.Estado, nota);
        }

        /// <summary>
        /// Valida destinatario, estado y hora vigente antes de aplicar la respuesta a una carta de prórroga.
        /// </summary>
        public static ResultadoRespuestaProrroga ResponderCarta(
            EstadoPersonaje estado,
            string cartaId,
            string respondienteRef,
            bool aceptar,
            TiempoJuego ahora)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (ahora is null)
            {
                throw new ArgumentNullException(nameof(ahora));
            }

            if (string.IsNullOrWhiteSpace(cartaId))
            {
                return new ResultadoRespuestaProrroga(
                    false,
                    "carta_inexistente",
                    "No se especificó ninguna carta de prórroga.",
                    null);
            }

            var compromiso = BuscarPorCarta(estado, cartaId);
            if (compromiso?.Prorroga is not { } prorroga)
            {
                return new ResultadoRespuestaProrroga(
                    false,
                    "carta_inexistente",
                    "No existe ninguna petición de prórroga asociada a esta carta.",
                    null);
            }

            if (string.IsNullOrWhiteSpace(respondienteRef)
                || !string.Equals(respondienteRef, compromiso.Destinatario, StringComparison.Ordinal))
            {
                return new ResultadoRespuestaProrroga(
                    false,
                    "no_autorizado",
                    "Solo el destinatario del compromiso puede responder a esta carta.",
                    null);
            }

            if (string.Equals(compromiso.Estado, EstadosCompromiso.Incumplido, StringComparison.Ordinal)
                || string.Equals(prorroga.Estado, EstadosProrroga.Vencida, StringComparison.Ordinal))
            {
                return new ResultadoRespuestaProrroga(
                    false,
                    "plazo_vencido",
                    "El plazo del compromiso ya venció y no puede reactivarse con una respuesta tardía.",
                    null);
            }

            if (!string.Equals(compromiso.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal)
                || !string.Equals(prorroga.Estado, EstadosProrroga.Pendiente, StringComparison.Ordinal))
            {
                return new ResultadoRespuestaProrroga(
                    false,
                    "ya_resuelta",
                    "Esta petición de prórroga ya había sido respondida.",
                    ClonarTiempo(compromiso.PlazoVigente));
            }

            if (GestorCompromisos.EstaVencido(compromiso, ahora))
            {
                GestorCompromisos.MarcarIncumplido(estado, compromiso, ahora);
                return new ResultadoRespuestaProrroga(
                    false,
                    "plazo_vencido",
                    "El plazo vigente venció antes de recibir tu respuesta; el compromiso ha pasado a incumplido.",
                    null);
            }

            if (aceptar)
            {
                var nuevoPlazo = ClonarTiempo(prorroga.PlazoPropuesto);
                compromiso.PlazoVigente = ClonarTiempo(nuevoPlazo);
                compromiso.PlazoPropuesto = ClonarTiempo(nuevoPlazo);
                prorroga.Estado = EstadosProrroga.Aceptada;
                prorroga.ResueltaEn = ClonarTiempo(ahora);
                return new ResultadoRespuestaProrroga(
                    true,
                    "prorroga_aceptada",
                    $"Prórroga aceptada: el nuevo plazo es {FormatearTiempo(nuevoPlazo)}.",
                    ClonarTiempo(nuevoPlazo));
            }

            prorroga.Estado = EstadosProrroga.Rechazada;
            prorroga.ResueltaEn = ClonarTiempo(ahora);
            var plazoOriginal = ClonarTiempo(compromiso.PlazoVigente);
            return new ResultadoRespuestaProrroga(
                true,
                "prorroga_rechazada",
                $"Prórroga rechazada: se mantiene el plazo vigente ({FormatearTiempo(plazoOriginal)}).",
                plazoOriginal);
        }

        private static CompromisoEstado? BuscarPorCarta(EstadoPersonaje estado, string cartaId)
            => estado.Compromisos.FirstOrDefault(
                c => c.Prorroga is not null && string.Equals(c.Prorroga.CartaId, cartaId, StringComparison.Ordinal));

        private static CartaProrroga ConstruirCarta(
            CompromisoEstado compromiso,
            PeticionProrrogaEstado prorroga,
            PerfilPersonaje perfil)
        {
            var remitente = string.IsNullOrWhiteSpace(perfil.Npc) ? perfil.Id : perfil.Npc;
            var objeto = string.IsNullOrWhiteSpace(compromiso.Objeto) ? prorroga.Objeto : compromiso.Objeto;
            var plazoVigente = ClonarTiempo(compromiso.PlazoVigente);
            var plazoPropuesto = ClonarTiempo(prorroga.PlazoPropuesto);
            var titulo = $"Carta de {remitente} · Prórroga";
            var cuerpo =
                $"Sobre lo que te prometí ({objeto}): hoy tengo el día demasiado apretado para llegar a tiempo." +
                $"\nEl plazo vigente es {FormatearTiempo(plazoVigente)}, y quería pedirte si podemos dejarlo para {FormatearTiempo(plazoPropuesto)}." +
                $"\n\n— {remitente}";

            return new CartaProrroga(
                prorroga.CartaId!,
                prorroga.Id,
                compromiso.Id,
                perfil.Id,
                remitente,
                compromiso.Destinatario,
                titulo,
                cuerpo,
                plazoVigente,
                plazoPropuesto,
                prorroga.Entregada);
        }

        private static string FormatearTiempo(TiempoJuego? t)
        {
            var normalizado = ClonarTiempo(t);
            var horas = normalizado.Hora / 100;
            var minutos = normalizado.Hora % 100;
            return string.Create(CultureInfo.InvariantCulture, $"día {normalizado.Dia}, {horas:00}:{minutos:00}");
        }

        private static TiempoJuego ClonarTiempo(TiempoJuego? origen)
            => new()
            {
                Dia = Math.Max(1, origen?.Dia ?? 1),
                Hora = Math.Clamp(origen?.Hora ?? HoraJuego.InicioDia, HoraJuego.InicioDia, HoraJuego.FinDia),
            };

        private static string LimpiarSegmentoId(string raw)
            => new string(raw.Trim().Select(ch => char.IsLetterOrDigit(ch) || ch == '_' || ch == '-' ? ch : '_').ToArray());
    }
}
