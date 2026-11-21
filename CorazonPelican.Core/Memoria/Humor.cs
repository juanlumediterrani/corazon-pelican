using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Memoria
{
    /// <summary>Resultado de validar y aplicar una propuesta de humor (P-9, CP-210).</summary>
    public sealed record ResultadoAplicacionHumor(
        bool Aceptada,
        MotivoRechazo? MotivoRechazo,
        HumorVigente? HumorVigente,
        bool Aplicada);

    /// <summary>
    /// Custodia, decaimiento y sustitución del humor nacido de episodios vividos (P-9, CP-210).
    /// La mente interpreta y propone; el host valida origen y época, aplica los límites del perfil y
    /// atenúa o disipa la impresión con el tiempo de juego sin borrar los recuerdos asociados.
    /// </summary>
    public static class GestorHumor
    {
        public static double CalcularDiasTranscurridos(TiempoJuego? desde, TiempoJuego? hasta)
        {
            if (desde is null || hasta is null)
            {
                return 0.0;
            }

            var minDesde = ConvertirAMinutosJuegoAbsolutos(desde);
            var minHasta = ConvertirAMinutosJuegoAbsolutos(hasta);
            return Math.Max(0.0, (minHasta - minDesde) / 1440.0);
        }

        public static double CalcularIntensidad(EstadoHumor estadoHumor, TiempoJuego ahora, ConfigHumor? config = null)
        {
            if (estadoHumor is null)
            {
                throw new ArgumentNullException(nameof(estadoHumor));
            }

            if (ahora is null)
            {
                throw new ArgumentNullException(nameof(ahora));
            }

            var cfg = config ?? new ConfigHumor();
            var semivida = cfg.SemividaDias > 0 ? cfg.SemividaDias : ConfigHumor.SemividaDiasPorDefecto;
            var dias = CalcularDiasTranscurridos(estadoHumor.CreadoEn, ahora);
            var intensidad = Math.Clamp(estadoHumor.IntensidadInicial, 0.0, 1.0) * Math.Pow(0.5, dias / semivida);
            return Math.Clamp(intensidad, 0.0, 1.0);
        }

        /// <summary>
        /// Calcula el humor vigente en <paramref name="ahora"/>. Si cayó por debajo del umbral de disipación,
        /// despeja <see cref="EstadoPersonaje.Humor"/> conservando intactos <see cref="EstadoPersonaje.Recuerdos"/>.
        /// </summary>
        public static HumorVigente? ObtenerVigente(
            EstadoPersonaje estado,
            TiempoJuego? ahora,
            ConfigHumor? config = null,
            bool disiparSiDebil = true)
        {
            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            if (estado.Humor is null || string.IsNullOrWhiteSpace(estado.Humor.Resumen))
            {
                return null;
            }

            var cfg = config ?? new ConfigHumor();
            var referencia = ahora ?? estado.Humor.CreadoEn;
            var intensidad = CalcularIntensidad(estado.Humor, referencia, cfg);

            if (intensidad < cfg.UmbralDisipacion)
            {
                if (disiparSiDebil)
                {
                    estado.Humor = null;
                }

                return null;
            }

            return AContratoVigente(estado.Humor, intensidad);
        }

        /// <summary>Valida época, pulso base, rango y procedencia verificable de <see cref="Respuesta.Humor"/>.</summary>
        public static MotivoRechazo? ValidarPropuesta(Pulso pulso, Respuesta respuesta, EstadoPersonaje estado)
        {
            if (pulso is null)
            {
                throw new ArgumentNullException(nameof(pulso));
            }

            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            if (estado is null)
            {
                throw new ArgumentNullException(nameof(estado));
            }

            var errorEpoca = ValidadorEpoca.Comprobar(respuesta, pulso.Epoca);
            if (errorEpoca is not null)
            {
                return errorEpoca;
            }

            if (respuesta.BasadaEn != pulso.Id)
            {
                return new MotivoRechazo("pulso_distinto", "basada_en", "esa impresión pertenece a otro momento");
            }

            var propuesta = respuesta.Humor;
            if (propuesta is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(propuesta.Resumen))
            {
                return new MotivoRechazo("humor_vacio", "humor.resumen", "describe en pocas palabras qué ánimo te dejó lo vivido");
            }

            if (double.IsNaN(propuesta.Intensidad) || double.IsInfinity(propuesta.Intensidad)
                || propuesta.Intensidad <= 0 || propuesta.Intensidad > 1)
            {
                return new MotivoRechazo("humor_intensidad_invalida", "humor.intensidad", "la fuerza del ánimo debe situarse entre 0 y 1");
            }

            if (propuesta.OrigenDiario is null || propuesta.OrigenDiario.Count == 0)
            {
                return new MotivoRechazo("origen_ausente", "humor.origen_diario", "indica qué episodio vivido te dejó ese ánimo");
            }

            var conocidosEnEstado = new HashSet<string>(
                estado.Diario.Select(e => e.Id).Where(id => !string.IsNullOrWhiteSpace(id)),
                StringComparer.Ordinal);
            var vistos = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < propuesta.OrigenDiario.Count; i++)
            {
                var epId = propuesta.OrigenDiario[i];
                if (string.IsNullOrWhiteSpace(epId) || !vistos.Add(epId))
                {
                    return new MotivoRechazo("origen_duplicado", $"humor.origen_diario[{i}]", "no repitas ni dejes vacío el episodio de origen");
                }

                var enPulso = pulso.EpisodiosDiario is not null && pulso.EpisodiosDiario.ContainsKey(epId);
                var enEstado = conocidosEnEstado.Contains(epId);
                var valido = pulso.EpisodiosDiario is not null ? enPulso : enEstado;
                if (!valido)
                {
                    return new MotivoRechazo(
                        "episodio_desconocido",
                        $"humor.origen_diario[{i}]",
                        "ese episodio no figura entre los que has vivido y recuerdas");
                }
            }

            return null;
        }

        /// <summary>
        /// Valida y aplica <see cref="Respuesta.Humor"/> de forma idempotente, aplicando el tope y la
        /// regla de sustitución del perfil.
        /// </summary>
        public static ResultadoAplicacionHumor AplicarPropuesta(
            EstadoPersonaje estado,
            Pulso pulso,
            Respuesta respuesta,
            ConfigHumor? config = null)
        {
            var rechazo = ValidarPropuesta(pulso, respuesta, estado);
            if (rechazo is not null)
            {
                return new ResultadoAplicacionHumor(false, rechazo, null, false);
            }

            var cfg = config ?? new ConfigHumor();
            var ahora = ClonarTiempo(pulso.TiempoJuego);
            var propuesta = respuesta.Humor;
            if (propuesta is null)
            {
                return new ResultadoAplicacionHumor(true, null, ObtenerVigente(estado, ahora, cfg), false);
            }

            if (estado.Humor is not null
                && string.Equals(estado.Humor.Epoca, respuesta.Epoca, StringComparison.Ordinal)
                && estado.Humor.BasadaEnPulso == respuesta.BasadaEn)
            {
                return new ResultadoAplicacionHumor(true, null, ObtenerVigente(estado, ahora, cfg), false);
            }

            var actual = ObtenerVigente(estado, ahora, cfg, disiparSiDebil: true);
            var intensidadAcotada = Math.Clamp(propuesta.Intensidad, 0.0, cfg.MaxIntensidad);
            if (intensidadAcotada < cfg.UmbralDisipacion)
            {
                return new ResultadoAplicacionHumor(true, null, actual, false);
            }

            var reemplaza = actual is null
                || propuesta.Significativa == true
                || intensidadAcotada >= cfg.UmbralSustitucion
                || intensidadAcotada > actual.Intensidad;

            if (!reemplaza)
            {
                return new ResultadoAplicacionHumor(true, null, actual, false);
            }

            estado.Humor = new EstadoHumor
            {
                Resumen = propuesta.Resumen.Trim(),
                IntensidadInicial = intensidadAcotada,
                OrigenDiario = propuesta.OrigenDiario.Distinct(StringComparer.Ordinal).ToList(),
                CreadoEn = ahora,
                Epoca = respuesta.Epoca,
                BasadaEnPulso = respuesta.BasadaEn,
            };

            return new ResultadoAplicacionHumor(
                true,
                null,
                AContratoVigente(estado.Humor, intensidadAcotada),
                true);
        }

        private static HumorVigente AContratoVigente(EstadoHumor humor, double intensidadActual)
            => new()
            {
                Resumen = humor.Resumen,
                Intensidad = Math.Round(intensidadActual, 4),
                IntensidadInicial = Math.Round(humor.IntensidadInicial, 4),
                OrigenDiario = new List<string>(humor.OrigenDiario ?? new List<string>()),
                CreadoEn = ClonarTiempo(humor.CreadoEn),
            };

        private static TiempoJuego ClonarTiempo(TiempoJuego? t)
            => t is null
                ? new TiempoJuego { Dia = 1, Hora = HoraJuego.InicioDia }
                : new TiempoJuego
                {
                    Dia = Math.Max(1, t.Dia),
                    Hora = Math.Clamp(t.Hora, HoraJuego.InicioDia, HoraJuego.FinDia),
                };

        private static double ConvertirAMinutosJuegoAbsolutos(TiempoJuego t)
        {
            var dia = Math.Max(1, t.Dia);
            var hora = Math.Clamp(t.Hora, HoraJuego.InicioDia, HoraJuego.FinDia);
            return ((dia - 1) * 1440.0) + ((hora / 100) * 60.0) + (hora % 100);
        }
    }
}
