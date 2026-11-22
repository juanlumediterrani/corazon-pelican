using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>Resultado de validar que toda entidad nombrada existe en escena o memoria (R-15, CP-506).</summary>
    public sealed record ResultadoMenciones(
        bool EsValido,
        IReadOnlyList<string> MencionesDetectadas,
        IReadOnlyList<string> MencionesNoAncladas,
        IReadOnlyList<MotivoRechazo> Motivos);

    /// <summary>Estadística acumulada de menciones para un personaje y día de juego.</summary>
    public sealed record EstadisticaMencionesDia(
        string Personaje,
        int DiaJuego,
        int MencionesTotales,
        int MencionesNoAncladas)
    {
        public double TasaNoAncladas => this.MencionesTotales <= 0
            ? 0.0
            : (double)this.MencionesNoAncladas / this.MencionesTotales;
    }

    /// <summary>Acumulador diario de tasa de menciones no ancladas para telemetría en <c>rechazos.jsonl</c>.</summary>
    public sealed class RastreadorTasaMenciones
    {
        private readonly Dictionary<(string Personaje, int Dia), (int Totales, int NoAncladas)> acumulado = new();

        public EstadisticaMencionesDia Registrar(string personaje, int diaJuego, ResultadoMenciones resultado)
        {
            var id = string.IsNullOrWhiteSpace(personaje) ? "desconocido" : personaje;
            var dia = Math.Max(1, diaJuego);
            var clave = (id, dia);

            var actual = this.acumulado.TryGetValue(clave, out var previo) ? previo : (Totales: 0, NoAncladas: 0);
            var nuevosTotales = actual.Totales + resultado.MencionesDetectadas.Count;
            var nuevasNoAncladas = actual.NoAncladas + resultado.MencionesNoAncladas.Count;
            this.acumulado[clave] = (nuevosTotales, nuevasNoAncladas);

            return new EstadisticaMencionesDia(id, dia, nuevosTotales, nuevasNoAncladas);
        }

        public EstadisticaMencionesDia ObtenerDia(string personaje, int diaJuego)
        {
            var id = string.IsNullOrWhiteSpace(personaje) ? "desconocido" : personaje;
            var dia = Math.Max(1, diaJuego);
            return this.acumulado.TryGetValue((id, dia), out var v)
                ? new EstadisticaMencionesDia(id, dia, v.Totales, v.NoAncladas)
                : new EstadisticaMencionesDia(id, dia, 0, 0);
        }
    }

    /// <summary>
    /// Validador determinista de entidades mencionadas en <c>frase</c>, <c>intencion</c> y <c>obra</c> (GSS §8.5, R-15).
    /// Comprueba que nombres propios, referencias, ids de léxico y entidades concretas del mundo aparezcan en la escena,
    /// el apéndice del pulso o la memoria recuperada/persistida del personaje, ignorando entidades genéricas sin nombre.
    /// </summary>
    public static class ValidadorMenciones
    {
        private static readonly Regex PatronRef = new(
            @"\b[a-z][a-z_]*:[^\s,;.""')\]!?]+\b",
            RegexOptions.Compiled);

        private static readonly Regex PatronIdSnake = new(
            @"\b[a-z][a-z0-9]*(?:_[a-z0-9]+)+\b",
            RegexOptions.Compiled);

        private static readonly Regex PatronPalabra = new(
            @"[\p{L}\p{M}]+(?:-[\p{L}\p{M}]+)*",
            RegexOptions.Compiled);

        private static readonly HashSet<string> EntidadesGenericasIgnoradas = new(StringComparer.Ordinal)
        {
            "alguien", "nadie", "algo", "nada", "gente", "persona", "personas",
            "viento", "aire", "brisa", "luz", "sol", "luna", "cielo", "nube", "nubes",
            "sombra", "sombras", "agua", "lluvia", "niebla", "frio", "calor",
            "tiempo", "dia", "dias", "tarde", "tardes", "noche", "noches", "manana", "mananas",
            "silencio", "ruido", "camino", "sendero", "madera", "color", "colores",
            "hoja", "hojas", "calma", "quietud", "energia", "suelo",
        };

        // Fauna, enclaves concretos y objetos singulares del mundo que no deben inventarse si faltan en escena/memoria.
        private static readonly HashSet<string> EntidadesMundoConocidas = new(StringComparer.Ordinal)
        {
            "garza", "garzas", "cuervo", "cuervos", "buho", "buhos", "ciervo", "ciervos",
            "zorro", "zorros", "conejo", "conejos", "ardilla", "ardillas", "trucha", "salmon",
            "gaviota", "gaviotas", "pato", "patos", "junimo", "junimos", "oso", "lobo",
            "muelle", "muelles", "faro", "cascada", "cueva", "mina", "minas",
            "ferrocarril", "estacion", "herreria", "museo", "biblioteca", "carpinteria",
            "rancho", "clinica", "alcantarilla", "torre", "desierto", "oasis", "isla",
            "volcan", "invernadero", "molino", "gallinero", "establo", "cementerio", "fuente",
            "taberna", "playa", "mercado",
            "espada", "escudo", "reliquia", "pergamino", "corona", "amuleto",
            "hacha", "azadon", "guadana",
        };

        private static readonly HashSet<string> PalabrasInicioOracionComunes = new(StringComparer.Ordinal)
        {
            "a", "acaso", "ahora", "ahi", "al", "algo", "alguien", "alli", "amanecer", "antes",
            "apenas", "aqui", "asi", "atardecer", "aun", "aunque", "ayer",
            "bien", "boceto", "buena", "buenas", "bueno", "buenos",
            "cada", "cae", "calma", "casi", "claro", "como", "con", "cuando", "cuadro", "cuidado",
            "de", "debo", "dejo", "del", "desde", "despues", "dia", "dibujar", "dibujo", "disculpa", "donde", "durante",
            "el", "ella", "ellos", "en", "encima", "entonces", "entre", "era", "es", "esa", "esas", "ese", "esos", "esta", "estas", "este", "esto", "estos", "estoy", "estuve", "estudio",
            "fue", "fui",
            "gracias", "guardo", "guarda",
            "ha", "hacia", "han", "hasta", "hay", "hola", "hoy",
            "invierno", "ir",
            "la", "las", "le", "les", "llueve", "lo", "los", "luego", "luz",
            "manana", "mas", "me", "mejor", "mi", "mientras", "mira", "mirar", "mis", "mucho", "muy",
            "nada", "nadie", "ni", "no", "noche", "nos", "nota", "nueva", "nuevo", "nunca",
            "o", "observo", "otra", "otono", "otro", "oye",
            "paisaje", "para", "pausa", "pensamiento", "perdon", "pero", "poco", "por", "porque", "prefiero", "primavera", "puedo", "pues",
            "que", "queda", "quedarme", "quien", "quiero", "quietud",
            "recoger", "recojo", "recuerda", "recuerdo", "regreso", "retomar", "retrato",
            "salgo", "salir", "se", "serena", "si", "siempre", "silencio", "sin", "sobre", "solo", "son", "su", "sus",
            "tal", "tambien", "tampoco", "tan", "tarde", "te", "tema", "tengo", "tiempo", "toda", "todo", "todos", "trabajar", "trabajo", "tu", "tus",
            "un", "una", "unas", "uno", "unos",
            "va", "vale", "vamos", "vas", "ver", "verano", "vi", "viene", "viento", "volvamos", "volver", "voy", "vuelvo",
            "y", "ya", "yo",
        };

        public static ResultadoMenciones Validar(
            Pulso pulso,
            Respuesta respuesta,
            EstadoPersonaje? estado = null,
            PerfilPersonaje? perfil = null,
            Observacion? observacion = null,
            IEnumerable<string>? recuerdosContexto = null)
        {
            if (pulso is null)
            {
                throw new ArgumentNullException(nameof(pulso));
            }

            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            var refsAncladas = new HashSet<string>(StringComparer.Ordinal);
            var tokensAnclados = new HashSet<string>(StringComparer.Ordinal);
            var idsLexicoDesconocidos = new HashSet<string>(StringComparer.Ordinal);

            RecopilarAnclajes(
                pulso,
                estado,
                perfil,
                observacion,
                recuerdosContexto,
                refsAncladas,
                tokensAnclados,
                idsLexicoDesconocidos);

            var textos = RecopilarTextosAComprobar(respuesta);
            var detectadas = new List<string>();
            var noAncladas = new List<string>();
            var motivos = new List<MotivoRechazo>();
            var vistosDetectados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var vistosNoAnclados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (campo, texto) in textos)
            {
                if (string.IsNullOrWhiteSpace(texto))
                {
                    continue;
                }

                // 1. Referencias estructuradas (<tipo>:<id>).
                foreach (Match match in PatronRef.Matches(texto))
                {
                    var valor = match.Value;
                    var norm = Normalizar(valor);
                    RegistrarDeteccion(detectadas, vistosDetectados, valor);

                    var anclado = refsAncladas.Contains(norm) || ContieneSegmentoFinalAnclado(norm, refsAncladas);
                    if (!anclado)
                    {
                        RegistrarNoAnclada(noAncladas, vistosNoAnclados, motivos, valor, campo);
                    }
                }

                // 2. Identificadores del léxico (snake_case) usados en prosa fuera de la escena/memoria.
                foreach (Match match in PatronIdSnake.Matches(texto))
                {
                    var valor = match.Value;
                    var norm = Normalizar(valor);
                    if (!idsLexicoDesconocidos.Contains(norm) && !refsAncladas.Contains(norm))
                    {
                        continue;
                    }

                    RegistrarDeteccion(detectadas, vistosDetectados, valor);
                    if (idsLexicoDesconocidos.Contains(norm) && !tokensAnclados.Contains(norm) && !refsAncladas.Contains(norm))
                    {
                        RegistrarNoAnclada(noAncladas, vistosNoAnclados, motivos, valor, campo);
                    }
                }

                // 3. Nombres propios y entidades concretas del catálogo del mundo.
                foreach (Match match in PatronPalabra.Matches(texto))
                {
                    var palabra = match.Value;
                    var norm = Normalizar(palabra);
                    if (string.IsNullOrEmpty(norm) || EntidadesGenericasIgnoradas.Contains(norm))
                    {
                        continue;
                    }

                    var inicioOracion = EsInicioDeOracion(texto, match.Index);
                    var empiezaMayuscula = char.IsUpper(palabra[0]);

                    var esNombrePropio = empiezaMayuscula
                        && palabra.Length >= 2
                        && !inicioOracion;
                    var esEntidadCatalogo = EntidadesMundoConocidas.Contains(norm)
                        || EntidadesMundoConocidas.Contains(Singularizar(norm));

                    if (!esNombrePropio && !esEntidadCatalogo)
                    {
                        continue;
                    }

                    // Si un token del catálogo también apareció como palabra inicial común no capitalizada no importa, ya entró por catálogo.
                    if (esNombrePropio && PalabrasInicioOracionComunes.Contains(norm))
                    {
                        continue;
                    }

                    RegistrarDeteccion(detectadas, vistosDetectados, palabra);

                    var estaAnclada = tokensAnclados.Contains(norm)
                        || tokensAnclados.Contains(Singularizar(norm))
                        || refsAncladas.Contains(norm);

                    if (!estaAnclada)
                    {
                        RegistrarNoAnclada(noAncladas, vistosNoAnclados, motivos, palabra, campo);
                    }
                }
            }

            return new ResultadoMenciones(
                noAncladas.Count == 0,
                detectadas,
                noAncladas,
                motivos);
        }

        private static List<(string Campo, string Texto)> RecopilarTextosAComprobar(Respuesta respuesta)
        {
            var lista = new List<(string Campo, string Texto)>();
            if (!string.IsNullOrWhiteSpace(respuesta.Frase))
            {
                lista.Add(("frase", respuesta.Frase!));
            }

            if (respuesta.Intencion is { } intencion)
            {
                if (!string.IsNullOrWhiteSpace(intencion.Objetivo))
                {
                    lista.Add(("intencion.objetivo", intencion.Objetivo));
                }

                if (!string.IsNullOrWhiteSpace(intencion.Porque))
                {
                    lista.Add(("intencion.porque", intencion.Porque));
                }

                if (intencion.Plan is { Count: > 0 } plan)
                {
                    for (var i = 0; i < plan.Count; i++)
                    {
                        var paso = plan[i];
                        if (paso is not null
                            && string.Equals(paso.Accion, "murmurar", StringComparison.Ordinal)
                            && !string.IsNullOrWhiteSpace(paso.Texto))
                        {
                            lista.Add(($"intencion.plan[{i}].texto", paso.Texto!));
                        }
                    }
                }
            }

            if (respuesta.Obra?.Decision is { } decision)
            {
                if (!string.IsNullOrWhiteSpace(decision.Titulo))
                {
                    lista.Add(("obra.decision.titulo", decision.Titulo));
                }

                if (!string.IsNullOrWhiteSpace(decision.Tema))
                {
                    lista.Add(("obra.decision.tema", decision.Tema));
                }

                if (!string.IsNullOrWhiteSpace(decision.Composicion))
                {
                    lista.Add(("obra.decision.composicion", decision.Composicion));
                }

                if (!string.IsNullOrWhiteSpace(decision.Nota))
                {
                    lista.Add(("obra.decision.nota", decision.Nota));
                }
            }

            return lista;
        }

        private static void RecopilarAnclajes(
            Pulso pulso,
            EstadoPersonaje? estado,
            PerfilPersonaje? perfil,
            Observacion? observacion,
            IEnumerable<string>? recuerdosContexto,
            HashSet<string> refsAncladas,
            HashSet<string> tokensAnclados,
            HashSet<string> idsLexicoDesconocidos)
        {
            IndexarTexto(pulso.Personaje, refsAncladas, tokensAnclados);
            IndexarTexto(pulso.Escena, refsAncladas, tokensAnclados);

            foreach (var lugar in pulso.Lugares ?? Enumerable.Empty<ParIdProsa>())
            {
                if (lugar is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(lugar.Id))
                {
                    refsAncladas.Add(Normalizar(lugar.Id));
                    tokensAnclados.Add(Normalizar(lugar.Id));
                }

                IndexarTexto(lugar.Prosa, refsAncladas, tokensAnclados);
            }

            if (pulso.Hechos is not null)
            {
                foreach (var kv in pulso.Hechos)
                {
                    IndexarTexto(kv.Key, refsAncladas, tokensAnclados);
                    IndexarJson(kv.Value, refsAncladas, tokensAnclados);
                }
            }

            foreach (var obra in pulso.Obras ?? Enumerable.Empty<ObraCitable>())
            {
                if (obra is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(obra.Id))
                {
                    refsAncladas.Add(Normalizar(obra.Id));
                }

                IndexarTexto(obra.Prosa, refsAncladas, tokensAnclados);
            }

            foreach (var b in pulso.Bufer ?? Enumerable.Empty<EntradaBufer>())
            {
                if (b is not null)
                {
                    IndexarTexto(b.Prosa, refsAncladas, tokensAnclados);
                }
            }

            if (pulso.Huella is not null)
            {
                IndexarTexto(pulso.Huella.Prosa, refsAncladas, tokensAnclados);
                foreach (var ent in pulso.Huella.Entidades ?? Enumerable.Empty<AtencionEntidad>())
                {
                    IndexarIdentidad(ent?.Ref, refsAncladas, tokensAnclados);
                }
            }

            if (pulso.IntencionActiva is not null)
            {
                IndexarIdentidad(pulso.IntencionActiva.Id, refsAncladas, tokensAnclados);
                IndexarTexto(pulso.IntencionActiva.Objetivo, refsAncladas, tokensAnclados);
            }

            foreach (var ag in pulso.Agenda ?? Enumerable.Empty<EntradaAgenda>())
            {
                if (ag is null)
                {
                    continue;
                }

                IndexarIdentidad(ag.Hito, refsAncladas, tokensAnclados);
                IndexarTexto(ag.Prosa, refsAncladas, tokensAnclados);
            }

            foreach (var comp in pulso.CompromisosProximos ?? Enumerable.Empty<CompromisoProximo>())
            {
                if (comp is null)
                {
                    continue;
                }

                IndexarIdentidad(comp.Id, refsAncladas, tokensAnclados);
                IndexarIdentidad(comp.Con, refsAncladas, tokensAnclados);
                IndexarTexto(comp.Prosa, refsAncladas, tokensAnclados);
            }

            if (pulso.EpisodiosDiario is not null)
            {
                foreach (var (epId, prosa) in pulso.EpisodiosDiario)
                {
                    IndexarIdentidad(epId, refsAncladas, tokensAnclados);
                    IndexarTexto(prosa, refsAncladas, tokensAnclados);
                }
            }

            foreach (var linea in pulso.DiarioDia ?? Enumerable.Empty<string>())
            {
                IndexarTexto(linea, refsAncladas, tokensAnclados);
            }

            foreach (var rec in pulso.Recuerdos ?? Enumerable.Empty<string>())
            {
                IndexarTexto(rec, refsAncladas, tokensAnclados);
            }

            if (pulso.Humor is not null)
            {
                IndexarTexto(pulso.Humor.Resumen, refsAncladas, tokensAnclados);
            }

            if (pulso.Interlocutor is not null)
            {
                IndexarIdentidad(pulso.Interlocutor.Ref, refsAncladas, tokensAnclados);
                IndexarTexto(pulso.Interlocutor.Texto, refsAncladas, tokensAnclados);
            }

            if (observacion is not null)
            {
                IndexarTexto(observacion.Ubicacion, refsAncladas, tokensAnclados);
                foreach (var ent in observacion.Entidades)
                {
                    IndexarIdentidad(ent.Ref, refsAncladas, tokensAnclados);
                    IndexarTexto(ent.Nombre, refsAncladas, tokensAnclados);
                    foreach (var eti in ent.Etiquetas)
                    {
                        IndexarTexto(eti, refsAncladas, tokensAnclados);
                    }
                }

                if (observacion.Sucesos is not null)
                {
                    foreach (var suc in observacion.Sucesos)
                    {
                        IndexarIdentidad(suc.Ref, refsAncladas, tokensAnclados);
                        IndexarIdentidad(suc.ActorRef, refsAncladas, tokensAnclados);
                        IndexarIdentidad(suc.ObjetoId, refsAncladas, tokensAnclados);
                        IndexarTexto(suc.Descripcion, refsAncladas, tokensAnclados);
                    }
                }
            }

            if (estado is not null)
            {
                foreach (var ep in estado.Diario)
                {
                    IndexarIdentidad(ep.Id, refsAncladas, tokensAnclados);
                    IndexarTexto(ep.ResumenProsa, refsAncladas, tokensAnclados);
                    foreach (var ent in ep.Entidades)
                    {
                        IndexarIdentidad(ent, refsAncladas, tokensAnclados);
                    }
                }

                foreach (var rec in estado.Recuerdos)
                {
                    IndexarTexto(rec.Texto, refsAncladas, tokensAnclados);
                }

                foreach (var (clave, rel) in estado.Relaciones)
                {
                    IndexarIdentidad(clave, refsAncladas, tokensAnclados);
                    IndexarTexto(rel?.Impresion, refsAncladas, tokensAnclados);
                }

                foreach (var comp in estado.Compromisos)
                {
                    IndexarIdentidad(comp.Id, refsAncladas, tokensAnclados);
                    IndexarIdentidad(comp.Destinatario, refsAncladas, tokensAnclados);
                    IndexarTexto(comp.Objeto, refsAncladas, tokensAnclados);
                }

                foreach (var obra in estado.Portafolio)
                {
                    IndexarIdentidad(obra.ObraId, refsAncladas, tokensAnclados);
                    IndexarIdentidad(obra.Destinatario, refsAncladas, tokensAnclados);
                    if (obra.Decision is not null)
                    {
                        IndexarTexto(obra.Decision.Titulo, refsAncladas, tokensAnclados);
                        IndexarTexto(obra.Decision.Tema, refsAncladas, tokensAnclados);
                        IndexarTexto(obra.Decision.Composicion, refsAncladas, tokensAnclados);
                        IndexarTexto(obra.Decision.Paleta, refsAncladas, tokensAnclados);
                        IndexarTexto(obra.Decision.Nota, refsAncladas, tokensAnclados);
                    }
                }

                if (estado.Humor is not null)
                {
                    IndexarTexto(estado.Humor.Resumen, refsAncladas, tokensAnclados);
                }
            }

            if (recuerdosContexto is not null)
            {
                foreach (var texto in recuerdosContexto)
                {
                    IndexarTexto(texto, refsAncladas, tokensAnclados);
                }
            }

            if (perfil is not null)
            {
                IndexarIdentidad(perfil.Id, refsAncladas, tokensAnclados);
                IndexarTexto(perfil.Npc, refsAncladas, tokensAnclados);

                var lugaresEnPulso = new HashSet<string>(
                    (pulso.Lugares ?? Enumerable.Empty<ParIdProsa>())
                        .Where(l => !string.IsNullOrWhiteSpace(l?.Id))
                        .Select(l => Normalizar(l.Id)),
                    StringComparer.Ordinal);

                foreach (var lugar in perfil.Lexico.Lugares)
                {
                    if (!string.IsNullOrWhiteSpace(lugar.Id))
                    {
                        var normId = Normalizar(lugar.Id);
                        if (!lugaresEnPulso.Contains(normId) && !tokensAnclados.Contains(normId))
                        {
                            idsLexicoDesconocidos.Add(normId);
                        }
                    }
                }
            }
        }

        private static void IndexarJson(JsonElement elemento, HashSet<string> refsAncladas, HashSet<string> tokensAnclados)
        {
            switch (elemento.ValueKind)
            {
                case JsonValueKind.String:
                    IndexarTexto(elemento.GetString(), refsAncladas, tokensAnclados);
                    break;
                case JsonValueKind.Array:
                    foreach (var item in elemento.EnumerateArray())
                    {
                        IndexarJson(item, refsAncladas, tokensAnclados);
                    }

                    break;
                case JsonValueKind.Object:
                    foreach (var prop in elemento.EnumerateObject())
                    {
                        IndexarTexto(prop.Name, refsAncladas, tokensAnclados);
                        IndexarJson(prop.Value, refsAncladas, tokensAnclados);
                    }

                    break;
            }
        }

        private static void IndexarIdentidad(string? valor, HashSet<string> refsAncladas, HashSet<string> tokensAnclados)
        {
            if (string.IsNullOrWhiteSpace(valor))
            {
                return;
            }

            var norm = Normalizar(valor);
            refsAncladas.Add(norm);
            tokensAnclados.Add(norm);
            foreach (var segmento in norm.Split(':', '/', ',', '_', '-', ' '))
            {
                if (segmento.Length >= 2)
                {
                    tokensAnclados.Add(segmento);
                    tokensAnclados.Add(Singularizar(segmento));
                }
            }
        }

        private static void IndexarTexto(string? texto, HashSet<string> refsAncladas, HashSet<string> tokensAnclados)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return;
            }

            foreach (Match m in PatronRef.Matches(texto))
            {
                IndexarIdentidad(m.Value, refsAncladas, tokensAnclados);
            }

            foreach (Match m in PatronIdSnake.Matches(texto))
            {
                var norm = Normalizar(m.Value);
                refsAncladas.Add(norm);
                tokensAnclados.Add(norm);
            }

            foreach (Match m in PatronPalabra.Matches(texto))
            {
                var norm = Normalizar(m.Value);
                if (norm.Length >= 2)
                {
                    tokensAnclados.Add(norm);
                    tokensAnclados.Add(Singularizar(norm));
                }
            }
        }

        private static bool ContieneSegmentoFinalAnclado(string refNorm, HashSet<string> refsAncladas)
        {
            var idx = refNorm.LastIndexOf(':');
            if (idx < 0 || idx + 1 >= refNorm.Length)
            {
                return false;
            }

            var sufijo = refNorm[(idx + 1)..];
            return sufijo.Length >= 2 && refsAncladas.Contains(sufijo);
        }

        private static bool EsInicioDeOracion(string texto, int indice)
        {
            for (var i = indice - 1; i >= 0; i--)
            {
                var c = texto[i];
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                return c is '.' or '!' or '?' or '¡' or '¿' or '\n' or '\r' or ':' or '«' or '"' or '—';
            }

            return true;
        }

        private static void RegistrarDeteccion(List<string> lista, HashSet<string> vistos, string valor)
        {
            if (vistos.Add(valor))
            {
                lista.Add(valor);
            }
        }

        private static void RegistrarNoAnclada(
            List<string> noAncladas,
            HashSet<string> vistosNoAncladas,
            List<MotivoRechazo> motivos,
            string valor,
            string campo)
        {
            if (vistosNoAncladas.Add(valor))
            {
                noAncladas.Add(valor);
            }

            motivos.Add(new MotivoRechazo(
                "mencion_no_anclada",
                campo,
                $"«{valor}» no aparece en lo que ves ni en lo que recuerdas"));
        }

        private static string Singularizar(string palabraNorm)
        {
            if (palabraNorm.Length > 4 && palabraNorm.EndsWith("es", StringComparison.Ordinal))
            {
                return palabraNorm[..^2];
            }

            if (palabraNorm.Length > 3 && palabraNorm.EndsWith('s'))
            {
                return palabraNorm[..^1];
            }

            return palabraNorm;
        }

        private static string Normalizar(string texto)
        {
            var descompuesto = texto.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(descompuesto.Length);
            foreach (var ch in descompuesto)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
