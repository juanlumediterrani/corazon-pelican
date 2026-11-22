using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>Registro de un reflejo disparado por las reglas declarativas del perfil (CP-304).</summary>
    public sealed record ReflejoDisparado(
        string ReglaId,
        string EntidadRef,
        PasoPlan Paso,
        long MilisegundosReales);

    /// <summary>
    /// Evaluador determinista de reflejos del perfil en menos de 100 ms (GSS §2.1, §6.4; ADS §4 paso 6; CP-304).
    /// Produce gestos o ajustes de postura inmediatos (<c>guardar_cuaderno</c>, <c>levantar_la_vista</c>,
    /// <c>mirar</c>, <c>gesto</c>, <c>murmurar</c>) sin invocar a la mente ni consumir presupuesto de pulsos.
    /// </summary>
    public sealed class EvaluadorReflejos
    {
        private readonly PerfilPersonaje perfil;
        private readonly Dictionary<string, long> ultimosDisparosMs = new(StringComparer.Ordinal);

        public EvaluadorReflejos(PerfilPersonaje perfil)
        {
            this.perfil = perfil ?? throw new ArgumentNullException(nameof(perfil));
        }

        public List<ReflejoDisparado> Historial { get; } = new();

        public void ReiniciarEnfriamientos()
        {
            this.ultimosDisparosMs.Clear();
            this.Historial.Clear();
        }

        /// <summary>
        /// Evalúa las reglas declarativas del perfil sobre la observación y bandas actuales y, si una
        /// coincide y no está en enfriamiento, aplica inmediatamente el reflejo sobre <paramref name="ejecutor"/>
        /// en el mismo paso de observación.
        /// </summary>
        public ReflejoDisparado? EvaluarYEjecutar(
            Observacion observacion,
            IReadOnlyList<EntidadEnBanda> bandas,
            EjecutorCuerpo ejecutor,
            long ahoraMs)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            if (bandas is null)
            {
                throw new ArgumentNullException(nameof(bandas));
            }

            if (ejecutor is null)
            {
                throw new ArgumentNullException(nameof(ejecutor));
            }

            // No interrumpir una acción con duración en marcha (p. ej. caminando o sentándose).
            if (ejecutor.Ocupado)
            {
                return null;
            }

            var entidadesPorRef = observacion.Entidades.ToDictionary(e => e.Ref, e => e, StringComparer.Ordinal);
            var reglas = this.perfil.ObtenerReglasReflejo();

            foreach (var regla in reglas)
            {
                if (regla is null || string.IsNullOrWhiteSpace(regla.Id) || string.IsNullOrWhiteSpace(regla.Accion))
                {
                    continue;
                }

                var cond = regla.Si ?? new CondicionReflejo();
                if (!CoincideActividadContinua(cond.ActividadContinua, ejecutor.Continua))
                {
                    continue;
                }

                var bandaMinima = ResolverBanda(cond.BandaMinima);
                foreach (var enBanda in bandas.Where(b => b.Banda >= bandaMinima).OrderByDescending(b => b.Saliencia))
                {
                    if (!entidadesPorRef.TryGetValue(enBanda.Ref, out var entidad))
                    {
                        continue;
                    }

                    if (!CoincideCategoria(entidad.Categoria, cond.Categoria))
                    {
                        continue;
                    }

                    var corazones = Math.Max(0, entidad.Corazones ?? 0);
                    if (cond.DesdeCorazones.HasValue && corazones < cond.DesdeCorazones.Value)
                    {
                        continue;
                    }

                    if (cond.HastaCorazones.HasValue && corazones > cond.HastaCorazones.Value)
                    {
                        continue;
                    }

                    if (cond.RequiereEnsenar.HasValue
                        && ResolverPermiteEnsenar(this.perfil.Apertura, corazones) != cond.RequiereEnsenar.Value)
                    {
                        continue;
                    }

                    var claveEnfriamiento = $"{regla.Id}:{entidad.Ref}";
                    var enfriamientoMs = (long)(Math.Max(0.0, regla.EnfriamientoS) * 1000.0);
                    if (this.ultimosDisparosMs.TryGetValue(claveEnfriamiento, out var ultimoMs)
                        && ahoraMs - ultimoMs < enfriamientoMs)
                    {
                        continue;
                    }

                    var paso = ConstruirPasoReflejo(regla, entidad.Ref);
                    if (paso is null || !ejecutor.EjecutarReflejo(paso, ahoraMs))
                    {
                        continue;
                    }

                    this.ultimosDisparosMs[claveEnfriamiento] = ahoraMs;
                    var disparado = new ReflejoDisparado(regla.Id, entidad.Ref, paso, ahoraMs);
                    this.Historial.Add(disparado);
                    return disparado;
                }
            }

            return null;
        }

        public static bool ResolverPermiteEnsenar(IReadOnlyList<TramoApertura>? apertura, int corazones)
        {
            if (apertura is null || apertura.Count == 0)
            {
                return true;
            }

            TramoApertura? tramoAplicable = null;
            foreach (var tramo in apertura.OrderBy(t => t.DesdeCorazones))
            {
                if (corazones >= tramo.DesdeCorazones)
                {
                    tramoAplicable = tramo;
                }
            }

            return tramoAplicable?.Ensenar ?? false;
        }

        private static bool CoincideActividadContinua(string? esperada, string? actual)
        {
            if (string.IsNullOrWhiteSpace(esperada))
            {
                return true;
            }

            if (string.Equals(esperada, "ninguna", StringComparison.OrdinalIgnoreCase))
            {
                return string.IsNullOrEmpty(actual);
            }

            return string.Equals(esperada, actual, StringComparison.Ordinal);
        }

        private static Banda ResolverBanda(string? nombre) => nombre?.ToLowerInvariant() switch
        {
            "periferia" => Banda.Periferia,
            "mencion" => Banda.Mencion,
            _ => Banda.Foco,
        };

        private static bool CoincideCategoria(CategoriaEntidad categoria, string? filtro)
        {
            if (string.IsNullOrWhiteSpace(filtro))
            {
                return true;
            }

            return filtro.ToLowerInvariant() switch
            {
                "granjero" => categoria == CategoriaEntidad.Granjero,
                "npc" => categoria == CategoriaEntidad.Npc,
                "persona" => categoria is CategoriaEntidad.Granjero or CategoriaEntidad.Npc,
                _ => true,
            };
        }

        private static PasoPlan? ConstruirPasoReflejo(ReglaReflejo regla, string entidadRef) => regla.Accion switch
        {
            "guardar_cuaderno" => new PasoPlan { Accion = "guardar_cuaderno" },
            "levantar_la_vista" => new PasoPlan { Accion = "levantar_la_vista" },
            "mirar" => new PasoPlan { Accion = "mirar", Objetivo = entidadRef },
            "gesto" => new PasoPlan { Accion = "gesto", Tipo = string.IsNullOrWhiteSpace(regla.Tipo) ? "saludo" : regla.Tipo },
            "murmurar" => new PasoPlan { Accion = "murmurar", Texto = string.IsNullOrWhiteSpace(regla.Texto) ? "..." : regla.Texto },
            _ => null,
        };
    }
}
