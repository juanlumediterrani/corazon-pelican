using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>Entrada deserializada de <c>GET /api/v1/host/events?since=N</c> en Pelican 4Ever (CP-602).</summary>
    public sealed class EventoHostPelican
    {
        [JsonPropertyName("Id")] public long Id { get; set; }
        [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("TimestampUtc")] public string TimestampUtc { get; set; } = string.Empty;
        [JsonPropertyName("GameDay")] public int GameDay { get; set; }
        [JsonPropertyName("GameTime")] public int GameTime { get; set; }
        [JsonPropertyName("Summary")] public string Summary { get; set; } = string.Empty;
        [JsonPropertyName("Data")] public Dictionary<string, JsonElement>? Data { get; set; }
    }

    /// <summary>Respuesta incremental de <c>GET /api/v1/host/events?since=N</c> (CP-602).</summary>
    public sealed class RespuestaEventosPelican
    {
        [JsonPropertyName("SinceId")] public long SinceId { get; set; }
        [JsonPropertyName("LastEventId")] public long LastEventId { get; set; }
        [JsonPropertyName("Events")] public List<EventoHostPelican>? Events { get; set; }
    }

    /// <summary>Par proyectado al dominio puro del sensorio a partir de un evento de Pelican 4Ever.</summary>
    public sealed record SucesoPelicanProyectado(
        long IdEvento,
        EntidadObservada Entidad,
        SucesoObservado Suceso);

    /// <summary>
    /// Convierte eventos de Pelican 4Ever (<c>player.connected</c>, festivales, etc.) al modelo puro de
    /// <see cref="Observacion"/> sin dependencias de HTTP ni SMAPI (ADS §11.1, CP-602).
    /// </summary>
    public static class ConversorSucesosPelican
    {
        public static bool EsConfigurado(string? pelicanApiUrl)
            => !string.IsNullOrWhiteSpace(pelicanApiUrl);

        public static IReadOnlyList<EventoHostPelican> ParsearRespuestaJson(string json, out long lastEventId)
        {
            lastEventId = 0;
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<EventoHostPelican>();
            }

            var dto = JsonSerializer.Deserialize<RespuestaEventosPelican>(
                json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (dto is null)
            {
                return Array.Empty<EventoHostPelican>();
            }

            lastEventId = dto.LastEventId;
            return dto.Events ?? (IReadOnlyList<EventoHostPelican>)Array.Empty<EventoHostPelican>();
        }

        public static SucesoPelicanProyectado? Convertir(EventoHostPelican? evento, string ubicacionActual, int x, int y)
        {
            if (evento is null || string.IsNullOrWhiteSpace(evento.Type))
            {
                return null;
            }

            var tipo = evento.Type.Trim();
            if (!EsTipoDiegetico(tipo))
            {
                return null;
            }

            string descripcion;
            string? actorRef = ExtraerCadena(evento.Data, "playerId")
                ?? ExtraerCadena(evento.Data, "farmerId");
            var nombreJugador = ExtraerCadena(evento.Data, "playerName")
                ?? ExtraerCadena(evento.Data, "name")
                ?? ExtraerCadena(evento.Data, "farmerName");
            var etiquetas = new List<string> { "suceso" };

            if (string.Equals(tipo, "player.connected", StringComparison.OrdinalIgnoreCase))
            {
                var quien = string.IsNullOrWhiteSpace(nombreJugador) ? "alguien nuevo" : nombreJugador.Trim();
                descripcion = $"ha llegado {quien} al valle";
                etiquetas.Add("persona");
                if (!string.IsNullOrWhiteSpace(actorRef) && !actorRef.StartsWith("farmer:", StringComparison.Ordinal))
                {
                    actorRef = $"farmer:{actorRef}";
                }
            }
            else if (string.Equals(tipo, "player.disconnected", StringComparison.OrdinalIgnoreCase))
            {
                var quien = string.IsNullOrWhiteSpace(nombreJugador) ? "un vecino" : nombreJugador.Trim();
                descripcion = $"se ha marchado {quien} del valle";
            }
            else if (tipo.StartsWith("festival.", StringComparison.OrdinalIgnoreCase))
            {
                var detalle = string.IsNullOrWhiteSpace(evento.Summary) ? tipo : evento.Summary.Trim();
                descripcion = $"celebración en el valle: {detalle}";
            }
            else if (string.Equals(tipo, "host.announcement", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(evento.Summary))
            {
                descripcion = evento.Summary.Trim();
            }
            else
            {
                return null;
            }

            var refSuceso = $"suceso:pelican:{evento.Id}";
            var entidad = new EntidadObservada(
                Ref: refSuceso,
                Categoria: CategoriaEntidad.Suceso,
                Nombre: descripcion,
                Ubicacion: ubicacionActual,
                X: x,
                Y: y,
                Etiquetas: etiquetas,
                Visible: true,
                Distancia: 1.0);

            var suceso = new SucesoObservado(
                Ref: refSuceso,
                Tipo: tipo,
                Descripcion: descripcion,
                ActorRef: actorRef);

            return new SucesoPelicanProyectado(evento.Id, entidad, suceso);
        }

        public static bool EsTipoDiegetico(string? tipo)
        {
            if (string.IsNullOrWhiteSpace(tipo))
            {
                return false;
            }

            var t = tipo.Trim();
            return string.Equals(t, "player.connected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "player.disconnected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(t, "host.announcement", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("festival.", StringComparison.OrdinalIgnoreCase);
        }

        private static string? ExtraerCadena(Dictionary<string, JsonElement>? data, string clave)
        {
            if (data is null)
            {
                return null;
            }

            foreach (var par in data)
            {
                if (string.Equals(par.Key, clave, StringComparison.OrdinalIgnoreCase))
                {
                    return par.Value.ValueKind switch
                    {
                        JsonValueKind.String => par.Value.GetString(),
                        JsonValueKind.Number => par.Value.GetRawText(),
                        _ => null,
                    };
                }
            }

            return null;
        }
    }

    /// <summary>Cola concurrente entre el hilo HTTP de fondo y el hilo principal de observación (CP-602, CP-607).</summary>
    public sealed class ColaSucesosPelican
    {
        private const int CapacidadMaxima = 128;

        private readonly object sync = new();
        private readonly List<EventoHostPelican> eventos = new();
        private readonly Dictionary<string, long> ultimoIdPorPersonaje = new(StringComparer.Ordinal);
        private long ultimoIdEncolado;
        private long secuenciaSintetica;

        public int CantidadPendiente
        {
            get
            {
                lock (this.sync)
                {
                    return this.eventos.Count;
                }
            }
        }

        public void Encolar(EventoHostPelican evento)
        {
            if (evento is null || string.IsNullOrWhiteSpace(evento.Type))
            {
                return;
            }

            if (!ConversorSucesosPelican.EsTipoDiegetico(evento.Type))
            {
                return;
            }

            lock (this.sync)
            {
                var idEfectivo = evento.Id;
                if (idEfectivo > 0)
                {
                    if (idEfectivo <= this.ultimoIdEncolado)
                    {
                        return;
                    }

                    this.ultimoIdEncolado = idEfectivo;
                }
                else
                {
                    this.secuenciaSintetica--;
                    evento = new EventoHostPelican
                    {
                        Id = this.secuenciaSintetica,
                        Type = evento.Type,
                        TimestampUtc = evento.TimestampUtc,
                        GameDay = evento.GameDay,
                        GameTime = evento.GameTime,
                        Summary = evento.Summary,
                        Data = evento.Data,
                    };
                }

                this.eventos.Add(evento);
                if (this.eventos.Count > CapacidadMaxima)
                {
                    this.eventos.RemoveRange(0, this.eventos.Count - CapacidadMaxima);
                }
            }
        }

        public Observacion AplicarAObservacion(Observacion observacion)
            => this.AplicarAObservacion(observacion, personajeId: null);

        public Observacion AplicarAObservacion(Observacion observacion, string? personajeId)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            List<EventoHostPelican>? lote = null;
            lock (this.sync)
            {
                if (this.eventos.Count == 0)
                {
                    return observacion;
                }

                if (string.IsNullOrWhiteSpace(personajeId))
                {
                    lote = new List<EventoHostPelican>(this.eventos);
                    this.eventos.Clear();
                }
                else
                {
                    var clave = personajeId.Trim();
                    this.ultimoIdPorPersonaje.TryGetValue(clave, out var ultimoVisto);
                    lote = new List<EventoHostPelican>();
                    var nuevoUltimo = ultimoVisto;
                    foreach (var ev in this.eventos)
                    {
                        if (ev.Id > ultimoVisto || (ev.Id < 0 && !this.ultimoIdPorPersonaje.ContainsKey(clave)))
                        {
                            lote.Add(ev);
                            if (ev.Id > nuevoUltimo)
                            {
                                nuevoUltimo = ev.Id;
                            }
                        }
                    }

                    this.ultimoIdPorPersonaje[clave] = nuevoUltimo;
                }
            }

            if (lote is null || lote.Count == 0)
            {
                return observacion;
            }

            var entidades = observacion.Entidades.ToList();
            var sucesos = observacion.Sucesos?.ToList() ?? new List<SucesoObservado>();
            var cambio = false;

            foreach (var evento in lote)
            {
                var proyectado = ConversorSucesosPelican.Convertir(evento, observacion.Ubicacion, observacion.X, observacion.Y);
                if (proyectado is null)
                {
                    continue;
                }

                entidades.Add(proyectado.Entidad);
                sucesos.Add(proyectado.Suceso);
                cambio = true;
            }

            return cambio
                ? observacion with { Entidades = entidades, Sucesos = sucesos }
                : observacion;
        }
    }
}
