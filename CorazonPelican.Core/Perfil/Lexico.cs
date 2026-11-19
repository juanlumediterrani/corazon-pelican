using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Perfil
{
    /// <summary>Lugares del perfil que comparten la escena, las intenciones y los actos (R-14).</summary>
    [DataContract]
    public sealed class Lexico
    {
        [DataMember(Name = "lugares")] public List<Lugar> Lugares { get; set; } = new();

        /// <summary>Selecciona una vez los lugares disponibles para el pulso y su respuesta.</summary>
        public LugaresOfrecidos OfrecerLugares(Func<Lugar, bool> disponible)
            => new(this.Lugares.Where(disponible));
    }

    /// <summary>Selección común al apéndice y a la resolución de destinos de una misma escena.</summary>
    public sealed class LugaresOfrecidos
    {
        private readonly List<Lugar> lugares;

        internal LugaresOfrecidos(IEnumerable<Lugar> lugares)
        {
            this.lugares = lugares.ToList();
        }

        /// <summary>Resuelve el id exacto a la prosa y al área que el perfil asigna al lugar.</summary>
        public Lugar? ResolverLugar(string? id)
            => this.lugares.Find(lugar => string.Equals(lugar.Id, id, StringComparison.Ordinal));

        /// <summary>Proyecta al pulso solo los lugares ofrecidos en esta escena, en orden del perfil.</summary>
        public List<ParIdProsa> CrearApendice()
            => this.lugares.Select(lugar => new ParIdProsa { Id = lugar.Id, Prosa = lugar.Prosa }).ToList();

        /// <summary>Resuelve el nombre del destino. Su disponibilidad física se comprueba al actuar.</summary>
        public Lugar? DestinoDe(PasoPlan paso)
            => paso.Accion is "ir_a" or "sentarse" ? this.ResolverLugar(paso.Lugar) : null;

        /// <summary>Resuelve el lugar de una cita. El plazo y el destinatario se validan con el acto.</summary>
        public Lugar? DestinoDe(Acto acto)
            => acto.Tipo == "cita" ? this.ResolverLugar(acto.Lugar) : null;
    }
}
