using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Sensorio.Prosa;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Atencion
{
    /// <summary>Procedencia de una entrada admitida en el búfer del personaje.</summary>
    public enum OrigenBufer
    {
        Percepcion,
        Accion,
    }

    /// <summary>Entrada original registrada en el búfer antes o después de la consolidación temporal.</summary>
    public sealed record EntradaOriginalBufer(
        int Dia,
        int Hora,
        string Prosa,
        double Saliencia,
        OrigenBufer Origen,
        IReadOnlyList<string> Entidades);

    /// <summary>
    /// Búfer de prosa de acción y percepciones del personaje (GSS §4.4, P-8, P-10).
    /// Solo admite percepciones desde <see cref="Banda.Mencion"/> o acciones propias del personaje, y
    /// aplica consolidación temporal (fóvea/periferia en el tiempo) sin resumidor LLM ni invención de texto.
    /// </summary>
    public sealed class Bufer
    {
        public const int MaxEntradasPorDefecto = 12;
        public const int VentanaRecientePorDefectoMin = 60;
        public const double SemividaAntiguasPorDefectoMin = 60.0;

        private readonly List<ItemBufer> entradas = new();
        private long siguienteOrden;

        public Bufer(
            int maxEntradas = MaxEntradasPorDefecto,
            int ventanaRecienteMinJuego = VentanaRecientePorDefectoMin,
            double semividaAntiguasMinJuego = SemividaAntiguasPorDefectoMin)
        {
            if (maxEntradas <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxEntradas), "El tope de entradas debe ser mayor que 0.");
            }

            if (ventanaRecienteMinJuego < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ventanaRecienteMinJuego), "La ventana reciente no puede ser negativa.");
            }

            if (semividaAntiguasMinJuego <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(semividaAntiguasMinJuego), "La semivida debe ser mayor que 0.");
            }

            this.MaxEntradas = maxEntradas;
            this.VentanaRecienteMinJuego = ventanaRecienteMinJuego;
            this.SemividaAntiguasMinJuego = semividaAntiguasMinJuego;
        }

        public int MaxEntradas { get; }

        public int VentanaRecienteMinJuego { get; }

        public double SemividaAntiguasMinJuego { get; }

        public IReadOnlyList<EntradaOriginalBufer> Entradas => this.entradas.Select(x => x.Entrada).ToList();

        /// <summary>
        /// Registra una percepción en el búfer solo si alcanza al menos <see cref="Banda.Mencion"/>.
        /// Conserva literalmente <paramref name="prosa"/> sin reescribirla.
        /// </summary>
        public bool RegistrarPercepcion(
            int dia,
            int hora,
            Banda banda,
            double saliencia,
            string prosa,
            string? entidadRef = null)
        {
            if (banda < Banda.Mencion || string.IsNullOrWhiteSpace(prosa))
            {
                return false;
            }

            ValidarEntrada(dia, hora, saliencia);
            IReadOnlyList<string> entidades = string.IsNullOrWhiteSpace(entidadRef)
                ? Array.Empty<string>()
                : new[] { entidadRef };

            this.entradas.Add(new ItemBufer(
                new EntradaOriginalBufer(dia, hora, prosa, saliencia, OrigenBufer.Percepcion, entidades),
                this.siguienteOrden++));
            return true;
        }

        /// <summary>
        /// Genera y registra la prosa breve de una entidad percibida si alcanza <see cref="Banda.Mencion"/>.
        /// </summary>
        public bool RegistrarPercepcion(
            EntidadObservada entidad,
            Banda banda,
            double saliencia,
            int dia,
            int hora,
            PlantillasProsa plantillas)
        {
            if (banda < Banda.Mencion)
            {
                return false;
            }

            var prosa = GeneradorAccion.GenerarPercepcion(entidad, banda, dia, plantillas);
            if (string.IsNullOrWhiteSpace(prosa))
            {
                return false;
            }

            return this.RegistrarPercepcion(dia, hora, banda, saliencia, prosa, entidad.Ref);
        }

        /// <summary>
        /// Registra una acción ejecutada por el propio personaje (entra siempre, independientemente de bandas).
        /// </summary>
        public bool RegistrarAccion(
            int dia,
            int hora,
            string prosa,
            double saliencia = 0.8,
            IReadOnlyList<string>? entidades = null)
        {
            if (string.IsNullOrWhiteSpace(prosa))
            {
                return false;
            }

            ValidarEntrada(dia, hora, saliencia);
            var listaEntidades = entidades is null || entidades.Count == 0
                ? Array.Empty<string>()
                : entidades.Where(e => !string.IsNullOrWhiteSpace(e)).ToArray();

            this.entradas.Add(new ItemBufer(
                new EntradaOriginalBufer(dia, hora, prosa, saliencia, OrigenBufer.Accion, listaEntidades),
                this.siguienteOrden++));
            return true;
        }

        /// <summary>
        /// Genera con las plantillas del perfil y registra una acción del vocabulario ejecutada por el personaje.
        /// </summary>
        public bool RegistrarAccion(
            PasoPlan paso,
            int dia,
            int hora,
            PlantillasProsa plantillas,
            double saliencia = 0.8,
            LugaresOfrecidos? lugares = null,
            IReadOnlyDictionary<string, string>? nombresEntidades = null)
        {
            var prosa = GeneradorAccion.GenerarAccion(paso, dia, plantillas, lugares, nombresEntidades);
            IReadOnlyList<string>? entidades = string.IsNullOrWhiteSpace(paso.Objetivo)
                ? null
                : new[] { paso.Objetivo };
            return this.RegistrarAccion(dia, hora, prosa, saliencia, entidades);
        }

        /// <summary>
        /// Proyecta el búfer consolidado hacia <see cref="EntradaBufer"/> sin modificar el estado interno.
        /// Nunca supera <see cref="MaxEntradas"/> y conserva literalmente la prosa original.
        /// </summary>
        public List<EntradaBufer> Proyectar(int diaActual, int horaActual)
            => this.SeleccionarConsolidadas(diaActual, horaActual)
                .Select(x => new EntradaBufer
                {
                    Hora = x.Entrada.Hora,
                    Prosa = x.Entrada.Prosa,
                    Saliencia = x.Entrada.Saliencia,
                })
                .ToList();

        /// <summary>
        /// Consolida el búfer en sitio (fóvea para la última ventana reciente y periferia con fuga para
        /// lo anterior) y devuelve su proyección para el pulso.
        /// </summary>
        public IReadOnlyList<EntradaBufer> Consolidar(int diaActual, int horaActual)
        {
            var seleccion = this.SeleccionarConsolidadas(diaActual, horaActual);
            this.entradas.Clear();
            this.entradas.AddRange(seleccion);
            return seleccion
                .Select(x => new EntradaBufer
                {
                    Hora = x.Entrada.Hora,
                    Prosa = x.Entrada.Prosa,
                    Saliencia = x.Entrada.Saliencia,
                })
                .ToList();
        }

        public void Limpiar() => this.entradas.Clear();

        private List<ItemBufer> SeleccionarConsolidadas(int diaActual, int horaActual)
        {
            if (this.entradas.Count == 0)
            {
                return new List<ItemBufer>();
            }

            var recientes = new List<ItemBufer>();
            var antiguas = new List<(ItemBufer Item, int EdadMin)>();

            foreach (var item in this.entradas)
            {
                var edad = MinutosEntre(item.Entrada.Dia, item.Entrada.Hora, diaActual, horaActual);
                if (edad <= this.VentanaRecienteMinJuego)
                {
                    recientes.Add(item);
                }
                else
                {
                    antiguas.Add((item, edad));
                }
            }

            var seleccionadas = recientes
                .OrderByDescending(x => x.Entrada.Saliencia)
                .ThenByDescending(x => x.Orden)
                .Take(this.MaxEntradas)
                .ToList();

            var huecos = this.MaxEntradas - seleccionadas.Count;
            if (huecos > 0 && antiguas.Count > 0)
            {
                var tau = this.SemividaAntiguasMinJuego / Math.Log(2);
                seleccionadas.AddRange(antiguas
                    .OrderByDescending(x => x.Item.Entrada.Saliencia * Math.Exp(-x.EdadMin / tau))
                    .ThenByDescending(x => x.Item.Orden)
                    .Take(huecos)
                    .Select(x => x.Item));
            }

            return seleccionadas
                .OrderBy(x => x.Entrada.Dia)
                .ThenBy(x => x.Entrada.Hora)
                .ThenBy(x => x.Orden)
                .ToList();
        }

        private static int MinutosEntre(int diaDesde, int horaDesde, int diaHasta, int horaHasta)
        {
            var desde = ((diaDesde - 1) * 1440) + HoraJuego.MinutosDesdeInicio(horaDesde);
            var hasta = ((diaHasta - 1) * 1440) + HoraJuego.MinutosDesdeInicio(horaHasta);
            return Math.Max(0, hasta - desde);
        }

        private static void ValidarEntrada(int dia, int hora, double saliencia)
        {
            if (dia < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(dia), "El día de juego debe ser ≥ 1.");
            }

            if (hora is < HoraJuego.InicioDia or > HoraJuego.FinDia)
            {
                throw new ArgumentOutOfRangeException(nameof(hora), $"La hora debe estar entre {HoraJuego.InicioDia} y {HoraJuego.FinDia}.");
            }

            if (saliencia < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(saliencia), "La saliencia no puede ser negativa.");
            }
        }

        private sealed record ItemBufer(EntradaOriginalBufer Entrada, long Orden);
    }
}
