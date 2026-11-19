using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Sensorio.Prosa
{
    /// <summary>Entidad observada con su atención acumulada desde el último pensamiento (GSS §4.4).</summary>
    public sealed record EntidadAtendida(string Ref, string Prosa, double Atencion);

    /// <summary>
    /// Genera frases breves deterministas de acción y percepción para el búfer, y la frase resumen
    /// de la huella de atención entre pensamientos (GSS §3.4, §4.4; R-01, R-02).
    /// </summary>
    public static class GeneradorAccion
    {
        public static string GenerarAccion(
            PasoPlan paso,
            int dia,
            PlantillasProsa plantillas,
            LugaresOfrecidos? lugares = null,
            IReadOnlyDictionary<string, string>? nombresEntidades = null)
        {
            if (!plantillas.Acciones.TryGetValue(paso.Accion, out var variantes) || variantes.Count == 0)
            {
                throw new InvalidOperationException($"No hay plantillas para la acción «{paso.Accion}».");
            }

            var argumentoClave = paso.Lugar ?? paso.Objetivo ?? paso.Obra ?? paso.Tipo ?? "";
            var plantilla = PlantillasProsa.ElegirVariante(variantes, dia, $"accion:{paso.Accion}:{argumentoClave}");

            var lugar = lugares?.ResolverLugar(paso.Lugar)?.Prosa ?? paso.Lugar ?? "";
            var objetivo = paso.Objetivo is not null && nombresEntidades is not null && nombresEntidades.TryGetValue(paso.Objetivo, out var nombre)
                ? nombre
                : paso.Objetivo ?? "";
            var obra = paso.Obra == "nueva" ? "una obra nueva" : (paso.Obra ?? "el cuaderno");
            var tipo = paso.Tipo ?? "";
            var texto = paso.Texto ?? "";
            var duracion = (paso.DuracionMin ?? 10).ToString();

            return plantilla
                .Replace("{lugar}", lugar, StringComparison.Ordinal)
                .Replace("{objetivo}", objetivo, StringComparison.Ordinal)
                .Replace("{obra}", obra, StringComparison.Ordinal)
                .Replace("{tipo}", tipo, StringComparison.Ordinal)
                .Replace("{texto}", texto, StringComparison.Ordinal)
                .Replace("{duracion}", duracion, StringComparison.Ordinal);
        }

        /// <summary>
        /// Prosa breve para el búfer cuando una entidad alcanza al menos la banda de mención (GSS §4.4).
        /// Devuelve <c>null</c> para periferia o ausente.
        /// </summary>
        public static string? GenerarPercepcion(
            EntidadObservada entidad,
            Banda banda,
            int dia,
            PlantillasProsa plantillas)
        {
            if (banda < Banda.Mencion)
            {
                return null;
            }

            var categoria = PlantillasProsa.ClaveCategoria(entidad.Categoria);
            if (!plantillas.PercepcionBufer.TryGetValue(categoria, out var variantes) || variantes.Count == 0)
            {
                return null;
            }

            var plantilla = PlantillasProsa.ElegirVariante(variantes, dia, $"bufer:{entidad.Ref}");
            return plantilla.Replace("{nombre}", entidad.Nombre, StringComparison.Ordinal);
        }

        /// <summary>
        /// Construye la huella de atención (GSS §4.4): solo nombra entidades que estuvieron en la
        /// observación y utiliza las plantillas deterministas del perfil.
        /// </summary>
        public static Huella GenerarHuella(
            int minutosDesdeUltimoPensamiento,
            IReadOnlyList<EntidadAtendida> entidadesOrdenadas,
            int dia,
            PlantillasProsa plantillas)
        {
            var minutos = Math.Max(0, minutosDesdeUltimoPensamiento);
            var tiempo = FormatearTiempo(minutos);
            string prosa;

            if (entidadesOrdenadas.Count == 0)
            {
                var plantilla = PlantillasProsa.ElegirVariante(plantillas.Huella.SinEntidades, dia, "huella:sin");
                prosa = plantilla.Replace("{tiempo}", tiempo, StringComparison.Ordinal);
            }
            else if (entidadesOrdenadas.Count == 1)
            {
                var primera = entidadesOrdenadas[0];
                var plantilla = PlantillasProsa.ElegirVariante(plantillas.Huella.UnaEntidad, dia, $"huella:1:{primera.Ref}");
                prosa = plantilla
                    .Replace("{tiempo}", tiempo, StringComparison.Ordinal)
                    .Replace("{primera}", primera.Prosa, StringComparison.Ordinal);
            }
            else
            {
                var primera = entidadesOrdenadas[0];
                var segunda = entidadesOrdenadas[1];
                var plantilla = PlantillasProsa.ElegirVariante(plantillas.Huella.VariasEntidades, dia, $"huella:2:{primera.Ref}:{segunda.Ref}");
                prosa = plantilla
                    .Replace("{tiempo}", tiempo, StringComparison.Ordinal)
                    .Replace("{primera}", primera.Prosa, StringComparison.Ordinal)
                    .Replace("{segunda}", segunda.Prosa, StringComparison.Ordinal);
            }

            return new Huella
            {
                DesdeUltimoPensamientoMinJuego = minutos,
                Prosa = prosa,
                Entidades = entidadesOrdenadas.Select(e => new AtencionEntidad { Ref = e.Ref, Atencion = e.Atencion }).ToList(),
            };
        }

        public static string FormatearTiempo(int minutos)
        {
            if (minutos <= 0)
            {
                return "un instante";
            }

            if (minutos == 1)
            {
                return "un minuto";
            }

            if (minutos < 60)
            {
                return $"{minutos} minutos";
            }

            var horas = minutos / 60;
            var resto = minutos % 60;
            var textoHoras = horas == 1 ? "una hora" : $"{horas} horas";
            return resto switch
            {
                0 => textoHoras,
                30 => $"{textoHoras} y media",
                1 => $"{textoHoras} y un minuto",
                _ => $"{textoHoras} y {resto} minutos",
            };
        }
    }
}
