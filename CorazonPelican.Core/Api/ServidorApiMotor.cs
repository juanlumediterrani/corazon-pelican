using System;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Api
{
    /// <summary>
    /// Servidor HTTP loopback del motor (<c>http://127.0.0.1:{puerto}/api/v1/</c>, ADS §5, R-05, CP-203).
    /// Escucha exclusivamente en loopback, valida Bearer token opcional y nunca bloquea el hilo principal del juego.
    /// </summary>
    public sealed class ServidorApiMotor : IDisposable
    {
        public static readonly string RutaBase = $"/api/{CorazonCore.VersionContrato}";

        private static readonly JsonSerializerOptions JsonOpciones = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private readonly ApiMotor motor;
        private readonly string? token;
        private readonly Action<string>? log;
        private readonly object sync = new();

        private HttpListener? listener;
        private Thread? hiloEscucha;
        private CancellationTokenSource? shutdownCts;
        private volatile bool estaEscuchando;

        public ServidorApiMotor(
            ApiMotor motor,
            int puerto = 25560,
            string? token = null,
            string host = "127.0.0.1",
            Action<string>? log = null)
        {
            this.motor = motor ?? throw new ArgumentNullException(nameof(motor));
            this.Puerto = puerto;
            this.token = string.IsNullOrWhiteSpace(token) ? null : token.Trim();
            this.log = log;
            this.Prefijo = ConstruirPrefijoLoopback(puerto, host);
        }

        public int Puerto { get; }

        public string Prefijo { get; }

        public bool EstaEscuchando => this.estaEscuchando;

        public bool RequiereToken => !string.IsNullOrEmpty(this.token);

        /// <summary>
        /// Construye el prefijo de <see cref="HttpListener"/> exigiendo estrictamente una dirección loopback
        /// (rechaza <c>0.0.0.0</c>, <c>*</c>, <c>+</c>, IPv6 cualquiera e IPs o nombres externos).
        /// </summary>
        public static string ConstruirPrefijoLoopback(int puerto, string host = "127.0.0.1")
        {
            if (puerto is < 1 or > 65535)
            {
                throw new ArgumentOutOfRangeException(nameof(puerto), puerto, "El puerto debe estar entre 1 y 65535.");
            }

            if (string.IsNullOrWhiteSpace(host))
            {
                throw new ArgumentException("El host de escucha no puede estar vacío.", nameof(host));
            }

            var limpio = host.Trim();
            if (limpio.Contains('*') || limpio.Contains('+'))
            {
                throw new ArgumentException($"Host no permitido para escucha loopback: «{host}».", nameof(host));
            }

            var sinCorchetes = limpio.StartsWith("[", StringComparison.Ordinal) && limpio.EndsWith("]", StringComparison.Ordinal)
                ? limpio[1..^1]
                : limpio;

            if (!IPAddress.TryParse(sinCorchetes, out var ip) || !EsDireccionLoopback(ip))
            {
                throw new ArgumentException(
                    $"La API del motor solo puede escuchar en loopback (p. ej. 127.0.0.1); host rechazado: «{host}».",
                    nameof(host));
            }

            var prefijo = ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6
                ? $"http://[{ip}]:{puerto}{RutaBase}/"
                : $"http://{ip}:{puerto}{RutaBase}/";

            ValidarPrefijoLoopback(prefijo);
            return prefijo;
        }

        /// <summary>Valida que un prefijo de listener sea HTTP y apunte estrictamente a una dirección loopback.</summary>
        public static void ValidarPrefijoLoopback(string prefijo)
        {
            if (string.IsNullOrWhiteSpace(prefijo))
            {
                throw new ArgumentException("El prefijo no puede estar vacío.", nameof(prefijo));
            }

            if (prefijo.Contains('*') || prefijo.Contains('+'))
            {
                throw new ArgumentException($"Prefijo comodín rechazado: «{prefijo}».", nameof(prefijo));
            }

            if (!Uri.TryCreate(prefijo, UriKind.Absolute, out var uri) ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                throw new ArgumentException($"Prefijo HTTP inválido: «{prefijo}».", nameof(prefijo));
            }

            var host = uri.Host;
            var sinCorchetes = host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal)
                ? host[1..^1]
                : host;

            if (!IPAddress.TryParse(sinCorchetes, out var ip) || !EsDireccionLoopback(ip))
            {
                throw new ArgumentException($"Prefijo no loopback rechazado: «{prefijo}».", nameof(prefijo));
            }
        }

        /// <summary>Comprueba si una dirección IP pertenece a loopback (<c>127.0.0.0/8</c> o <c>::1</c>).</summary>
        public static bool EsDireccionLoopback(IPAddress? direccion)
        {
            if (direccion is null)
            {
                return false;
            }

            if (direccion.IsIPv4MappedToIPv6)
            {
                direccion = direccion.MapToIPv4();
            }

            return IPAddress.IsLoopback(direccion);
        }

        /// <summary>Valida que la IP remota, la cabecera <c>Host</c> y la cabecera <c>Origin</c> (si existen) sean loopback.</summary>
        public static bool EsPeticionLoopbackValida(IPAddress? ipRemota, string? cabeceraHost, string? cabeceraOrigin)
        {
            if (!EsDireccionLoopback(ipRemota))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(cabeceraHost))
            {
                if (!Uri.TryCreate($"http://{cabeceraHost.Trim()}/", UriKind.Absolute, out var hostUri) ||
                    !EsHostCabeceraLoopback(hostUri.Host))
                {
                    return false;
                }
            }

            if (!string.IsNullOrWhiteSpace(cabeceraOrigin))
            {
                if (!Uri.TryCreate(cabeceraOrigin.Trim(), UriKind.Absolute, out var originUri) ||
                    !EsHostCabeceraLoopback(originUri.Host))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Valida el token Bearer opcional. Si <paramref name="tokenConfigurado"/> está vacío, el acceso es libre.</summary>
        public static bool EsTokenAutorizado(string? cabeceraAuthorization, string? tokenConfigurado)
        {
            if (string.IsNullOrWhiteSpace(tokenConfigurado))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(cabeceraAuthorization))
            {
                return false;
            }

            var recortada = cabeceraAuthorization.Trim();
            if (!recortada.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var recibido = recortada[7..].Trim();
            var bytesRecibido = Encoding.UTF8.GetBytes(recibido);
            var bytesEsperado = Encoding.UTF8.GetBytes(tokenConfigurado.Trim());
            return bytesRecibido.Length == bytesEsperado.Length &&
                   CryptographicOperations.FixedTimeEquals(bytesRecibido, bytesEsperado);
        }

        /// <summary>Arranca el <see cref="HttpListener"/> en loopback si no estaba ya escuchando.</summary>
        public void Iniciar()
        {
            lock (this.sync)
            {
                if (this.estaEscuchando)
                {
                    return;
                }

                ValidarPrefijoLoopback(this.Prefijo);

                var nuevoListener = new HttpListener();
                nuevoListener.Prefixes.Add(this.Prefijo);
                nuevoListener.Start();

                this.listener = nuevoListener;
                this.shutdownCts = new CancellationTokenSource();
                this.estaEscuchando = true;

                this.hiloEscucha = new Thread(this.BucleEscucha)
                {
                    IsBackground = true,
                    Name = $"CorazonPelican-Api-{this.Puerto}",
                };
                this.hiloEscucha.Start();
            }
        }

        /// <summary>Detiene el servidor HTTP y cancela las esperas long-poll en curso.</summary>
        public void Detener()
        {
            HttpListener? listenerActual;
            Thread? hiloActual;
            CancellationTokenSource? ctsActual;

            lock (this.sync)
            {
                if (!this.estaEscuchando && this.listener is null)
                {
                    return;
                }

                this.estaEscuchando = false;
                listenerActual = this.listener;
                hiloActual = this.hiloEscucha;
                ctsActual = this.shutdownCts;
                this.listener = null;
                this.hiloEscucha = null;
                this.shutdownCts = null;
            }

            try
            {
                ctsActual?.Cancel();
            }
            catch
            {
            }

            try
            {
                listenerActual?.Stop();
                listenerActual?.Close();
            }
            catch
            {
            }

            if (hiloActual is not null && hiloActual != Thread.CurrentThread)
            {
                hiloActual.Join(TimeSpan.FromMilliseconds(500));
            }

            ctsActual?.Dispose();
        }

        public void Dispose() => this.Detener();

        private void BucleEscucha()
        {
            while (this.estaEscuchando)
            {
                var actual = this.listener;
                if (actual is null || !actual.IsListening)
                {
                    break;
                }

                try
                {
                    var httpCtx = actual.GetContext();
                    _ = Task.Run(() => this.ProcesarPeticionSeguraAsync(httpCtx));
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (this.estaEscuchando)
                    {
                        this.log?.Invoke($"Error en bucle de escucha HTTP: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
        }

        private async Task ProcesarPeticionSeguraAsync(HttpListenerContext httpCtx)
        {
            try
            {
                await this.ProcesarPeticionAsync(httpCtx).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await TryEscribirJsonAsync(httpCtx.Response, HttpStatusCode.ServiceUnavailable,
                    new ErrorApi { Error = "El servidor se está deteniendo." }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                this.log?.Invoke($"Petición {httpCtx.Request.HttpMethod} {httpCtx.Request.Url?.AbsolutePath} falló: {ex.GetType().Name}: {ex.Message}");
                await TryEscribirJsonAsync(httpCtx.Response, HttpStatusCode.InternalServerError,
                    new ErrorApi { Error = ex.Message }).ConfigureAwait(false);
            }
        }

        private async Task ProcesarPeticionAsync(HttpListenerContext httpCtx)
        {
            var req = httpCtx.Request;
            var res = httpCtx.Response;

            if (FaltaLongitudCuerpoRequerida(req.HttpMethod, req.Headers["Content-Length"], req.Headers["Transfer-Encoding"]))
            {
                await TryEscribirJsonAsync(res, HttpStatusCode.LengthRequired,
                    new ErrorApi { Error = "Las peticiones con cuerpo deben indicar Content-Length o Transfer-Encoding: chunked." }).ConfigureAwait(false);
                return;
            }

            if (!EsPeticionLoopbackValida(req.RemoteEndPoint?.Address, req.Headers["Host"], req.Headers["Origin"]))
            {
                await TryEscribirJsonAsync(res, HttpStatusCode.Forbidden,
                    new ErrorApi { Error = "Acceso denegado: solo se admiten peticiones desde loopback." }).ConfigureAwait(false);
                return;
            }

            if (!EsTokenAutorizado(req.Headers["Authorization"], this.token))
            {
                try
                {
                    res.Headers["WWW-Authenticate"] = "Bearer";
                }
                catch
                {
                }

                await TryEscribirJsonAsync(res, HttpStatusCode.Unauthorized,
                    new ErrorApi { Error = "No autorizado: se requiere un token Bearer válido." }).ConfigureAwait(false);
                return;
            }

            var metodo = (req.HttpMethod ?? "GET").ToUpperInvariant();
            var ruta = NormalizarRuta(req.Url?.AbsolutePath);
            var ct = this.shutdownCts?.Token ?? CancellationToken.None;

            if (ruta == $"{RutaBase}/salud")
            {
                if (metodo != "GET")
                {
                    await EscribirMetodoNoPermitidoAsync(res, "GET").ConfigureAwait(false);
                    return;
                }

                await TryEscribirJsonAsync(res, HttpStatusCode.OK, this.motor.ObtenerSalud()).ConfigureAwait(false);
                return;
            }

            if (ruta == $"{RutaBase}/pulsos")
            {
                if (metodo != "GET")
                {
                    await EscribirMetodoNoPermitidoAsync(res, "GET").ConfigureAwait(false);
                    return;
                }

                var epoca = req.QueryString["epoca"];
                long since = 0;
                if (!string.IsNullOrWhiteSpace(req.QueryString["since"]))
                {
                    long.TryParse(req.QueryString["since"], out since);
                }

                int esperaMs = 0;
                if (!string.IsNullOrWhiteSpace(req.QueryString["espera_ms"]))
                {
                    int.TryParse(req.QueryString["espera_ms"], out esperaMs);
                }

                var resultadoPulsos = await this.motor.EsperarPulsosAsync(epoca, since, esperaMs, ct).ConfigureAwait(false);
                if (resultadoPulsos.ConflictoEpoca)
                {
                    await TryEscribirJsonAsync(res, HttpStatusCode.Conflict, new ErrorApi
                    {
                        Error = resultadoPulsos.MundoCargado ? ValidadorEpoca.CodigoEpocaObsoleta : "mundo_no_cargado",
                        Epoca = resultadoPulsos.EpocaActual,
                        MundoCargado = resultadoPulsos.MundoCargado,
                    }).ConfigureAwait(false);
                    return;
                }

                await TryEscribirJsonAsync(res, HttpStatusCode.OK, new RespuestaPulsos
                {
                    Epoca = resultadoPulsos.EpocaActual ?? string.Empty,
                    Pulsos = new System.Collections.Generic.List<Pulso>(resultadoPulsos.Pulsos),
                }).ConfigureAwait(false);
                return;
            }

            if (ruta == $"{RutaBase}/respuestas")
            {
                if (metodo != "POST")
                {
                    await EscribirMetodoNoPermitidoAsync(res, "POST").ConfigureAwait(false);
                    return;
                }

                var (respuesta, huella, errorLectura) = await LeerJsonCuerpoAsync<Respuesta>(req, ct).ConfigureAwait(false);
                if (respuesta is null)
                {
                    await TryEscribirJsonAsync(res, HttpStatusCode.BadRequest,
                        new ErrorApi { Error = errorLectura ?? "Cuerpo JSON inválido para Respuesta." }).ConfigureAwait(false);
                    return;
                }

                try
                {
                    var resultado = await this.motor.ProcesarRespuestaAsync(respuesta, huella, tokenCancelacion: ct).ConfigureAwait(false);
                    await TryEscribirJsonAsync(res, HttpStatusCode.OK, resultado).ConfigureAwait(false);
                }
                catch (TimeoutException ex)
                {
                    await TryEscribirJsonAsync(res, HttpStatusCode.GatewayTimeout,
                        new ErrorApi { Error = ex.Message }).ConfigureAwait(false);
                }

                return;
            }

            if (ruta == $"{RutaBase}/eventos")
            {
                if (metodo != "GET")
                {
                    await EscribirMetodoNoPermitidoAsync(res, "GET").ConfigureAwait(false);
                    return;
                }

                long since = 0;
                if (!string.IsNullOrWhiteSpace(req.QueryString["since"]))
                {
                    long.TryParse(req.QueryString["since"], out since);
                }

                int limite = 500;
                if (!string.IsNullOrWhiteSpace(req.QueryString["limit"]) && int.TryParse(req.QueryString["limit"], out var parsedLimite))
                {
                    limite = parsedLimite;
                }

                await TryEscribirJsonAsync(res, HttpStatusCode.OK, this.motor.ObtenerEventos(since, limite)).ConfigureAwait(false);
                return;
            }

            if (IntentarExtraerPersonaje(ruta, $"{RutaBase}/memoria/", out var personajeMemoria))
            {
                if (metodo != "GET")
                {
                    await EscribirMetodoNoPermitidoAsync(res, "GET").ConfigureAwait(false);
                    return;
                }

                var consulta = await this.motor.ObtenerMemoriaAsync(personajeMemoria, ct).ConfigureAwait(false);
                await ResponderRecursoAsync(res, consulta, personajeMemoria).ConfigureAwait(false);
                return;
            }

            if (IntentarExtraerPersonaje(ruta, $"{RutaBase}/escena/", out var personajeEscena))
            {
                if (metodo != "GET")
                {
                    await EscribirMetodoNoPermitidoAsync(res, "GET").ConfigureAwait(false);
                    return;
                }

                var consulta = await this.motor.ObtenerEscenaAsync(personajeEscena, ct).ConfigureAwait(false);
                await ResponderRecursoAsync(res, consulta, personajeEscena).ConfigureAwait(false);
                return;
            }

            await TryEscribirJsonAsync(res, HttpStatusCode.NotFound,
                new ErrorApi { Error = $"Ruta no encontrada: {metodo} {ruta}" }).ConfigureAwait(false);
        }

        private static async Task ResponderRecursoAsync<T>(HttpListenerResponse res, ResultadoConsultaRecurso<T> consulta, string personaje)
        {
            switch (consulta.Estado)
            {
                case EstadoRecursoApi.Ok:
                    await TryEscribirJsonAsync(res, HttpStatusCode.OK, consulta.Valor!).ConfigureAwait(false);
                    break;
                case EstadoRecursoApi.MundoNoCargado:
                    await TryEscribirJsonAsync(res, HttpStatusCode.Conflict,
                        new ErrorApi { Error = "mundo_no_cargado", MundoCargado = false }).ConfigureAwait(false);
                    break;
                default:
                    await TryEscribirJsonAsync(res, HttpStatusCode.NotFound,
                        new ErrorApi { Error = $"Personaje desconocido: «{personaje}»." }).ConfigureAwait(false);
                    break;
            }
        }

        private static bool IntentarExtraerPersonaje(string ruta, string prefijoRuta, out string personaje)
        {
            personaje = string.Empty;
            if (!ruta.StartsWith(prefijoRuta, StringComparison.Ordinal))
            {
                return false;
            }

            var segmento = ruta[prefijoRuta.Length..];
            if (string.IsNullOrWhiteSpace(segmento) || segmento.Contains('/'))
            {
                return false;
            }

            personaje = Uri.UnescapeDataString(segmento);
            return !string.IsNullOrWhiteSpace(personaje) && !personaje.Contains('/') && !personaje.Contains("..", StringComparison.Ordinal);
        }

        private static bool EsHostCabeceraLoopback(string host)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var sinCorchetes = host.StartsWith("[", StringComparison.Ordinal) && host.EndsWith("]", StringComparison.Ordinal)
                ? host[1..^1]
                : host;

            return IPAddress.TryParse(sinCorchetes, out var ip) && EsDireccionLoopback(ip);
        }

        private static bool FaltaLongitudCuerpoRequerida(string? metodo, string? contentLength, string? transferEncoding)
        {
            var m = (metodo ?? string.Empty).Trim().ToUpperInvariant();
            if (m != "POST" && m != "PUT" && m != "PATCH")
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(contentLength))
            {
                return false;
            }

            return transferEncoding is null ||
                   transferEncoding.IndexOf("chunked", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static string NormalizarRuta(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return "/";
            }

            var trimmed = rawPath.Trim();
            return trimmed.Length > 1 ? trimmed.TrimEnd('/') : trimmed;
        }

        private static async Task<(T? Valor, string? Huella, string? Error)> LeerJsonCuerpoAsync<T>(
            HttpListenerRequest req,
            CancellationToken ct)
        {
            try
            {
                using var lector = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8);
                var texto = await lector.ReadToEndAsync().WaitAsync(ct).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(texto))
                {
                    return (default, null, "El cuerpo de la petición está vacío.");
                }

                var valor = JsonSerializer.Deserialize<T>(texto, JsonOpciones);
                if (valor is null)
                {
                    return (default, null, "El JSON deserializado es nulo.");
                }

                var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(texto)));
                return (valor, hash, null);
            }
            catch (JsonException ex)
            {
                return (default, null, $"JSON inválido: {ex.Message}");
            }
        }

        private static Task EscribirMetodoNoPermitidoAsync(HttpListenerResponse res, string permitido)
        {
            try
            {
                res.Headers["Allow"] = permitido;
            }
            catch
            {
            }

            return TryEscribirJsonAsync(res, HttpStatusCode.MethodNotAllowed,
                new ErrorApi { Error = $"Método HTTP no permitido; se esperaba {permitido}." });
        }

        private static async Task TryEscribirJsonAsync<T>(HttpListenerResponse res, HttpStatusCode estado, T carga)
        {
            try
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(carga, JsonOpciones);
                res.StatusCode = (int)estado;
                res.ContentType = "application/json; charset=utf-8";
                res.ContentLength64 = bytes.Length;
                await res.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
                res.Close();
            }
            catch (Exception ex) when (ex is ObjectDisposedException or HttpListenerException or InvalidOperationException or IOException)
            {
            }
        }
    }
}
