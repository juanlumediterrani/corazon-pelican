using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using CorazonPelican.Core.Api;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Resultado de una pasada de envejecimiento y poda del diario.</summary>
    public sealed record ResultadoConsolidacion(int EpisodiosEliminados, int EpisodiosRestantes, int TamanoBytes);

    /// <summary>Resultado de aplicar <see cref="Respuesta.Memoria"/> sobre <see cref="EstadoPersonaje"/>.</summary>
    public sealed record ResultadoAplicacionMemoria(
        bool Aceptada,
        MotivoRechazo? MotivoRechazo,
        IReadOnlyList<Recuerdo> RecuerdosCreados);

    /// <summary>
    /// Envejecimiento temporal y poda del diario con tope <c>TamanoMaxMemoriaKB</c> (GSS §9.2, ADS §9, §12.1),
    /// proyección hacia <see cref="Pulso.EpisodiosDiario"/> e <see cref="InstantaneaMemoria"/>,
    /// y aplicación de reflexiones validadas (CP-206, CP-211).
    /// </summary>
    public static class Consolidacion
    {
        public const double SemividaEnvejecimientoPorDefectoDias = 14.0;

        /// <summary>
        /// Saliencia efectiva de un episodio tras envejecer desde <see cref="EpisodioDiario.DiaJuego"/>
        /// hasta <paramref name="diaActual"/>. Función pura e idempotente.
        /// </summary>
        public static double SalienciaEnvejecida(
            EpisodioDiario episodio,
            int diaActual,
            double semividaDias = SemividaEnvejecimientoPorDefectoDias)
        {
            if (semividaDias <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaDias), "La semivida de envejecimiento debe ser mayor que 0.");
            }

            var diasTranscurridos = Math.Max(0, diaActual - episodio.DiaJuego);
            return Math.Max(0, episodio.Saliencia) * Math.Pow(0.5, diasTranscurridos / semividaDias);
        }

        /// <summary>Tamaño actual del estado serializado en UTF-8.</summary>
        public static int MedirTamanoBytes(EstadoPersonaje estado)
            => JsonSerializer.SerializeToUtf8Bytes(estado).Length;

        /// <summary>
        /// Envejece y poda el diario del personaje para no superar <paramref name="tamanoMaxMemoriaKB"/> KB.
        /// Conserva prioritariamente los episodios del día actual y los de mayor saliencia envejecida.
        /// </summary>
        public static ResultadoConsolidacion Consolidar(
            EstadoPersonaje estado,
            int diaActual,
            int tamanoMaxMemoriaKB,
            double semividaDias = SemividaEnvejecimientoPorDefectoDias,
            double? salienciaMinimaEnvejecida = null)
        {
            if (tamanoMaxMemoriaKB <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tamanoMaxMemoriaKB), "El tope de memoria debe ser mayor que 0 KB.");
            }

            if (semividaDias <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaDias), "La semivida debe ser mayor que 0.");
            }

            var iniciales = estado.Diario.Count;
            if (salienciaMinimaEnvejecida is double umbralMinimo)
            {
                estado.Diario.RemoveAll(e =>
                    e.DiaJuego < diaActual && SalienciaEnvejecida(e, diaActual, semividaDias) < umbralMinimo);
            }

            var maxBytes = tamanoMaxMemoriaKB * 1024;
            var tamanoBytes = MedirTamanoBytes(estado);

            if (tamanoBytes > maxBytes && estado.Diario.Count > 0)
            {
                var ordenPoda = estado.Diario
                    .Select((ep, idx) => new
                    {
                        Indice = idx,
                        EsDeHoy = ep.DiaJuego >= diaActual ? 1 : 0,
                        Valor = SalienciaEnvejecida(ep, diaActual, semividaDias),
                        ep.DiaJuego,
                        ep.Hora,
                    })
                    .OrderBy(x => x.EsDeHoy)
                    .ThenBy(x => x.Valor)
                    .ThenBy(x => x.DiaJuego)
                    .ThenBy(x => x.Hora)
                    .ThenBy(x => x.Indice)
                    .Select(x => x.Indice)
                    .ToList();

                var bajo = 1;
                var alto = ordenPoda.Count;
                var mejorK = ordenPoda.Count;

                while (bajo <= alto)
                {
                    var medio = bajo + ((alto - bajo) / 2);
                    var candidato = FiltrarSinIndices(estado, ordenPoda, medio);
                    if (MedirTamanoBytes(candidato) <= maxBytes)
                    {
                        mejorK = medio;
                        alto = medio - 1;
                    }
                    else
                    {
                        bajo = medio + 1;
                    }
                }

                var podados = new HashSet<int>(ordenPoda.Take(mejorK));
                estado.Diario = estado.Diario.Where((_, idx) => !podados.Contains(idx)).ToList();
                tamanoBytes = MedirTamanoBytes(estado);
            }

            return new ResultadoConsolidacion(
                iniciales - estado.Diario.Count,
                estado.Diario.Count,
                tamanoBytes);
        }

        /// <summary>Vuelca las entradas actuales del búfer al diario asignando ids estables por personaje.</summary>
        public static IReadOnlyList<EpisodioDiario> RegistrarDesdeBufer(EstadoPersonaje estado, Bufer bufer)
        {
            var creados = new List<EpisodioDiario>();
            foreach (var entrada in bufer.Entradas)
            {
                if (string.IsNullOrWhiteSpace(entrada.Prosa))
                {
                    continue;
                }

                creados.Add(estado.AgregarEpisodio(
                    entrada.Dia,
                    entrada.Hora,
                    entrada.Prosa,
                    entrada.Saliencia,
                    entrada.Entidades));
            }

            return creados;
        }

        /// <summary>
        /// Proyecta los episodios del día <paramref name="diaJuego"/> al mapa <c>id → prosa</c>
        /// requerido por <see cref="Pulso.EpisodiosDiario"/> (<c>schemas/pulso.schema.json</c>).
        /// </summary>
        public static Dictionary<string, string> ProyectarEpisodiosDelDia(EstadoPersonaje estado, int diaJuego)
        {
            var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var episodio in estado.Diario
                .Where(e => e.DiaJuego == diaJuego
                    && !string.IsNullOrWhiteSpace(e.Id)
                    && !string.IsNullOrWhiteSpace(e.ResumenProsa))
                .OrderBy(e => e.Hora))
            {
                mapa[episodio.Id] = episodio.ResumenProsa;
            }

            return mapa;
        }

        /// <summary>
        /// Rellena <see cref="Pulso.EpisodiosDiario"/> con los episodios del día y despeja
        /// <see cref="Pulso.DiarioDia"/> para respetar la exclusión mutua de <c>schemas/pulso.schema.json</c>.
        /// </summary>
        public static void AplicarEpisodiosAlPulso(Pulso pulso, EstadoPersonaje estado, int diaJuego)
        {
            var mapa = ProyectarEpisodiosDelDia(estado, diaJuego);
            pulso.EpisodiosDiario = mapa.Count > 0 ? mapa : null;
            pulso.DiarioDia = null;
        }

        /// <summary>
        /// Valida <see cref="Respuesta.Memoria"/> frente a <paramref name="pulso"/> mediante
        /// <see cref="ValidadorOrigenDiario.Comprobar"/> y guarda los nuevos <see cref="Recuerdo"/>s en
        /// <paramref name="estado"/> conservando <c>origen_diario</c> (o en prosa si el pulso no ofrecía <c>episodios_diario</c>).
        /// </summary>
        public static ResultadoAplicacionMemoria AplicarMemoria(
            EstadoPersonaje estado,
            Pulso pulso,
            Respuesta respuesta,
            double? importanciaReflexion = null,
            double importanciaProsa = 0.5)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (pulso is null)
            {
                throw new ArgumentNullException(nameof(pulso));
            }

            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            var rechazo = ValidadorOrigenDiario.Comprobar(pulso, respuesta);
            if (rechazo is not null)
            {
                return new ResultadoAplicacionMemoria(false, rechazo, Array.Empty<Recuerdo>());
            }

            var memoria = respuesta.Memoria;
            if (memoria is null)
            {
                return new ResultadoAplicacionMemoria(true, null, Array.Empty<Recuerdo>());
            }

            if (memoria.Reflexiones is { } reflexiones)
            {
                for (var i = 0; i < reflexiones.Count; i++)
                {
                    var origen = reflexiones[i].OrigenDiario;
                    if (origen is null || origen.Count == 0)
                    {
                        return new ResultadoAplicacionMemoria(
                            false,
                            new MotivoRechazo(
                                "origen_ausente",
                                $"memoria.reflexiones[{i}].origen_diario",
                                "recuerda qué episodio te dejó esa impresión"),
                            Array.Empty<Recuerdo>());
                    }

                    if (string.IsNullOrWhiteSpace(reflexiones[i].Texto))
                    {
                        return new ResultadoAplicacionMemoria(
                            false,
                            new MotivoRechazo(
                                "texto_vacio",
                                $"memoria.reflexiones[{i}].texto",
                                "la reflexión debe expresar qué recuerdas"),
                            Array.Empty<Recuerdo>());
                    }
                }

                var creados = new List<Recuerdo>(reflexiones.Count);
                foreach (var reflexion in reflexiones)
                {
                    var importancia = importanciaReflexion
                        ?? InferirImportanciaDesdeDiario(estado, reflexion.OrigenDiario, valorPorDefecto: 0.75);
                    creados.Add(estado.AgregarRecuerdo(reflexion.Texto, importancia, reflexion.OrigenDiario));
                }

                return new ResultadoAplicacionMemoria(true, null, creados);
            }

            if (memoria.Recordar is { Count: > 0 } recordar)
            {
                var creados = new List<Recuerdo>(recordar.Count);
                foreach (var texto in recordar)
                {
                    if (string.IsNullOrWhiteSpace(texto))
                    {
                        continue;
                    }

                    creados.Add(estado.AgregarRecuerdo(texto, importanciaProsa));
                }

                return new ResultadoAplicacionMemoria(true, null, creados);
            }

            return new ResultadoAplicacionMemoria(true, null, Array.Empty<Recuerdo>());
        }

        /// <summary>
        /// Proyecta el <see cref="EstadoPersonaje"/> custodiado por el motor hacia una
        /// <see cref="InstantaneaMemoria"/> para <c>GET /api/v1/memoria/{personaje}</c> (ADS §5, CP-206).
        /// </summary>
        public static InstantaneaMemoria ProyectarInstantaneaMemoria(
            EstadoPersonaje estado,
            string personaje,
            string epoca = "")
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            var instantanea = new InstantaneaMemoria
            {
                Epoca = epoca ?? string.Empty,
                Personaje = personaje ?? string.Empty,
                Diario = estado.Diario
                    .Select(e => new EntradaDiarioMemoria
                    {
                        Id = e.Id,
                        DiaJuego = e.DiaJuego,
                        Hora = e.Hora,
                        ResumenProsa = e.ResumenProsa,
                        Saliencia = e.Saliencia,
                        Entidades = new List<string>(e.Entidades ?? new List<string>()),
                    })
                    .ToList(),
                Recuerdos = estado.Recuerdos
                    .Select(r => new RecuerdoMemoria
                    {
                        Id = r.Id,
                        Texto = r.Texto,
                        Importancia = r.Importancia,
                        OrigenDiario = new List<string>(r.OrigenDiario ?? new List<string>()),
                    })
                    .ToList(),
                Relaciones = estado.Relaciones.ToDictionary(
                    kv => kv.Key,
                    kv => new RelacionMemoria
                    {
                        Impresion = kv.Value.Impresion,
                        UltimaVez = kv.Value.UltimaVez is null
                            ? null
                            : new TiempoJuego
                            {
                                Dia = kv.Value.UltimaVez.Dia,
                                Hora = kv.Value.UltimaVez.Hora,
                            },
                    },
                    StringComparer.Ordinal),
                Compromisos = estado.Compromisos
                    .Select(c => new CompromisoProximo
                    {
                        Id = c.Id,
                        Con = c.Destinatario,
                        Prosa = string.IsNullOrWhiteSpace(c.Tipo) ? c.Objeto : $"{c.Tipo}: {c.Objeto}",
                        PlazoJuego = new TiempoJuego
                        {
                            Dia = c.PlazoVigente?.Dia ?? 1,
                            Hora = c.PlazoVigente?.Hora ?? 600,
                        },
                    })
                    .ToList(),
                Portafolio = estado.Portafolio
                    .Select(o => new Obra
                    {
                        ObraId = o.ObraId,
                        Estado = o.Estado,
                        Decision = o.Decision is null
                            ? new DecisionObra()
                            : new DecisionObra
                            {
                                Titulo = o.Decision.Titulo,
                                Tema = o.Decision.Tema,
                                Composicion = o.Decision.Composicion,
                                Paleta = o.Decision.Paleta,
                                Nota = o.Decision.Nota,
                            },
                    })
                    .ToList(),
            };

            return instantanea;
        }

        /// <summary>Proyecta la prosa de los recuerdos del personaje (incluidos los heredados sin origen).</summary>
        public static List<string> ProyectarRecuerdosParaPulso(EstadoPersonaje estado, int? maxRecuerdos = null)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            var textos = estado.Recuerdos
                .Where(r => !string.IsNullOrWhiteSpace(r.Texto))
                .Select(r => r.Texto);

            if (maxRecuerdos is int limite && limite >= 0)
            {
                textos = textos.Take(limite);
            }

            return textos.ToList();
        }

        private static double InferirImportanciaDesdeDiario(
            EstadoPersonaje estado,
            IReadOnlyCollection<string> origenDiario,
            double valorPorDefecto)
        {
            if (origenDiario.Count == 0 || estado.Diario.Count == 0)
            {
                return valorPorDefecto;
            }

            var ids = new HashSet<string>(origenDiario, StringComparer.Ordinal);
            var encontrada = false;
            var maxSaliencia = 0.0;
            foreach (var ep in estado.Diario)
            {
                if (ids.Contains(ep.Id))
                {
                    encontrada = true;
                    maxSaliencia = Math.Max(maxSaliencia, ep.Saliencia);
                }
            }

            return encontrada ? Math.Clamp(maxSaliencia, 0.0, 1.0) : valorPorDefecto;
        }

        private static EstadoPersonaje FiltrarSinIndices(EstadoPersonaje origen, List<int> ordenPoda, int cantidadAPodar)
        {
            var excluidos = new HashSet<int>(ordenPoda.Take(cantidadAPodar));
            var diarioFiltrado = new List<EpisodioDiario>(Math.Max(0, origen.Diario.Count - excluidos.Count));
            for (var i = 0; i < origen.Diario.Count; i++)
            {
                if (!excluidos.Contains(i))
                {
                    diarioFiltrado.Add(origen.Diario[i]);
                }
            }

            return new EstadoPersonaje
            {
                VersionEsquema = origen.VersionEsquema,
                SiguienteIdEpisodio = origen.SiguienteIdEpisodio,
                Diario = diarioFiltrado,
                Recuerdos = origen.Recuerdos,
                Relaciones = origen.Relaciones,
                Portafolio = origen.Portafolio,
                Compromisos = origen.Compromisos,
                Contadores = origen.Contadores,
                IntencionActiva = origen.IntencionActiva,
            };
        }
    }
}
