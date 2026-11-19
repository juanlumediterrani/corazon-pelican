using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Perfil
{
    /// <summary>Cómo tratar lo que todavía no se ha medido en vivo (áreas del léxico, V-09).</summary>
    public enum ModoValidacion
    {
        /// <summary>Partida de desarrollo: un área pendiente de medir es un aviso.</summary>
        Desarrollo,

        /// <summary>Juego normal: un área pendiente de medir es un error y el personaje no se gobierna.</summary>
        Normal,
    }

    /// <summary>Un problema del perfil, con el campo afectado y una explicación legible.</summary>
    public sealed record HallazgoPerfil(string Campo, string Mensaje)
    {
        public override string ToString() => $"{this.Campo}: {this.Mensaje}";
    }

    public sealed class ResultadoValidacionPerfil
    {
        public List<HallazgoPerfil> Errores { get; } = new();

        public List<HallazgoPerfil> Avisos { get; } = new();

        public bool EsValido => this.Errores.Count == 0;
    }

    /// <summary>
    /// Validación determinista de un perfil al cargarlo (ADS §2, CP-003): reglas semánticas que el
    /// esquema no expresa. Un perfil con errores no se gobierna.
    /// </summary>
    public static class PerfilValidator
    {
        public static readonly IReadOnlyCollection<string> FormatosObra = new HashSet<string> { "boceto", "cuadro_pequeno", "cuadro_mediano" };

        public static readonly IReadOnlyCollection<string> CambiosRutina = new HashSet<string> { "saltar", "mover", "sustituir" };

        public static readonly IReadOnlyCollection<string> CondicionesNunca = new HashSet<string> { "festival", "lluvia", "boda" };

        /// <summary>Hitos del motor, comunes a todos los personajes: no salen del horario y siempre abren cadena.</summary>
        public static readonly IReadOnlyCollection<string> HitosDelMotor = new HashSet<string> { "despertar", "acostarse" };

        private static readonly Regex IdLexico = new(@"\A[a-z][a-z0-9_]*\z", RegexOptions.Compiled);

        internal static bool EsIdValido(string? id) => IdLexico.IsMatch(id ?? "");

        public static ResultadoValidacionPerfil Validar(PerfilPersonaje perfil, ModoValidacion modo)
        {
            var r = new ResultadoValidacionPerfil();
            ValidarIdentidad(perfil, r);
            ValidarRasgos(perfil, r);
            ValidarUmbrales(perfil.Umbrales, r);
            ValidarRefractario(perfil.Refractario, r);
            ValidarPresupuesto("presupuesto.vanilla", perfil.Presupuesto.Vanilla, r);
            ValidarPresupuesto("presupuesto.pelican", perfil.Presupuesto.Pelican, r);
            ValidarLexico(perfil.Lexico, modo, r);
            ValidarApertura(perfil.Apertura, r);
            ValidarAcciones(perfil.Acciones, r);
            ValidarFormatos(perfil.FormatosObra, r);
            ValidarRutina(perfil.Rutina, r);
            return r;
        }

        /// <summary>El área <c>[0,0,0,0]</c> es el marcador de «pendiente de medir en vivo».</summary>
        public static bool AreaPendiente(Lugar lugar) => lugar.Area.Count == 4 && lugar.Area.All(v => v == 0);

        private static void ValidarIdentidad(PerfilPersonaje p, ResultadoValidacionPerfil r)
        {
            if (!IdLexico.IsMatch(p.Id ?? ""))
            {
                r.Errores.Add(new("id", $"«{p.Id}» no es un id válido: minúsculas, dígitos y «_», empezando por letra."));
            }

            if (string.IsNullOrWhiteSpace(p.Npc))
            {
                r.Errores.Add(new("npc", "falta el nombre interno del NPC que gobierna este perfil."));
            }

            if (string.IsNullOrWhiteSpace(p.Plantillas))
            {
                r.Errores.Add(new("plantillas", "falta la ruta de las plantillas de prosa."));
            }

            if (string.IsNullOrWhiteSpace(p.Voz))
            {
                r.Errores.Add(new("voz", "falta la ruta de la voz del personaje."));
            }
        }

        private static void ValidarRasgos(PerfilPersonaje p, ResultadoValidacionPerfil r)
        {
            if (p.Rasgos.Count == 0)
            {
                r.Errores.Add(new("rasgos", "un personaje sin rasgos no tiene atención propia (pilar 2)."));
            }

            foreach (var (rasgo, peso) in p.Rasgos)
            {
                if (!IdLexico.IsMatch(rasgo))
                {
                    r.Errores.Add(new($"rasgos.{rasgo}", "el nombre de un rasgo debe ser un id del léxico."));
                }

                if (peso < 0)
                {
                    r.Errores.Add(new($"rasgos.{rasgo}", $"el peso {peso} es negativo."));
                }
            }
        }

        private static void ValidarUmbrales(Umbrales u, ResultadoValidacionPerfil r)
        {
            if (!(u.Periferia > 0 && u.Periferia < u.Mencion && u.Mencion < u.Foco && u.Foco < u.Pico))
            {
                r.Errores.Add(new(
                    "umbrales",
                    $"deben cumplir 0 < periferia < mencion < foco < pico; ahora son periferia {u.Periferia}, mencion {u.Mencion}, foco {u.Foco}, pico {u.Pico}."));
            }

            var huecoMinimo = new[] { u.Mencion - u.Periferia, u.Foco - u.Mencion, u.Pico - u.Foco }.Min();
            if (u.Histeresis < 0 || u.Histeresis >= huecoMinimo / 2)
            {
                r.Errores.Add(new("umbrales.histeresis", $"debe estar entre 0 y la mitad del menor hueco entre umbrales ({huecoMinimo / 2:0.###}); es {u.Histeresis}."));
            }

            if (u.Deriva <= 0)
            {
                r.Errores.Add(new("umbrales.deriva", "el umbral de deriva debe ser mayor que 0."));
            }

            if (u.DerivaSemividaMin <= 0)
            {
                r.Errores.Add(new("umbrales.deriva_semivida_min", "la fuga de la deriva necesita una semivida mayor que 0 minutos."));
            }
        }

        private static void ValidarRefractario(Refractario f, ResultadoValidacionPerfil r)
        {
            if (f.Subida < 0)
            {
                r.Errores.Add(new("refractario.subida", "no puede ser negativa."));
            }

            if (f.SemividaS <= 0)
            {
                r.Errores.Add(new("refractario.semivida_s", "debe ser mayor que 0 segundos."));
            }
        }

        private static void ValidarPresupuesto(string campo, PresupuestoModo m, ResultadoValidacionPerfil r)
        {
            if (m.ExtraCompromisos < 0)
            {
                r.Errores.Add(new($"{campo}.extra_compromisos", "la bolsa extra de compromisos debe ser cero o mayor."));
            }

            if (m.ReservadosCadenas < HitosDelMotor.Count)
            {
                r.Errores.Add(new($"{campo}.reservados_cadenas", $"debe reservar al menos {HitosDelMotor.Count} pulsos (despertar y acostarse)."));
            }

            if (m.PulsosPorDiaJuego <= m.ReservadosCadenas)
            {
                r.Errores.Add(new($"{campo}.pulsos_por_dia_juego", $"{m.PulsosPorDiaJuego} no deja pulsos libres tras reservar {m.ReservadosCadenas} para las cadenas."));
            }
        }

        private static void ValidarLexico(Lexico l, ModoValidacion modo, ResultadoValidacionPerfil r)
        {
            if (l.Lugares.Count == 0)
            {
                r.Errores.Add(new("lexico.lugares", "un personaje necesita al menos un lugar en su léxico."));
            }

            var vistos = new HashSet<string>();
            for (var i = 0; i < l.Lugares.Count; i++)
            {
                var lugar = l.Lugares[i];
                var campo = $"lexico.lugares[{i}]";
                if (!IdLexico.IsMatch(lugar.Id ?? ""))
                {
                    r.Errores.Add(new($"{campo}.id", $"«{lugar.Id}» no es un id válido: minúsculas, dígitos y «_», empezando por letra."));
                }
                else if (!vistos.Add(lugar.Id!))
                {
                    r.Errores.Add(new($"{campo}.id", $"el lugar «{lugar.Id}» está duplicado: el léxico es único (R-14)."));
                }

                if (string.IsNullOrWhiteSpace(lugar.Ubicacion))
                {
                    r.Errores.Add(new($"{campo}.ubicacion", "falta la ubicación del juego."));
                }

                if (string.IsNullOrWhiteSpace(lugar.Prosa))
                {
                    r.Errores.Add(new($"{campo}.prosa", "falta la prosa con la que la escena nombra este lugar."));
                }

                ValidarArea(lugar, campo, modo, r);
            }
        }

        private static void ValidarArea(Lugar lugar, string campo, ModoValidacion modo, ResultadoValidacionPerfil r)
        {
            if (AreaPendiente(lugar))
            {
                var hallazgo = new HallazgoPerfil($"{campo}.area", $"el área de «{lugar.Id}» está pendiente de medir en vivo (V-09, CP-006).");
                (modo == ModoValidacion.Desarrollo ? r.Avisos : r.Errores).Add(hallazgo);
                return;
            }

            if (lugar.Area.Count != 4 || lugar.Area[0] < 0 || lugar.Area[1] < 0 || lugar.Area[2] <= 0 || lugar.Area[3] <= 0)
            {
                r.Errores.Add(new($"{campo}.area", $"debe ser [x, y, ancho, alto] con x, y ≥ 0 y ancho, alto > 0; es [{string.Join(", ", lugar.Area)}]."));
            }
        }

        private static void ValidarApertura(List<TramoApertura> tramos, ResultadoValidacionPerfil r)
        {
            if (tramos.Count == 0 || tramos[0].DesdeCorazones != 0)
            {
                r.Errores.Add(new("apertura", "el primer tramo debe empezar en 0 corazones."));
            }

            for (var i = 1; i < tramos.Count; i++)
            {
                if (tramos[i].DesdeCorazones <= tramos[i - 1].DesdeCorazones)
                {
                    r.Errores.Add(new($"apertura[{i}].desde_corazones", "los tramos deben ir en orden creciente de corazones."));
                }
            }
        }

        private static void ValidarAcciones(List<string> acciones, ResultadoValidacionPerfil r)
        {
            if (acciones.Count == 0)
            {
                r.Errores.Add(new("acciones", "el personaje no tiene ninguna acción."));
            }

            foreach (var accion in acciones.Where(a => !Intencion.Acciones.VocabularioV1.Contains(a)))
            {
                r.Errores.Add(new("acciones", $"«{accion}» no está en el vocabulario v1 (GSS §5.2)."));
            }

            foreach (var repetida in acciones.GroupBy(a => a).Where(g => g.Count() > 1))
            {
                r.Errores.Add(new("acciones", $"«{repetida.Key}» está repetida."));
            }
        }

        private static void ValidarFormatos(List<string> formatos, ResultadoValidacionPerfil r)
        {
            foreach (var formato in formatos.Where(f => !FormatosObra.Contains(f)))
            {
                r.Errores.Add(new("formatos_obra", $"«{formato}» no es un formato de obra conocido."));
            }
        }

        private static void ValidarRutina(Rutina rutina, ResultadoValidacionPerfil r)
        {
            ValidarAnotacion(rutina.PorDefecto, "rutina.por_defecto", esPorDefecto: true, r);

            var vistos = new HashSet<string>();
            for (var i = 0; i < rutina.Anotaciones.Count; i++)
            {
                var anotacion = rutina.Anotaciones[i];
                var campo = $"rutina.anotaciones[{i}]";
                if (!IdLexico.IsMatch(anotacion.Id ?? ""))
                {
                    r.Errores.Add(new($"{campo}.id", $"«{anotacion.Id}» no es un id válido."));
                }
                else if (!vistos.Add(anotacion.Id!))
                {
                    r.Errores.Add(new($"{campo}.id", $"la anotación «{anotacion.Id}» está duplicada."));
                }

                ValidarAnotacion(anotacion, campo, esPorDefecto: false, r);
            }
        }

        private static void ValidarAnotacion(AnotacionHito a, string campo, bool esPorDefecto, ResultadoValidacionPerfil r)
        {
            var esHitoDelMotor = a.Id is not null && HitosDelMotor.Contains(a.Id);

            if (!esPorDefecto && !esHitoDelMotor)
            {
                ValidarPatron(a.Si, campo, r);
            }
            else if (a.Si is not null)
            {
                r.Errores.Add(new($"{campo}.si", esPorDefecto
                    ? "la anotación por defecto se aplica a todas las entradas: no lleva patrón."
                    : $"«{a.Id}» es un hito del motor: no sale del horario y no lleva patrón."));
            }

            if (esHitoDelMotor && a.Forma is not null && a.Forma != "cadena")
            {
                r.Errores.Add(new($"{campo}.forma", $"«{a.Id}» siempre abre una cadena de pensamientos."));
            }

            if (a.Forma is not null && a.Forma != "cadena" && a.Forma != "pensamiento")
            {
                r.Errores.Add(new($"{campo}.forma", $"«{a.Forma}» no es una forma conocida (pensamiento o cadena)."));
            }

            foreach (var fuente in (a.Contexto ?? new List<string>()).Where(f => !FuentesContexto.Conocidas.Contains(f)))
            {
                r.Errores.Add(new($"{campo}.contexto", $"el motor no sabe reunir «{fuente}»; conocidas: {string.Join(", ", FuentesContexto.Conocidas.OrderBy(x => x))}."));
            }

            foreach (var cambio in (a.Cambios ?? new List<string>()).Where(c => !CambiosRutina.Contains(c)))
            {
                r.Errores.Add(new($"{campo}.cambios", $"«{cambio}» no es un cambio de rutina (saltar, mover, sustituir)."));
            }

            if (esHitoDelMotor && a.Cambios is { Count: > 0 })
            {
                r.Errores.Add(new($"{campo}.cambios", $"«{a.Id}» no se puede saltar, mover ni sustituir."));
            }

            if (a.Limites is not null)
            {
                if (a.Limites.MaxPorSemana is < 0)
                {
                    r.Errores.Add(new($"{campo}.limites.max_por_semana", "no puede ser negativo."));
                }

                foreach (var condicion in (a.Limites.Nunca ?? new List<string>()).Where(c => !CondicionesNunca.Contains(c)))
                {
                    r.Errores.Add(new($"{campo}.limites.nunca", $"«{condicion}» no es una condición conocida (festival, lluvia, boda)."));
                }
            }
        }

        private static void ValidarPatron(PatronEntrada? si, string campo, ResultadoValidacionPerfil r)
        {
            if (si is null || (si.Ubicacion is null && si.Animacion is null && si.DesdeHora is null && si.HastaHora is null))
            {
                r.Errores.Add(new($"{campo}.si", "una anotación de rutina necesita un patrón (ubicación, animación o franja) para saber a qué entradas del horario se aplica."));
                return;
            }

            if (si.DesdeHora is not null && si.HastaHora is not null && si.DesdeHora >= si.HastaHora)
            {
                r.Errores.Add(new($"{campo}.si", $"la franja {si.DesdeHora}–{si.HastaHora} está vacía o invertida."));
            }
        }
    }
}
