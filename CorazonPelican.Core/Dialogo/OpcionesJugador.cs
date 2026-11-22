using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>Objeto presente en el inventario del jugador evaluable como regalo (CP-504).</summary>
    public sealed record ObjetoInventarioJugador(
        string ItemId,
        string Nombre,
        bool EsRegalable = true,
        int Cantidad = 1);

    /// <summary>Encargo candidato del jugador si la apertura del personaje lo permite (CP-504).</summary>
    public sealed record PropuestaEncargo(string Id, string Etiqueta);

    /// <summary>Entrada pura para construir las 3–4 opciones concretas del jugador más «Decir otra cosa…».</summary>
    public sealed record ContextoOpcionesJugador(
        PerfilPersonaje Perfil,
        int Corazones,
        IReadOnlyList<ObjetoInventarioJugador> Inventario,
        IReadOnlyList<PropuestaEncargo> EncargosDisponibles,
        IReadOnlyList<Lugar>? LugaresCita = null,
        int MaxOpciones = 3);

    /// <summary>Opción ofrecida al jugador con identificador estable para humanos y agentes (Voyager).</summary>
    public sealed record OpcionJugador(
        string Id,
        string Etiqueta,
        string Categoria,
        string? ItemId = null,
        string? LugarId = null,
        string? EncargoId = null);

    /// <summary>
    /// Genera opciones de diálogo desde affordances reales del jugador y apertura del personaje (GSS §8.4, ADS §8.3, CP-504).
    /// </summary>
    public static class GeneradorOpcionesJugador
    {
        public const int MaxOpcionesConcretas = 4;
        public const int LongitudMaximaTextoLibre = 160;
        public const string IdDecirOtraCosa = "decir_otra_cosa";
        public const string TextoDecirOtraCosa = "Decir otra cosa…";

        public static IReadOnlyList<OpcionJugador> Generar(ContextoOpcionesJugador contexto)
        {
            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            if (contexto.Perfil is null)
            {
                throw new ArgumentNullException(nameof(contexto), "El perfil del personaje es obligatorio.");
            }

            var tope = Math.Clamp(contexto.MaxOpciones, 1, MaxOpcionesConcretas);
            var apertura = AperturaPerfil.TramoPara(contexto.Perfil, contexto.Corazones);
            var candidatas = new List<OpcionJugador>();

            // 1. Inventario regalable real (nunca ofrecer algo ausente del inventario).
            if (contexto.Inventario is { Count: > 0 })
            {
                var regalos = contexto.Inventario
                    .Where(i => i is not null
                        && i.EsRegalable
                        && i.Cantidad > 0
                        && !string.IsNullOrWhiteSpace(i.ItemId)
                        && !string.IsNullOrWhiteSpace(i.Nombre))
                    .GroupBy(i => i.ItemId.Trim(), StringComparer.Ordinal)
                    .Select(g => g.First());

                foreach (var regalo in regalos)
                {
                    var idLimpio = regalo.ItemId.Trim();
                    candidatas.Add(new OpcionJugador(
                        Id: $"regalar:{idLimpio}",
                        Etiqueta: $"Regalar {regalo.Nombre.Trim()}",
                        Categoria: "regalar",
                        ItemId: idLimpio));
                }
            }

            // 2. Encargos solo si la apertura vigente del personaje lo permite.
            if (apertura.Encargos && contexto.EncargosDisponibles is { Count: > 0 })
            {
                foreach (var encargo in contexto.EncargosDisponibles
                    .Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Id) && !string.IsNullOrWhiteSpace(e.Etiqueta)))
                {
                    var idLimpio = encargo.Id.Trim();
                    candidatas.Add(new OpcionJugador(
                        Id: $"encargo:{idLimpio}",
                        Etiqueta: encargo.Etiqueta.Trim(),
                        Categoria: "encargo",
                        EncargoId: idLimpio));
                }
            }

            // 3. Lugares para citas desde el léxico del perfil (o lista explícita), sin áreas pendientes.
            var lugaresFuente = contexto.LugaresCita ?? contexto.Perfil.Lexico.Lugares;
            if (lugaresFuente is { Count: > 0 })
            {
                foreach (var lugar in lugaresFuente
                    .Where(l => l is not null
                        && !string.IsNullOrWhiteSpace(l.Id)
                        && !string.IsNullOrWhiteSpace(l.Prosa)
                        && !PerfilValidator.AreaPendiente(l)))
                {
                    var idLimpio = lugar.Id.Trim();
                    candidatas.Add(new OpcionJugador(
                        Id: $"cita:{idLimpio}",
                        Etiqueta: $"Quedar en {lugar.Prosa.Trim()}",
                        Categoria: "cita",
                        LugarId: idLimpio));
                }
            }

            // Selección equilibrada por categoría respetando el orden determinista.
            var seleccionadas = new List<OpcionJugador>(tope + 1);
            var vistas = new HashSet<string>(StringComparer.Ordinal);

            foreach (var categoria in new[] { "regalar", "encargo", "cita" })
            {
                if (seleccionadas.Count >= tope)
                {
                    break;
                }

                var primera = candidatas.FirstOrDefault(c => c.Categoria == categoria && !vistas.Contains(c.Id));
                if (primera is not null)
                {
                    seleccionadas.Add(primera);
                    vistas.Add(primera.Id);
                }
            }

            foreach (var restante in candidatas)
            {
                if (seleccionadas.Count >= tope)
                {
                    break;
                }

                if (vistas.Add(restante.Id))
                {
                    seleccionadas.Add(restante);
                }
            }

            seleccionadas.Add(new OpcionJugador(
                Id: IdDecirOtraCosa,
                Etiqueta: TextoDecirOtraCosa,
                Categoria: "texto_libre"));

            return seleccionadas;
        }

        /// <summary>
        /// Normaliza el texto libre del jugador conservándolo tal cual (sin censura ni reescritura) y aplicando el límite máximo.
        /// </summary>
        public static string? NormalizarTextoLibre(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return null;
            }

            var limpio = texto.Trim();
            return limpio.Length > LongitudMaximaTextoLibre
                ? limpio.Substring(0, LongitudMaximaTextoLibre)
                : limpio;
        }
    }
}
