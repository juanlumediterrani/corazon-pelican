using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Dialogo;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>Candidato evaluado para iniciativa espontánea de acercamiento al jugador (CP-613).</summary>
    public sealed record CandidatoIniciativaSocial(
        string InterlocutorRef,
        string Nombre,
        double Disposicion,
        string MotivoProsa);

    /// <summary>
    /// Evalúa si el personaje siente un impulso natural de acercarse a un jugador cercano en función de su
    /// ánimo (<see cref="GestorHumor"/>), relación (<see cref="RelacionEstado"/>, corazones, días sin hablar)
    /// y circunstancia actual (compromisos, sucesos recientes, atención, energía), sujeto a enfriamientos
    /// estrictos que garantizan un comportamiento esporádico y no recurrente (CP-613).
    /// </summary>
    public static class EvaluadorIniciativaSocial
    {
        public static IReadOnlyList<CandidatoIniciativaSocial> Evaluar(
            Observacion obs,
            EstadoPersonaje estado,
            PerfilPersonaje perfil,
            EstadoAtencion estadoAtencion,
            Func<int, string, bool>? yaConsumioOHayDialogoHoy = null)
        {
            if (obs is null || estado is null || perfil is null)
            {
                return Array.Empty<CandidatoIniciativaSocial>();
            }

            var cfg = perfil.ObtenerConfigIniciativaSocial();
            if (!cfg.Habilitada)
            {
                return Array.Empty<CandidatoIniciativaSocial>();
            }

            var dia = Math.Max(1, obs.Dia);
            var registro = estado.IniciativaSocial;
            if (registro is not null
                && registro.UltimoDiaGlobal > 0
                && dia - registro.UltimoDiaGlobal < Math.Max(1, cfg.EnfriamientoGlobalDias))
            {
                return Array.Empty<CandidatoIniciativaSocial>();
            }

            var energiaBaja = obs.Sucesos?.Any(s => string.Equals(s.Tipo, "energia_baja", StringComparison.Ordinal)) == true;
            if (energiaBaja && estadoAtencion == EstadoAtencion.Estrecha)
            {
                return Array.Empty<CandidatoIniciativaSocial>();
            }

            var hora = obs.Hora;
            if (hora >= 2300)
            {
                return Array.Empty<CandidatoIniciativaSocial>();
            }

            var tiempoActual = new TiempoJuego { Dia = dia, Hora = hora };
            var humor = GestorHumor.ObtenerVigente(estado, tiempoActual, perfil.ObtenerConfigHumor(), disiparSiDebil: false);
            var episodiosRecientes = estado.Diario.TakeLast(5).ToList();
            var episodiosEnSoledad = episodiosRecientes.Count >= 3
                && episodiosRecientes.All(ep => !ep.Entidades.Any(r => r.StartsWith("farmer:", StringComparison.Ordinal)));

            var resultado = new List<CandidatoIniciativaSocial>();

            foreach (var ent in obs.Entidades)
            {
                if (ent.Categoria != CategoriaEntidad.Granjero || !ent.Visible)
                {
                    continue;
                }

                if (yaConsumioOHayDialogoHoy is not null && yaConsumioOHayDialogoHoy(dia, ent.Ref))
                {
                    continue;
                }

                if (registro?.UltimosPorJugador is not null
                    && registro.UltimosPorJugador.TryGetValue(ent.Ref, out var ultimoDiaJugador)
                    && ultimoDiaJugador > 0
                    && dia - ultimoDiaJugador < Math.Max(1, cfg.EnfriamientoPorJugadorDias))
                {
                    continue;
                }

                var dist = ent.DistanciaDesde(obs.X, obs.Y);
                if (dist < cfg.DistanciaMinCasillas || dist > cfg.DistanciaMaxCasillas)
                {
                    continue;
                }

                var tieneCompromiso = estado.Compromisos.Any(c =>
                    string.Equals(c.Destinatario, ent.Ref, StringComparison.Ordinal)
                    && string.Equals(c.Estado, EstadosCompromiso.Pendiente, StringComparison.Ordinal));
                var tieneSucesoReciente = obs.Sucesos?.Any(s => string.Equals(s.ActorRef, ent.Ref, StringComparison.Ordinal)) == true
                    || estado.Diario.TakeLast(4).Any(ep => ep.DiaJuego >= Math.Max(1, dia - 1) && ep.Entidades.Contains(ent.Ref));

                var corazones = ent.Corazones ?? 0;
                if (corazones < cfg.CorazonesMinimos && !tieneCompromiso && !tieneSucesoReciente)
                {
                    continue;
                }

                double puntuacion = 0.0;
                var razones = new List<string>();

                // 1. Relación y tiempo desde la última conversación.
                if (corazones >= cfg.CorazonesMinimos)
                {
                    puntuacion += 0.22 + Math.Min(0.14, (corazones - cfg.CorazonesMinimos) * 0.025);
                    if (corazones >= 6)
                    {
                        razones.Add($"hay confianza cercana con {ent.Nombre}");
                    }
                }

                if (estado.Relaciones.TryGetValue(ent.Ref, out var rel) && rel.UltimaVez is not null)
                {
                    var diasSinHablar = dia - Math.Max(1, rel.UltimaVez.Dia);
                    if (diasSinHablar >= Math.Max(1, cfg.DiasSinHablarParaImpulso))
                    {
                        puntuacion += Math.Min(0.22, 0.10 + (diasSinHablar - cfg.DiasSinHablarParaImpulso) * 0.04);
                        razones.Add($"llevas {diasSinHablar} días sin hablar con {ent.Nombre}");
                    }
                }
                else if (corazones >= cfg.CorazonesMinimos)
                {
                    puntuacion += 0.14;
                    razones.Add($"hace tiempo que no charlas con {ent.Nombre}");
                }

                // 2. Estado de ánimo vigente y necesidad de compañía.
                if (humor is not null && humor.Intensidad >= 0.25)
                {
                    puntuacion += Math.Min(0.30, humor.Intensidad * 0.35);
                    razones.Add($"tu ánimo actual («{humor.Resumen}») te empuja a compartirlo");
                }
                else if (episodiosEnSoledad)
                {
                    puntuacion += 0.15;
                    razones.Add("llevas varios momentos a solas y te apetece compañía");
                }

                // 3. Circunstancia concreta (compromiso, suceso reciente, ocupación, energía).
                if (tieneCompromiso)
                {
                    puntuacion += 0.28;
                    razones.Add($"tienes un asunto pendiente con {ent.Nombre}");
                }

                if (tieneSucesoReciente)
                {
                    puntuacion += 0.16;
                    razones.Add($"lo ocurrido hace poco con {ent.Nombre} sigue muy presente");
                }

                if (estadoAtencion == EstadoAtencion.Estrecha)
                {
                    puntuacion -= 0.15;
                }

                if (energiaBaja)
                {
                    puntuacion -= 0.15;
                }

                var total = Math.Clamp(puntuacion, 0.0, 1.0);
                if (total >= cfg.UmbralDisposicion)
                {
                    var motivo = razones.Count > 0
                        ? $"Sientes un impulso natural de acercarte a {ent.Nombre} porque {string.Join(" y ", razones)}."
                        : $"Te apetece cruzar a saludar a {ent.Nombre}.";
                    resultado.Add(new CandidatoIniciativaSocial(ent.Ref, ent.Nombre, total, motivo));
                }
            }

            return resultado
                .OrderByDescending(c => c.Disposicion)
                .ThenBy(c => c.InterlocutorRef, StringComparer.Ordinal)
                .ToList();
        }

        public static void RegistrarIniciativa(EstadoPersonaje estado, int dia, string interlocutorRef)
        {
            if (estado is null || string.IsNullOrWhiteSpace(interlocutorRef))
            {
                return;
            }

            var diaValido = Math.Max(1, dia);
            estado.IniciativaSocial ??= new IniciativaSocialEstado();
            estado.IniciativaSocial.UltimoDiaGlobal = Math.Max(estado.IniciativaSocial.UltimoDiaGlobal, diaValido);
            estado.IniciativaSocial.UltimosPorJugador[interlocutorRef.Trim()] = diaValido;
        }
    }
}
