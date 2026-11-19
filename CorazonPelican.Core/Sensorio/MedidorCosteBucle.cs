using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace CorazonPelican.Core.Sensorio
{
    /// <summary>Resumen acumulado del coste en milisegundos de un paso del bucle del motor (ADS §4).</summary>
    public sealed record MedicionPaso(
        string Paso,
        long Muestras,
        double UltimoMs,
        double MedioMs,
        double MaximoMs,
        double MinimoMs,
        double TotalMs);

    /// <summary>
    /// Contador de coste por paso del bucle del motor (ADS §4, CP-101).
    /// Permite medir en vivo el tiempo de ejecución de cada paso (<c>observar</c>, <c>saliencia</c>, etc.)
    /// sin asignaciones en la ruta caliente usando <see cref="Medir"/> o <see cref="RegistrarTicks"/>.
    /// </summary>
    public sealed class MedidorCosteBucle
    {
        private readonly object bloqueo = new();
        private readonly List<string> orden = new();
        private readonly Dictionary<string, Acumulador> pasos = new(StringComparer.Ordinal);

        public AlcanceMedicion Medir(string paso)
            => new(this, paso, Stopwatch.GetTimestamp());

        public void RegistrarTicks(string paso, long ticksTranscurridos)
        {
            var ms = ticksTranscurridos <= 0
                ? 0.0
                : ticksTranscurridos * 1000.0 / Stopwatch.Frequency;
            this.Registrar(paso, ms);
        }

        public void Registrar(string paso, double milisegundos)
        {
            if (string.IsNullOrWhiteSpace(paso))
            {
                throw new ArgumentException("El nombre del paso no puede estar vacío.", nameof(paso));
            }

            var valor = milisegundos < 0 ? 0.0 : milisegundos;
            lock (this.bloqueo)
            {
                if (!this.pasos.TryGetValue(paso, out var acc))
                {
                    acc = new Acumulador(paso);
                    this.pasos[paso] = acc;
                    this.orden.Add(paso);
                }

                acc.Agregar(valor);
            }
        }

        public MedicionPaso? Obtener(string paso)
        {
            lock (this.bloqueo)
            {
                return this.pasos.TryGetValue(paso, out var acc) ? acc.Instantanea() : null;
            }
        }

        public IReadOnlyList<MedicionPaso> Resumen()
        {
            lock (this.bloqueo)
            {
                var lista = new List<MedicionPaso>(this.orden.Count);
                foreach (var nombre in this.orden)
                {
                    lista.Add(this.pasos[nombre].Instantanea());
                }

                return lista;
            }
        }

        public void Reiniciar()
        {
            lock (this.bloqueo)
            {
                this.pasos.Clear();
                this.orden.Clear();
            }
        }

        public readonly struct AlcanceMedicion : IDisposable
        {
            private readonly MedidorCosteBucle medidor;
            private readonly string paso;
            private readonly long inicio;

            internal AlcanceMedicion(MedidorCosteBucle medidor, string paso, long inicio)
            {
                this.medidor = medidor;
                this.paso = paso;
                this.inicio = inicio;
            }

            public void Dispose()
                => this.medidor.RegistrarTicks(this.paso, Stopwatch.GetTimestamp() - this.inicio);
        }

        private sealed class Acumulador
        {
            private readonly string paso;
            private long muestras;
            private double ultimoMs;
            private double totalMs;
            private double maximoMs;
            private double minimoMs = double.MaxValue;

            public Acumulador(string paso)
            {
                this.paso = paso;
            }

            public void Agregar(double ms)
            {
                this.muestras++;
                this.ultimoMs = ms;
                this.totalMs += ms;
                if (ms > this.maximoMs)
                {
                    this.maximoMs = ms;
                }

                if (ms < this.minimoMs)
                {
                    this.minimoMs = ms;
                }
            }

            public MedicionPaso Instantanea()
                => new(
                    this.paso,
                    this.muestras,
                    this.ultimoMs,
                    this.muestras == 0 ? 0.0 : this.totalMs / this.muestras,
                    this.maximoMs,
                    this.muestras == 0 ? 0.0 : this.minimoMs,
                    this.totalMs);
        }
    }
}
