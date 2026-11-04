using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>Cuánto detalle recibe una entidad en la prosa (GSS §3.3). Ordenadas de menos a más.</summary>
    public enum Banda
    {
        /// <summary>No aparece: para el personaje no existe (R-03).</summary>
        Ausente,

        /// <summary>Vaguedad deliberada: «alguien al fondo».</summary>
        Periferia,

        /// <summary>Una referencia breve.</summary>
        Mencion,

        /// <summary>Descripción rica.</summary>
        Foco,
    }

    /// <summary>Una entidad con su banda y la saliencia que la decidió.</summary>
    public sealed record EntidadEnBanda(string Ref, Banda Banda, double Saliencia);

    /// <summary>
    /// Asigna bandas con los umbrales del perfil (GSS §3.3). Con histéresis (<c>umbrales.histeresis</c>
    /// &gt; 0), una entidad solo cambia de banda si cruza el umbral con ese margen, para que no
    /// parpadee entre observaciones. Recuerda la última banda de cada entidad presente; la que
    /// desaparece de la observación se olvida y, si vuelve, empieza de cero.
    /// </summary>
    public sealed class ClasificadorBandas
    {
        private readonly Umbrales umbrales;
        private readonly Dictionary<string, Banda> ultimas = new();

        public ClasificadorBandas(Umbrales umbrales)
        {
            this.umbrales = umbrales;
        }

        /// <summary>Banda sin memoria: la que corresponde a la saliencia tal cual.</summary>
        public static Banda Cruda(double saliencia, Umbrales umbrales)
        {
            if (saliencia >= umbrales.Foco)
            {
                return Banda.Foco;
            }

            if (saliencia >= umbrales.Mencion)
            {
                return Banda.Mencion;
            }

            return saliencia >= umbrales.Periferia ? Banda.Periferia : Banda.Ausente;
        }

        /// <summary>Solo las entidades que llegan a la escena: cualquier banda menos <see cref="Banda.Ausente"/> (R-03).</summary>
        public static IReadOnlyList<EntidadEnBanda> EnEscena(IEnumerable<EntidadEnBanda> entidades)
            => entidades.Where(e => e.Banda != Banda.Ausente).ToList();

        /// <summary>Clasifica una observación entera y olvida las entidades que ya no están.</summary>
        public IReadOnlyList<EntidadEnBanda> Clasificar(IEnumerable<DesgloseSaliencia> saliencias)
        {
            var lista = saliencias.ToList();
            var presentes = new HashSet<string>(lista.Select(s => s.Ref));
            foreach (var olvidada in this.ultimas.Keys.Where(r => !presentes.Contains(r)).ToList())
            {
                this.ultimas.Remove(olvidada);
            }

            return lista.Select(s => new EntidadEnBanda(s.Ref, this.Clasificar(s.Ref, s.Total), s.Total)).ToList();
        }

        public Banda Clasificar(string referencia, double saliencia)
        {
            var cruda = Cruda(saliencia, this.umbrales);
            var margen = this.umbrales.Histeresis;
            if (margen <= 0 || !this.ultimas.TryGetValue(referencia, out var anterior))
            {
                this.ultimas[referencia] = cruda;
                return cruda;
            }

            var nueva = anterior;
            if (cruda > anterior)
            {
                var conMargen = Cruda(saliencia - margen, this.umbrales);
                nueva = conMargen > anterior ? conMargen : anterior;
            }
            else if (cruda < anterior)
            {
                var conMargen = Cruda(saliencia + margen, this.umbrales);
                nueva = conMargen < anterior ? conMargen : anterior;
            }

            this.ultimas[referencia] = nueva;
            return nueva;
        }
    }
}
