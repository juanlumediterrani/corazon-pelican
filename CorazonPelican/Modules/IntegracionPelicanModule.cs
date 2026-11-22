using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Framework;
using StardewModdingAPI;

namespace CorazonPelican.Modules
{
    /// <summary>
    /// Integración opcional con Pelican 4Ever (ADS §11.1, CP-602):
    /// consulta <c>GET /api/v1/host/events?since=N</c> en un hilo de fondo cuando
    /// <c>PelicanApiUrl</c> está configurado y los vuelca en la siguiente observación del hilo principal.
    /// </summary>
    internal sealed class IntegracionPelicanModule : IModule, IDisposable
    {
        private const int CadenciaSondeoMs = 2_000;

        private ModContext? context;
        private CancellationTokenSource? cts;
        private Task? workerTask;
        private HttpClient? httpClient;
        private long ultimoEventoId;

        public string Nombre => "IntegracionPelican";

        public LadoModulo Lado => LadoModulo.Anfitrion;

        public bool WorkerIniciado => this.workerTask is not null;

        public void Registrar(ModContext context)
        {
            this.context = context;
            var urlBase = context.Config.PelicanApiUrl;
            if (!ConversorSucesosPelican.EsConfigurado(urlBase))
            {
                // Sin Pelican configurado: no arranca ni registra errores (CP-602).
                return;
            }

            if (!Uri.TryCreate(urlBase.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uriBase))
            {
                return;
            }

            this.httpClient = new HttpClient
            {
                BaseAddress = uriBase,
                Timeout = TimeSpan.FromSeconds(5),
            };
            if (!string.IsNullOrWhiteSpace(context.Config.PelicanApiToken))
            {
                this.httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", context.Config.PelicanApiToken.Trim());
            }

            this.cts = new CancellationTokenSource();
            var token = this.cts.Token;
            this.workerTask = Task.Run(() => this.BucleSondeoAsync(token), token);
            context.Monitor.Log($"[IntegracionPelican] Sondeo en segundo plano iniciado sobre {uriBase}.", LogLevel.Trace);
        }

        private async Task BucleSondeoAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    if (this.httpClient is not null && this.context is not null)
                    {
                        var ruta = $"api/v1/host/events?since={this.ultimoEventoId}";
                        using var respuesta = await this.httpClient.GetAsync(ruta, ct).ConfigureAwait(false);
                        if (respuesta.IsSuccessStatusCode)
                        {
                            var json = await respuesta.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                            var eventos = ConversorSucesosPelican.ParsearRespuestaJson(json, out var ultimoId);
                            if (ultimoId > this.ultimoEventoId)
                            {
                                this.ultimoEventoId = ultimoId;
                            }

                            foreach (var ev in eventos)
                            {
                                this.context.SucesosPelican.Encolar(ev);
                                if (ev.Id > this.ultimoEventoId)
                                {
                                    this.ultimoEventoId = ev.Id;
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                    // Pelican puede no estar arrancado todavía; reintenta en segundo plano sin saturar el log.
                }

                try
                {
                    await Task.Delay(CadenciaSondeoMs, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                this.cts?.Cancel();
            }
            catch
            {
            }

            this.httpClient?.Dispose();
            this.cts?.Dispose();
            this.httpClient = null;
            this.cts = null;
            this.workerTask = null;
        }
    }
}
