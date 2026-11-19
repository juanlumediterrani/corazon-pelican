using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>
    /// Almacén persistido por partida con el estado de todos los personajes gobernados (ADS §9, P-6, CP-107).
    /// </summary>
    [DataContract]
    public sealed class EstadoPartida
    {
        public const int VersionActual = 2;

        [JsonPropertyName("version_esquema")]
        [DataMember(Name = "version_esquema")]
        public int VersionEsquema { get; set; } = VersionActual;

        [JsonPropertyName("personajes")]
        [DataMember(Name = "personajes")]
        public Dictionary<string, EstadoPersonaje> Personajes { get; set; } = new(StringComparer.Ordinal);

        /// <summary>
        /// Devuelve el estado del personaje indicado en la partida actual, creándolo si aún no existía.
        /// </summary>
        public EstadoPersonaje ObtenerOIniciar(string personajeId)
        {
            if (string.IsNullOrWhiteSpace(personajeId))
            {
                throw new ArgumentException("El identificador de personaje no puede estar vacío.", nameof(personajeId));
            }

            if (!this.Personajes.TryGetValue(personajeId, out var estado) || estado is null)
            {
                estado = new EstadoPersonaje();
                this.Personajes[personajeId] = estado;
            }

            return estado;
        }

        /// <summary>Devuelve el estado del personaje si ya existe en la partida, o <c>null</c>.</summary>
        public EstadoPersonaje? ObtenerSiExiste(string personajeId)
        {
            if (string.IsNullOrWhiteSpace(personajeId))
            {
                return null;
            }

            return this.Personajes.TryGetValue(personajeId, out var estado) ? estado : null;
        }
    }

    /// <summary>
    /// Serialización determinista, deserialización con migración y validación del estado persistido (CP-107, R-10).
    /// </summary>
    public static class PersistenciaEstado
    {
        /// <summary>Clave usada con <c>Helper.Data.ReadSaveData</c> / <c>WriteSaveData</c> (ADS §9).</summary>
        public const string ClaveGuardado = "CorazonPelican.Estado";

        private static readonly JsonSerializerOptions OpcionesEscrituraCompacta = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly JsonSerializerOptions OpcionesEscrituraIndentada = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private static readonly JsonSerializerOptions OpcionesLectura = new()
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// Serializa <paramref name="estado"/> de forma determinista ordenando las claves de diccionarios
        /// sin alterar el orden de las listas (diario, recuerdos, portafolio, compromisos).
        /// </summary>
        public static string Serializar(EstadoPartida estado, bool indentado = false)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            var canonico = Canonizar(estado);
            return JsonSerializer.Serialize(
                canonico,
                indentado ? OpcionesEscrituraIndentada : OpcionesEscrituraCompacta);
        }

        /// <summary>Serializa de forma determinista el estado de un personaje individual.</summary>
        public static string SerializarPersonaje(EstadoPersonaje estado, bool indentado = false)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            var canonico = CanonizarPersonaje(estado);
            return JsonSerializer.Serialize(
                canonico,
                indentado ? OpcionesEscrituraIndentada : OpcionesEscrituraCompacta);
        }

        /// <summary>
        /// Deserializa un <see cref="EstadoPartida"/> desde JSON y aplica las migraciones necesarias hasta
        /// <see cref="EstadoPartida.VersionActual"/>.
        /// </summary>
        public static EstadoPartida Deserializar(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("El JSON del estado no puede estar vacío.", nameof(json));
            }

            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("El estado de partida serializado debe ser un objeto JSON.");
            }

            var tieneVersionRaiz = TryLeerVersion(documento.RootElement, out var versionRaiz);
            if (tieneVersionRaiz && (versionRaiz < 0 || versionRaiz > EstadoPartida.VersionActual))
            {
                throw new NotSupportedException(
                    $"Versión de esquema de partida no soportada: v{versionRaiz} (máxima soportada: v{EstadoPartida.VersionActual}).");
            }

            var estado = JsonSerializer.Deserialize<EstadoPartida>(json, OpcionesLectura)
                ?? throw new JsonException("No se pudo deserializar el estado de la partida.");

            if (!tieneVersionRaiz)
            {
                estado.VersionEsquema = 1;
            }

            if (TryObtenerPropiedadInsensible(documento.RootElement, "personajes", out var elemPersonajes)
                && elemPersonajes.ValueKind == JsonValueKind.Object
                && estado.Personajes is not null)
            {
                foreach (var prop in elemPersonajes.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Object
                        && !TryLeerVersion(prop.Value, out _)
                        && estado.Personajes.TryGetValue(prop.Name, out var personaje)
                        && personaje is not null)
                    {
                        personaje.VersionEsquema = 1;
                    }
                }
            }

            MigradorEstado.Migrar(estado);
            return estado;
        }

        /// <summary>
        /// Deserializa un <see cref="EstadoPersonaje"/> individual desde JSON y aplica las migraciones hasta
        /// <see cref="EstadoPersonaje.VersionActual"/>.
        /// </summary>
        public static EstadoPersonaje DeserializarPersonaje(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("El JSON del estado de personaje no puede estar vacío.", nameof(json));
            }

            using var documento = JsonDocument.Parse(json);
            if (documento.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("El estado de personaje serializado debe ser un objeto JSON.");
            }

            var tieneVersion = TryLeerVersion(documento.RootElement, out var version);
            if (tieneVersion && (version < 0 || version > EstadoPersonaje.VersionActual))
            {
                throw new NotSupportedException(
                    $"Versión de esquema de personaje no soportada: v{version} (máxima soportada: v{EstadoPersonaje.VersionActual}).");
            }

            var estado = JsonSerializer.Deserialize<EstadoPersonaje>(json, OpcionesLectura)
                ?? throw new JsonException("No se pudo deserializar el estado del personaje.");

            if (!tieneVersion)
            {
                estado.VersionEsquema = 1;
            }

            MigradorEstado.Migrar(estado);
            return estado;
        }

        /// <summary>Comprueba la validez estructural de un <see cref="EstadoPartida"/> migrado.</summary>
        public static IReadOnlyList<string> Validar(EstadoPartida estado)
        {
            var errores = new List<string>();
            if (estado is null)
            {
                errores.Add("El estado de la partida es null.");
                return errores;
            }

            if (estado.VersionEsquema != EstadoPartida.VersionActual)
            {
                errores.Add(
                    $"version_esquema de partida esperada={EstadoPartida.VersionActual}, actual={estado.VersionEsquema}.");
            }

            if (estado.Personajes is null)
            {
                errores.Add("El diccionario de personajes es null.");
                return errores;
            }

            foreach (var (personajeId, personaje) in estado.Personajes)
            {
                if (string.IsNullOrWhiteSpace(personajeId))
                {
                    errores.Add("Existe una entrada de personaje con identificador vacío.");
                    continue;
                }

                if (personaje is null)
                {
                    errores.Add($"El estado del personaje «{personajeId}» es null.");
                    continue;
                }

                if (personaje.VersionEsquema != EstadoPersonaje.VersionActual)
                {
                    errores.Add(
                        $"Personaje «{personajeId}»: version_esquema esperada={EstadoPersonaje.VersionActual}, actual={personaje.VersionEsquema}.");
                }

                if (personaje.SiguienteIdEpisodio < 1)
                {
                    errores.Add($"Personaje «{personajeId}»: siguiente_id_episodio debe ser >= 1.");
                }

                var idsDiario = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < personaje.Diario.Count; i++)
                {
                    var ep = personaje.Diario[i];
                    if (string.IsNullOrWhiteSpace(ep.Id))
                    {
                        errores.Add($"Personaje «{personajeId}»: diario[{i}] tiene id vacío.");
                    }
                    else if (!idsDiario.Add(ep.Id))
                    {
                        errores.Add($"Personaje «{personajeId}»: id de episodio duplicado «{ep.Id}».");
                    }

                    if (ep.DiaJuego < 1)
                    {
                        errores.Add($"Personaje «{personajeId}»: diario[{i}] tiene dia_juego inválido ({ep.DiaJuego}).");
                    }

                    if (ep.Hora is < HoraJuego.InicioDia or > HoraJuego.FinDia)
                    {
                        errores.Add($"Personaje «{personajeId}»: diario[{i}] tiene hora inválida ({ep.Hora}).");
                    }

                    if (ep.Saliencia < 0)
                    {
                        errores.Add($"Personaje «{personajeId}»: diario[{i}] tiene saliencia negativa ({ep.Saliencia}).");
                    }
                }

                if (personaje.Contadores is { } contadores)
                {
                    if (contadores.Dia < 1)
                    {
                        errores.Add($"Personaje «{personajeId}»: contadores.Dia debe ser >= 1.");
                    }

                    if (contadores.OrdinariosLibresGastados < 0
                        || contadores.ExtrasCompromisosGastados < 0
                        || contadores.Interlocuciones < 0)
                    {
                        errores.Add($"Personaje «{personajeId}»: los contadores de pulsos no pueden ser negativos.");
                    }
                }
            }

            return errores;
        }

        private static EstadoPartida Canonizar(EstadoPartida origen)
        {
            var ordenado = new Dictionary<string, EstadoPersonaje>(StringComparer.Ordinal);
            if (origen.Personajes is not null)
            {
                foreach (var (id, personaje) in origen.Personajes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(id) && personaje is not null)
                    {
                        ordenado[id] = CanonizarPersonaje(personaje);
                    }
                }
            }

            return new EstadoPartida
            {
                VersionEsquema = origen.VersionEsquema,
                Personajes = ordenado,
            };
        }

        private static EstadoPersonaje CanonizarPersonaje(EstadoPersonaje origen)
        {
            var relacionesOrdenadas = new Dictionary<string, RelacionEstado>(StringComparer.Ordinal);
            if (origen.Relaciones is not null)
            {
                foreach (var (clave, valor) in origen.Relaciones.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                {
                    if (!string.IsNullOrWhiteSpace(clave) && valor is not null)
                    {
                        relacionesOrdenadas[clave] = valor;
                    }
                }
            }

            return new EstadoPersonaje
            {
                VersionEsquema = origen.VersionEsquema,
                SiguienteIdEpisodio = origen.SiguienteIdEpisodio,
                Diario = origen.Diario ?? new List<EpisodioDiario>(),
                Recuerdos = origen.Recuerdos ?? new List<Recuerdo>(),
                Relaciones = relacionesOrdenadas,
                Portafolio = origen.Portafolio ?? new List<ObraPortafolio>(),
                Compromisos = origen.Compromisos ?? new List<CompromisoEstado>(),
                Contadores = origen.Contadores,
                IntencionActiva = origen.IntencionActiva,
            };
        }

        private static bool TryLeerVersion(JsonElement objeto, out int version)
        {
            version = 0;
            if (!TryObtenerPropiedadInsensible(objeto, "version_esquema", out var prop))
            {
                return false;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out version))
            {
                return true;
            }

            throw new JsonException("El campo 'version_esquema' debe ser un número entero.");
        }

        private static bool TryObtenerPropiedadInsensible(JsonElement objeto, string nombre, out JsonElement valor)
        {
            if (objeto.TryGetProperty(nombre, out valor))
            {
                return true;
            }

            foreach (var prop in objeto.EnumerateObject())
            {
                if (string.Equals(prop.Name, nombre, StringComparison.OrdinalIgnoreCase))
                {
                    valor = prop.Value;
                    return true;
                }
            }

            valor = default;
            return false;
        }
    }
}
