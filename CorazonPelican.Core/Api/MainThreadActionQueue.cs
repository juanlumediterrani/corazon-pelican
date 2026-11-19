using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace CorazonPelican.Core.Api
{
    /// <summary>Resultado de ejecutar una acción en el hilo principal del juego.</summary>
    public sealed record ResultadoEjecucionHiloPrincipal<T>(T Valor, int IdHiloEjecucion);

    /// <summary>
    /// Cola segura entre hilos para despachar acciones desde los hilos HTTP del <c>ThreadPool</c>
    /// al hilo principal del bucle de juego (<c>UpdateTicked</c>) (ADS §1, R-05, CP-203).
    /// </summary>
    public sealed class MainThreadActionQueue
    {
        private sealed class ElementoCola
        {
            public ElementoCola(Action ejecutar, Action<Exception> fallar, Func<bool> estaAbandonada)
            {
                this.Ejecutar = ejecutar;
                this.Fallar = fallar;
                this.EstaAbandonada = estaAbandonada;
            }

            public Action Ejecutar { get; }
            public Action<Exception> Fallar { get; }

            /// <summary>Quien encoló la petición ya agotó el tiempo o canceló: no debe ejecutarse.</summary>
            public Func<bool> EstaAbandonada { get; }
        }

        private readonly ConcurrentQueue<ElementoCola> cola = new();
        private long totalEjecutadas;
        private long totalAbandonadas;
        private int ultimoHiloDrenado;

        /// <summary>Número aproximado de acciones pendientes en la cola.</summary>
        public int Pendientes => this.cola.Count;

        /// <summary>Total histórico de acciones drenadas y ejecutadas.</summary>
        public long TotalEjecutadas => Interlocked.Read(ref this.totalEjecutadas);

        /// <summary>Acciones descartadas sin ejecutar porque su petición ya había agotado el tiempo o cancelado.</summary>
        public long TotalAbandonadas => Interlocked.Read(ref this.totalAbandonadas);

        /// <summary>ID del último hilo que drenó la cola (hilo principal del juego).</summary>
        public int UltimoHiloDrenado => Volatile.Read(ref this.ultimoHiloDrenado);

        /// <summary>
        /// Encola una función para que se ejecute en el próximo tick del hilo principal y devuelve su resultado
        /// junto con el identificador del hilo que la ejecutó.
        /// </summary>
        public async Task<ResultadoEjecucionHiloPrincipal<T>> EncolarConInfoHiloAsync<T>(
            Func<T> accion,
            TimeSpan? tiempoLimite = null,
            CancellationToken tokenCancelacion = default)
        {
            if (accion is null)
            {
                throw new ArgumentNullException(nameof(accion));
            }

            var tcs = new TaskCompletionSource<ResultadoEjecucionHiloPrincipal<T>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            if (tokenCancelacion.IsCancellationRequested)
            {
                tcs.TrySetCanceled(tokenCancelacion);
                return await tcs.Task.ConfigureAwait(false);
            }

            this.cola.Enqueue(new ElementoCola(
                ejecutar: () =>
                {
                    try
                    {
                        var idHilo = Environment.CurrentManagedThreadId;
                        var valor = accion();
                        tcs.TrySetResult(new ResultadoEjecucionHiloPrincipal<T>(valor, idHilo));
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                },
                fallar: ex => tcs.TrySetException(ex),
                estaAbandonada: () => tcs.Task.IsCompleted));

            var limiteEfectivo = tiempoLimite ?? TimeSpan.FromSeconds(10);
            using var ctsTiempo = new CancellationTokenSource(limiteEfectivo);
            using var regTiempo = ctsTiempo.Token.Register(() =>
                tcs.TrySetException(new TimeoutException(
                    $"La acción en el hilo principal superó el tiempo límite ({limiteEfectivo.TotalSeconds:0.#}s).")));
            using var regCancelacion = tokenCancelacion.CanBeCanceled
                ? tokenCancelacion.Register(() => tcs.TrySetCanceled(tokenCancelacion))
                : default;

            return await tcs.Task.ConfigureAwait(false);
        }

        /// <summary>Encola una función para ejecutarse en el próximo tick del hilo principal y devuelve su valor.</summary>
        public async Task<T> EncolarAsync<T>(
            Func<T> accion,
            TimeSpan? tiempoLimite = null,
            CancellationToken tokenCancelacion = default)
        {
            var resultado = await this.EncolarConInfoHiloAsync(accion, tiempoLimite, tokenCancelacion).ConfigureAwait(false);
            return resultado.Valor;
        }

        /// <summary>Encola una acción sin valor de retorno para ejecutarse en el próximo tick del hilo principal.</summary>
        public Task EncolarAsync(
            Action accion,
            TimeSpan? tiempoLimite = null,
            CancellationToken tokenCancelacion = default)
        {
            if (accion is null)
            {
                throw new ArgumentNullException(nameof(accion));
            }

            return this.EncolarAsync(() =>
            {
                accion();
                return true;
            }, tiempoLimite, tokenCancelacion);
        }

        /// <summary>
        /// Drena y ejecuta hasta <paramref name="maxAcciones"/> acciones en el hilo llamante
        /// (debe invocarse desde <c>UpdateTicked</c>).
        /// </summary>
        public int Drenar(int maxAcciones = 64)
        {
            if (maxAcciones <= 0)
            {
                return 0;
            }

            Volatile.Write(ref this.ultimoHiloDrenado, Environment.CurrentManagedThreadId);

            var ejecutadas = 0;
            while (ejecutadas < maxAcciones && this.cola.TryDequeue(out var elemento))
            {
                if (elemento.EstaAbandonada())
                {
                    Interlocked.Increment(ref this.totalAbandonadas);
                    continue;
                }

                elemento.Ejecutar();
                ejecutadas++;
                Interlocked.Increment(ref this.totalEjecutadas);
            }

            return ejecutadas;
        }

        /// <summary>Cancela todas las acciones pendientes (p. ej. al volver al título o detener el servidor).</summary>
        public void LimpiarPendientes(string motivo = "La cola de acciones del motor se ha detenido.")
        {
            var ex = new OperationCanceledException(motivo);
            while (this.cola.TryDequeue(out var elemento))
            {
                elemento.Fallar(ex);
            }
        }
    }
}
