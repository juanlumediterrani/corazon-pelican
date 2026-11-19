using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Sensorio.Prosa
{
    /// <summary>Contexto adicional opcional que acompaña a la observación al construir la escena.</summary>
    public sealed class ContextoEscena
    {
        public EstadoAtencion? Estado { get; init; }

        public IReadOnlyList<string>? AccionesDisponibles { get; init; }

        public string? ObraEnCurso { get; init; }

        public string? IntencionActiva { get; init; }

        public IReadOnlyDictionary<string, double>? PesosIntencion { get; init; }

        public IReadOnlyList<ObraCitable>? Obras { get; init; }
    }

    /// <summary>Persona observada cerca del personaje, en formato del apéndice (<c>hechos.cerca</c>).</summary>
    public sealed record HechoPersona(string Ref, string Nombre, int? Corazones, int Distancia);

    /// <summary>Apéndice seco de hechos, referencias y acciones disponibles (GSS §3.4, ADS §3.3).</summary>
    public sealed class ApendiceEscena
    {
        public int Dia { get; init; }
        public int Hora { get; init; }
        public string Estacion { get; init; } = "";
        public int DiaEstacion { get; init; }
        public string Clima { get; init; } = "";
        public string NivelLuz { get; init; } = "";
        public string Energia { get; init; } = "media";
        public Lugar? LugarActual { get; init; }
        public string? ObraEnCurso { get; init; }
        public string? IntencionActiva { get; init; }
        public IReadOnlyList<HechoPersona> Cerca { get; init; } = Array.Empty<HechoPersona>();
        public IReadOnlyList<ParIdProsa> Entidades { get; init; } = Array.Empty<ParIdProsa>();
        public IReadOnlyList<ParIdProsa> Lugares { get; init; } = Array.Empty<ParIdProsa>();
        public IReadOnlyList<string> AccionesDisponibles { get; init; } = Array.Empty<string>();
        public IReadOnlyList<ObraCitable> Obras { get; init; } = Array.Empty<ObraCitable>();
    }

    /// <summary>Salida del generador: escena en 2ª persona y presente junto con su apéndice seco.</summary>
    public sealed class EscenaGenerada
    {
        public EscenaGenerada(string escena, ApendiceEscena apendice, string textoCompleto)
        {
            this.Escena = escena;
            this.Apendice = apendice;
            this.TextoCompleto = textoCompleto;
        }

        public string Escena { get; }

        public ApendiceEscena Apendice { get; }

        public string TextoCompleto { get; }

        /// <summary>Proyecta los hechos del apéndice al formato de <c>Pulso.Hechos</c> (<c>schemas/pulso.schema.json</c>).</summary>
        public Dictionary<string, JsonElement> ConstruirHechosPulso()
        {
            var hechos = new Dictionary<string, JsonElement>
            {
                ["energia"] = JsonSerializer.SerializeToElement(this.Apendice.Energia),
                ["cerca"] = JsonSerializer.SerializeToElement(this.Apendice.Cerca.Select(c =>
                {
                    var obj = new Dictionary<string, object?>
                    {
                        ["ref"] = c.Ref,
                        ["nombre"] = c.Nombre,
                        ["distancia"] = c.Distancia,
                    };
                    if (c.Corazones is int corazones)
                    {
                        obj["corazones"] = corazones;
                    }

                    return obj;
                }).ToList()),
                ["entidades"] = JsonSerializer.SerializeToElement(this.Apendice.Entidades),
            };
            return hechos;
        }

        /// <summary>
        /// Construye un pulso base con escena, hechos, lugares y acciones para <c>GET escena/{personaje}</c>
        /// o para completar al emitir una pulsión (CP-203).
        /// </summary>
        public Pulso CrearPulsoBase(long id, string epoca, string personaje, string motivo) => new()
        {
            Id = id,
            Epoca = epoca,
            Personaje = personaje,
            Motivo = motivo,
            TiempoJuego = new TiempoJuego { Dia = this.Apendice.Dia, Hora = this.Apendice.Hora },
            Escena = this.Escena,
            Hechos = this.ConstruirHechosPulso(),
            AccionesDisponibles = this.Apendice.AccionesDisponibles.ToList(),
            Lugares = this.Apendice.Lugares.ToList(),
            Obras = this.Apendice.Obras.Count > 0 ? this.Apendice.Obras.ToList() : null,
        };
    }

    /// <summary>
    /// Convierte una observación, sus bandas de saliencia y el léxico ofrecido en prosa determinista
    /// de segunda persona y presente más su apéndice seco (R-01, R-02, R-03; GSS §3.4).
    /// Punto de entrada compartido por <c>cp_escena</c>, <c>GET escena/{personaje}</c> y la emisión de pulsos.
    /// </summary>
    public static class GeneradorEscena
    {
        public static EscenaGenerada Generar(
            Observacion observacion,
            PerfilPersonaje perfil,
            PlantillasProsa plantillas,
            Novedad? novedad = null,
            ClasificadorBandas? clasificador = null,
            ContextoEscena? contexto = null)
        {
            var estado = contexto?.Estado ?? EstadoAtencion.Abierta;
            var contextoSaliencia = new ContextoSaliencia(perfil.Rasgos, estado, perfil.Umbrales.Periferia, contexto?.PesosIntencion);
            var saliencias = Saliencia.Calcular(observacion, contextoSaliencia, novedad ?? new Novedad());
            var bandas = (clasificador ?? new ClasificadorBandas(perfil.Umbrales)).Clasificar(saliencias);
            var lugaresOfrecidos = perfil.Lexico.OfrecerLugares(_ => true);
            var contextoFinal = new ContextoEscena
            {
                Estado = estado,
                AccionesDisponibles = contexto?.AccionesDisponibles ?? perfil.Acciones,
                ObraEnCurso = contexto?.ObraEnCurso,
                IntencionActiva = contexto?.IntencionActiva,
                Obras = contexto?.Obras,
            };

            return Generar(observacion, bandas, lugaresOfrecidos, plantillas, contextoFinal);
        }

        public static EscenaGenerada Generar(
            Observacion observacion,
            IReadOnlyList<EntidadEnBanda> bandas,
            LugaresOfrecidos lugaresOfrecidos,
            PlantillasProsa plantillas,
            ContextoEscena? contexto = null)
        {
            var (claveEstacion, diaEstacion) = PlantillasProsa.EstacionDe(observacion.Dia);
            var nombreEstacion = plantillas.NombreEstacion(claveEstacion);
            var franja = PlantillasProsa.ClaveFranja(observacion.Hora);
            var nivelLuz = PlantillasProsa.ClaveLuz(observacion.Luz);
            var estado = contexto?.Estado ?? EstadoAtencion.Abierta;
            var energia = ClasificarEnergia(estado.Energia);

            var oraciones = new List<string>();

            var variantesFranja = plantillas.FranjasHorarias[franja];
            oraciones.Add(Aplicar(
                PlantillasProsa.ElegirVariante(variantesFranja, observacion.Dia, $"franja:{franja}"),
                ("{estacion}", nombreEstacion),
                ("{dia_estacion}", diaEstacion.ToString())));

            var variantesClima = plantillas.VariantesClima(observacion.Clima);
            if (variantesClima.Count > 0)
            {
                oraciones.Add(Aplicar(
                    PlantillasProsa.ElegirVariante(variantesClima, observacion.Dia, $"clima:{observacion.Clima}"),
                    ("{estacion}", nombreEstacion)));
            }

            if (plantillas.Luz.TryGetValue(nivelLuz, out var variantesLuz) && variantesLuz.Count > 0)
            {
                oraciones.Add(PlantillasProsa.ElegirVariante(variantesLuz, observacion.Dia, $"luz:{nivelLuz}"));
            }

            var lugaresApendice = lugaresOfrecidos.CrearApendice();
            var lugarActual = ResolverLugarActual(observacion, lugaresOfrecidos, lugaresApendice);
            if (lugarActual is not null && plantillas.Lugar.Actual.Count > 0)
            {
                oraciones.Add(Aplicar(
                    PlantillasProsa.ElegirVariante(plantillas.Lugar.Actual, observacion.Dia, $"lugar:{lugarActual.Id}"),
                    ("{lugar}", lugarActual.Prosa)));
            }
            else if (lugarActual is null && plantillas.Lugar.SinLugar.Count > 0)
            {
                oraciones.Add(PlantillasProsa.ElegirVariante(plantillas.Lugar.SinLugar, observacion.Dia, "lugar:sin_lugar"));
            }

            if (plantillas.Energia.TryGetValue(energia, out var variantesEnergia) && variantesEnergia.Count > 0)
            {
                oraciones.Add(PlantillasProsa.ElegirVariante(variantesEnergia, observacion.Dia, $"energia:{energia}"));
            }

            if (!string.IsNullOrWhiteSpace(contexto?.ObraEnCurso) && plantillas.ObraEnCurso.Count > 0)
            {
                oraciones.Add(Aplicar(
                    PlantillasProsa.ElegirVariante(plantillas.ObraEnCurso, observacion.Dia, $"obra:{contexto.ObraEnCurso}"),
                    ("{obra}", contexto.ObraEnCurso)));
            }

            if (!string.IsNullOrWhiteSpace(contexto?.IntencionActiva) && plantillas.IntencionActiva.Count > 0)
            {
                oraciones.Add(Aplicar(
                    PlantillasProsa.ElegirVariante(plantillas.IntencionActiva, observacion.Dia, $"intencion:{contexto.IntencionActiva}"),
                    ("{intencion}", contexto.IntencionActiva)));
            }

            var observadasPorRef = observacion.Entidades.ToDictionary(e => e.Ref, StringComparer.Ordinal);
            var visiblesOrdenadas = bandas
                .Where(b => b.Banda != Banda.Ausente && observadasPorRef.ContainsKey(b.Ref))
                .OrderByDescending(b => b.Banda)
                .ThenByDescending(b => b.Saliencia)
                .ThenBy(b => b.Ref, StringComparer.Ordinal)
                .ToList();

            var entidadesApendice = new List<ParIdProsa>();
            var cercaApendice = new List<HechoPersona>();

            foreach (var enBanda in visiblesOrdenadas)
            {
                var entidad = observadasPorRef[enBanda.Ref];
                var categoriaClave = PlantillasProsa.ClaveCategoria(entidad.Categoria);
                var grupo = plantillas.Entidades[categoriaClave];
                var distancia = DistanciaCasillas(observacion, entidad);
                var rumbo = Rumbo(observacion, entidad);
                var cercania = Cercania(distancia);

                string prosaUsada;
                string plantilla;
                if (enBanda.Banda == Banda.Periferia)
                {
                    prosaUsada = grupo.Periferia.Referencia;
                    plantilla = PlantillasProsa.ElegirVariante(
                        grupo.Periferia.Variantes,
                        observacion.Dia,
                        $"{entidad.Ref}:periferia");
                    oraciones.Add(Aplicar(
                        plantilla,
                        ("{referencia}", prosaUsada),
                        ("{rumbo}", rumbo)));
                }
                else
                {
                    prosaUsada = entidad.Nombre;
                    var variantes = enBanda.Banda == Banda.Foco ? grupo.Foco : grupo.Mencion;
                    var claveBanda = enBanda.Banda == Banda.Foco ? "foco" : "mencion";
                    plantilla = PlantillasProsa.ElegirVariante(variantes, observacion.Dia, $"{entidad.Ref}:{claveBanda}");
                    oraciones.Add(Aplicar(
                        plantilla,
                        ("{nombre}", prosaUsada),
                        ("{rumbo}", rumbo),
                        ("{cercania}", cercania),
                        ("{relacion}", NotaRelacion(entidad.Corazones))));

                    if (entidad.Categoria is CategoriaEntidad.Granjero or CategoriaEntidad.Npc)
                    {
                        cercaApendice.Add(new HechoPersona(entidad.Ref, entidad.Nombre, entidad.Corazones, distancia));
                    }
                }

                entidadesApendice.Add(new ParIdProsa { Id = entidad.Ref, Prosa = prosaUsada });
            }

            var escena = string.Join(" ", oraciones.Where(o => !string.IsNullOrWhiteSpace(o)));
            var acciones = (contexto?.AccionesDisponibles ?? Array.Empty<string>()).ToList();
            var obras = (contexto?.Obras ?? Array.Empty<ObraCitable>()).ToList();

            var apendice = new ApendiceEscena
            {
                Dia = observacion.Dia,
                Hora = observacion.Hora,
                Estacion = nombreEstacion,
                DiaEstacion = diaEstacion,
                Clima = observacion.Clima,
                NivelLuz = nivelLuz,
                Energia = energia,
                LugarActual = lugarActual,
                ObraEnCurso = contexto?.ObraEnCurso,
                IntencionActiva = contexto?.IntencionActiva,
                Cerca = cercaApendice,
                Entidades = entidadesApendice,
                Lugares = lugaresApendice,
                AccionesDisponibles = acciones,
                Obras = obras,
            };

            var textoCompleto = FormatearTextoCompleto(escena, apendice);
            return new EscenaGenerada(escena, apendice, textoCompleto);
        }

        private static Lugar? ResolverLugarActual(
            Observacion observacion,
            LugaresOfrecidos lugaresOfrecidos,
            IReadOnlyList<ParIdProsa> lugaresApendice)
        {
            var candidatos = lugaresApendice
                .Select(p => lugaresOfrecidos.ResolverLugar(p.Id))
                .OfType<Lugar>()
                .Where(l => string.Equals(l.Ubicacion, observacion.Ubicacion, StringComparison.Ordinal))
                .ToList();

            var exacto = candidatos.FirstOrDefault(l =>
                l.Area.Count == 4 && l.Area[2] > 0 && l.Area[3] > 0
                && observacion.X >= l.Area[0] && observacion.X < l.Area[0] + l.Area[2]
                && observacion.Y >= l.Area[1] && observacion.Y < l.Area[1] + l.Area[3]);

            if (exacto is not null)
            {
                return exacto;
            }

            return candidatos.Count == 1 && PerfilValidator.AreaPendiente(candidatos[0]) ? candidatos[0] : null;
        }

        private static string FormatearTextoCompleto(string escena, ApendiceEscena apendice)
        {
            var sb = new StringBuilder();
            sb.Append(escena);
            sb.Append("\n\n— Hechos —\n");
            sb.Append($"hora {FormatearHora(apendice.Hora)} · {apendice.Estacion} {apendice.DiaEstacion} · {apendice.Clima} · luz {apendice.NivelLuz} · energía {apendice.Energia}");

            if (!string.IsNullOrWhiteSpace(apendice.ObraEnCurso))
            {
                sb.Append($"\nobra_en_curso: {apendice.ObraEnCurso}");
            }

            if (!string.IsNullOrWhiteSpace(apendice.IntencionActiva))
            {
                sb.Append($"\nintencion: {apendice.IntencionActiva}");
            }

            if (apendice.Cerca.Count > 0)
            {
                var personas = apendice.Cerca.Select(c =>
                    c.Corazones is int corazones
                        ? $"{c.Ref} — {c.Nombre} ({corazones} corazones, a {c.Distancia} casillas)"
                        : $"{c.Ref} — {c.Nombre} (a {c.Distancia} casillas)");
                sb.Append($"\ncerca: {string.Join("; ", personas)}");
            }
            else
            {
                sb.Append("\ncerca: nadie");
            }

            if (apendice.Entidades.Count > 0)
            {
                sb.Append("\nentidades:");
                foreach (var entidad in apendice.Entidades)
                {
                    sb.Append($"\n- {entidad.Id} — {entidad.Prosa}");
                }
            }
            else
            {
                sb.Append("\nentidades: ninguna");
            }

            if (apendice.Lugares.Count > 0)
            {
                sb.Append("\nlugares:");
                foreach (var lugar in apendice.Lugares)
                {
                    sb.Append($"\n- {lugar.Id} — {lugar.Prosa}");
                }
            }
            else
            {
                sb.Append("\nlugares: ninguno");
            }

            sb.Append(apendice.AccionesDisponibles.Count > 0
                ? $"\nacciones: {string.Join(" · ", apendice.AccionesDisponibles)}"
                : "\nacciones: ninguna");

            return sb.ToString();
        }

        private static string FormatearHora(int hora)
        {
            var horas = (hora / 100) % 24;
            var minutos = Math.Abs(hora % 100);
            return $"{horas:00}:{minutos:00}";
        }

        private static string ClasificarEnergia(double energia)
        {
            if (energia >= 0.7)
            {
                return "alta";
            }

            return energia >= 0.35 ? "media" : "baja";
        }

        private static int DistanciaCasillas(Observacion obs, EntidadObservada entidad)
            => (int)Math.Round(Math.Sqrt(Math.Pow(entidad.X - obs.X, 2) + Math.Pow(entidad.Y - obs.Y, 2)));

        private static string Rumbo(Observacion obs, EntidadObservada entidad)
        {
            var dx = entidad.X - obs.X;
            var dy = entidad.Y - obs.Y;
            if (Math.Abs(dx) <= 1 && Math.Abs(dy) <= 1)
            {
                return "junto a ti";
            }

            var vertical = dy < -1 ? "norte" : dy > 1 ? "sur" : "";
            var horizontal = dx > 1 ? "este" : dx < -1 ? "oeste" : "";
            return (vertical, horizontal) switch
            {
                ("norte", "") => "al norte",
                ("sur", "") => "al sur",
                ("", "este") => "al este",
                ("", "oeste") => "al oeste",
                ("norte", "este") => "al nordeste",
                ("norte", "oeste") => "al noroeste",
                ("sur", "este") => "al sureste",
                ("sur", "oeste") => "al suroeste",
                _ => "cerca",
            };
        }

        private static string Cercania(int distancia) => distancia switch
        {
            <= 1 => "al alcance de la mano",
            <= 5 => "muy cerca",
            <= 12 => "a pocos pasos",
            _ => "más lejos",
        };

        private static string NotaRelacion(int? corazones) => corazones switch
        {
            >= 6 => "alguien de confianza",
            >= 3 => "una cara conocida",
            _ => "alguien con quien apenas has tratado",
        };

        private static string Aplicar(string plantilla, params (string Clave, string Valor)[] reemplazos)
        {
            var resultado = plantilla;
            foreach (var (clave, valor) in reemplazos)
            {
                resultado = resultado.Replace(clave, valor, StringComparison.Ordinal);
            }

            return resultado;
        }
    }
}
