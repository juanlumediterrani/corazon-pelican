using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using CorazonPelican.Core.Atencion;
using CorazonPelican.Core.Contrato;

namespace CorazonPelican.Core.Api
{
    /// <summary>Resultado de consultar pulsos por long-poll (<c>GET /api/v1/pulsos</c>).</summary>
    public sealed class ResultadoConsultaPulsos
    {
        private ResultadoConsultaPulsos(bool conflictoEpoca, string? epocaActual, bool mundoCargado, IReadOnlyList<Pulso> pulsos)
        {
            this.ConflictoEpoca = conflictoEpoca;
            this.EpocaActual = epocaActual;
            this.MundoCargado = mundoCargado;
            this.Pulsos = pulsos;
        }

        public bool ConflictoEpoca { get; }
        public string? EpocaActual { get; }
        public bool MundoCargado { get; }
        public IReadOnlyList<Pulso> Pulsos { get; }

        public static ResultadoConsultaPulsos Ok(string epocaActual, IReadOnlyList<Pulso> pulsos)
            => new(conflictoEpoca: false, epocaActual, mundoCargado: true, pulsos);

        public static ResultadoConsultaPulsos Conflicto(string? epocaActual, bool mundoCargado)
            => new(conflictoEpoca: true, epocaActual, mundoCargado, Array.Empty<Pulso>());
    }

    public enum EstadoRecursoApi
    {
        Ok = 0,
        MundoNoCargado = 1,
        NoEncontrado = 2,
    }

    public sealed record ResultadoConsultaRecurso<T>(EstadoRecursoApi Estado, T? Valor)
    {
        public static ResultadoConsultaRecurso<T> Ok(T valor) => new(EstadoRecursoApi.Ok, valor);
        public static ResultadoConsultaRecurso<T> SinMundo() => new(EstadoRecursoApi.MundoNoCargado, default);
        public static ResultadoConsultaRecurso<T> Ausente() => new(EstadoRecursoApi.NoEncontrado, default);
    }

    /// <summary>
    /// Núcleo de estado y contrato de la API del motor (ADS §4.1, §5, P-1, R-05, CP-203).
    /// Mantiene la época vigente, los pulsos y eventos de la carga actual, los esperadores de long-poll,
    /// la idempotencia por <c>(epoca, basada_en)</c> y la cola al hilo principal.
    /// </summary>
    public sealed class ApiMotor
    {
        public const int MaxEsperaLongPollMs = 60_000;
        public const int MaxEventosRetenidos = 5_000;

        private const string AlfabetoUlid = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        private static readonly JsonSerializerOptions JsonCompacto = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        private sealed class EsperadorPulsos
        {
            public EsperadorPulsos(string epoca, long since)
            {
                this.Epoca = epoca;
                this.Since = since;
                this.Tcs = new TaskCompletionSource<ResultadoConsultaPulsos>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public string Epoca { get; }
            public long Since { get; }
            public TaskCompletionSource<ResultadoConsultaPulsos> Tcs { get; }
        }

        private readonly record struct ClaveRespuesta(string Epoca, long BasadaEn);
        private readonly record struct ClaveRechazoHuella(string Epoca, long BasadaEn, string Huella);

        private readonly object sync = new();
        private readonly string versionMotor;
        private readonly List<string> personajesActivos = new();
        private readonly Dictionary<long, Pulso> pulsosPorId = new();
        private readonly List<Pulso> pulsosOrdenados = new();
        private readonly List<EsperadorPulsos> esperandoPulsos = new();
        private readonly List<EventoMotor> eventos = new();
        private readonly Dictionary<ClaveRespuesta, Resultado> aceptadasPorClave = new();
        private readonly Dictionary<ClaveRechazoHuella, Resultado> rechazosPorHuella = new();
        private readonly Dictionary<string, Func<Pulso, Respuesta, Resultado>> aplicadoresPorPersonaje = new(StringComparer.Ordinal);

        private Func<Pulso, Respuesta, Resultado>? aplicadorPorDefecto;
        private Func<string, InstantaneaMemoria?>? proveedorMemoria;
        private bool proveedorMemoriaEnHiloPrincipal;
        private Func<string, EscenaActual?>? proveedorEscena;
        private bool proveedorEscenaEnHiloPrincipal;

        private bool mundoCargado;
        private string? epocaVigente;
        private long siguientePulsoId = 1;
        private long siguienteEventoId = 1;

        public ApiMotor(string versionMotor = "0.1.0", MainThreadActionQueue? cola = null)
        {
            this.versionMotor = string.IsNullOrWhiteSpace(versionMotor) ? "0.1.0" : versionMotor.Trim();
            this.Cola = cola ?? new MainThreadActionQueue();
        }

        public MainThreadActionQueue Cola { get; }

        public bool MundoCargado
        {
            get
            {
                lock (this.sync)
                {
                    return this.mundoCargado;
                }
            }
        }

        public string? EpocaVigente
        {
            get
            {
                lock (this.sync)
                {
                    return this.epocaVigente;
                }
            }
        }

        /// <summary>Genera un identificador opaco único (ULID de 26 caracteres Crockford Base32) para una carga de mundo (P-1).</summary>
        public static string GenerarEpoca()
        {
            var chars = new char[26];
            var ms = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            for (var i = 9; i >= 0; i--)
            {
                chars[i] = AlfabetoUlid[(int)(ms & 31)];
                ms >>= 5;
            }

            Span<byte> aleatorios = stackalloc byte[10];
            RandomNumberGenerator.Fill(aleatorios);

            // 80 bits -> 16 símbolos Base32 (5 bits cada uno).
            var indiceBit = 0;
            for (var i = 10; i < 26; i++)
            {
                var indiceByte = indiceBit / 8;
                var desplazamiento = indiceBit % 8;
                int valor = aleatorios[indiceByte] >> desplazamiento;
                if (desplazamiento > 3 && indiceByte + 1 < aleatorios.Length)
                {
                    valor |= aleatorios[indiceByte + 1] << (8 - desplazamiento);
                }

                chars[i] = AlfabetoUlid[valor & 31];
                indiceBit += 5;
            }

            return new string(chars);
        }

        /// <summary>Inicia una nueva época de mundo (equivale a <c>SaveLoaded</c>), reiniciando cursores de pulsos y eventos.</summary>
        public void IniciarEpoca(string epoca, IEnumerable<string>? personajes = null)
        {
            if (string.IsNullOrWhiteSpace(epoca))
            {
                throw new ArgumentException("La época no puede estar vacía.", nameof(epoca));
            }

            List<EsperadorPulsos> esperadoresPrevios;
            lock (this.sync)
            {
                this.mundoCargado = true;
                this.epocaVigente = epoca.Trim();
                this.personajesActivos.Clear();
                if (personajes is not null)
                {
                    this.personajesActivos.AddRange(personajes.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal));
                }

                this.pulsosPorId.Clear();
                this.pulsosOrdenados.Clear();
                this.siguientePulsoId = 1;
                this.eventos.Clear();
                this.siguienteEventoId = 1;
                this.aceptadasPorClave.Clear();
                this.rechazosPorHuella.Clear();

                esperadoresPrevios = new List<EsperadorPulsos>(this.esperandoPulsos);
                this.esperandoPulsos.Clear();
            }

            foreach (var esperador in esperadoresPrevios)
            {
                esperador.Tcs.TrySetResult(ResultadoConsultaPulsos.Conflicto(epoca.Trim(), mundoCargado: true));
            }
        }

        /// <summary>Actualiza la lista de personajes activos gobernados en la partida vigente.</summary>
        public void ActualizarPersonajes(IEnumerable<string> personajes)
        {
            if (personajes is null)
            {
                throw new ArgumentNullException(nameof(personajes));
            }

            lock (this.sync)
            {
                this.personajesActivos.Clear();
                this.personajesActivos.AddRange(personajes.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.Ordinal));
            }
        }

        /// <summary>Marca el motor sin mundo cargado (al volver al título o antes de cargar partida) y limpia la cola.</summary>
        public void MarcarSinMundo(string motivoLimpieza = "El jugador ha salido de la partida.")
        {
            this.Cola.LimpiarPendientes(motivoLimpieza);

            List<EsperadorPulsos> esperadoresPrevios;
            lock (this.sync)
            {
                this.mundoCargado = false;
                this.epocaVigente = null;
                this.pulsosPorId.Clear();
                this.pulsosOrdenados.Clear();
                this.siguientePulsoId = 1;
                this.eventos.Clear();
                this.siguienteEventoId = 1;
                this.aceptadasPorClave.Clear();
                this.rechazosPorHuella.Clear();

                esperadoresPrevios = new List<EsperadorPulsos>(this.esperandoPulsos);
                this.esperandoPulsos.Clear();
            }

            foreach (var esperador in esperadoresPrevios)
            {
                esperador.Tcs.TrySetResult(ResultadoConsultaPulsos.Conflicto(null, mundoCargado: false));
            }
        }

        /// <summary>Construye la instantánea atómica para <c>GET /api/v1/salud</c>.</summary>
        public Salud ObtenerSalud()
        {
            lock (this.sync)
            {
                return new Salud
                {
                    Version = this.versionMotor,
                    Contrato = CorazonCore.VersionContrato,
                    MundoCargado = this.mundoCargado,
                    Epoca = this.mundoCargado ? this.epocaVigente : null,
                    Personajes = new List<string>(this.personajesActivos),
                };
            }
        }

        /// <summary>
        /// Registra un pulso emitido en la época vigente y despierta inmediatamente a los clientes en long-poll.
        /// Si <see cref="Pulso.Id"/> es <c>&lt;= 0</c>, asigna el siguiente id secuencial de la época.
        /// </summary>
        public Pulso EmitirPulso(Pulso pulso)
        {
            if (pulso is null)
            {
                throw new ArgumentNullException(nameof(pulso));
            }

            List<(EsperadorPulsos Esperador, string Epoca, List<Pulso> Pulsos)> porDespertar = new();

            lock (this.sync)
            {
                if (!this.mundoCargado || string.IsNullOrEmpty(this.epocaVigente))
                {
                    throw new InvalidOperationException("No se puede emitir un pulso sin una época de mundo cargada.");
                }

                if (string.IsNullOrWhiteSpace(pulso.Epoca))
                {
                    pulso.Epoca = this.epocaVigente;
                }
                else if (pulso.Epoca != this.epocaVigente)
                {
                    throw new InvalidOperationException(
                        $"El pulso pertenece a la época «{pulso.Epoca}», distinta de la vigente «{this.epocaVigente}».");
                }

                if (pulso.Id <= 0)
                {
                    pulso.Id = this.siguientePulsoId++;
                }
                else
                {
                    if (this.pulsosPorId.ContainsKey(pulso.Id))
                    {
                        throw new InvalidOperationException($"Ya existe un pulso con id {pulso.Id} en la época «{this.epocaVigente}».");
                    }

                    this.siguientePulsoId = Math.Max(this.siguientePulsoId, pulso.Id + 1);
                }

                this.pulsosPorId[pulso.Id] = pulso;
                this.pulsosOrdenados.Add(pulso);
                if (this.pulsosOrdenados.Count > 1 && this.pulsosOrdenados[^2].Id > pulso.Id)
                {
                    this.pulsosOrdenados.Sort((a, b) => a.Id.CompareTo(b.Id));
                }

                this.AgregarEventoBloqueado(
                    "pulso_emitido",
                    pulso.Personaje,
                    $"Pulso #{pulso.Id} ({pulso.Motivo}) para {pulso.Personaje}",
                    new Dictionary<string, object?>
                    {
                        ["pulso_id"] = pulso.Id,
                        ["motivo"] = pulso.Motivo,
                    });

                for (var i = this.esperandoPulsos.Count - 1; i >= 0; i--)
                {
                    var esperador = this.esperandoPulsos[i];
                    if (esperador.Epoca != this.epocaVigente)
                    {
                        continue;
                    }

                    var pendientes = this.pulsosOrdenados.Where(p => p.Id > esperador.Since).ToList();
                    if (pendientes.Count > 0)
                    {
                        this.esperandoPulsos.RemoveAt(i);
                        porDespertar.Add((esperador, this.epocaVigente, pendientes));
                    }
                }
            }

            foreach (var (esperador, epoca, pulsos) in porDespertar)
            {
                esperador.Tcs.TrySetResult(ResultadoConsultaPulsos.Ok(epoca, pulsos));
            }

            return pulso;
        }

        /// <summary>
        /// Consulta pulsos con <c>id &gt; since</c> en la época actual; si no hay ninguno y <paramref name="esperaMs"/> &gt; 0,
        /// espera de forma asíncrona sin bloquear ningún hilo hasta que llegue un pulso, cambie la época o expire el plazo.
        /// </summary>
        public async Task<ResultadoConsultaPulsos> EsperarPulsosAsync(
            string? epocaSolicitada,
            long since = 0,
            int esperaMs = 0,
            CancellationToken tokenCancelacion = default)
        {
            if (since < 0)
            {
                since = 0;
            }

            esperaMs = Math.Clamp(esperaMs, 0, MaxEsperaLongPollMs);

            EsperadorPulsos esperador;
            string epocaObjetivo;

            lock (this.sync)
            {
                if (!this.mundoCargado || string.IsNullOrEmpty(this.epocaVigente))
                {
                    return ResultadoConsultaPulsos.Conflicto(null, mundoCargado: false);
                }

                if (!string.IsNullOrWhiteSpace(epocaSolicitada) && epocaSolicitada != this.epocaVigente)
                {
                    return ResultadoConsultaPulsos.Conflicto(this.epocaVigente, mundoCargado: true);
                }

                epocaObjetivo = this.epocaVigente;
                var existentes = this.pulsosOrdenados.Where(p => p.Id > since).ToList();
                if (existentes.Count > 0 || esperaMs == 0)
                {
                    return ResultadoConsultaPulsos.Ok(epocaObjetivo, existentes);
                }

                esperador = new EsperadorPulsos(epocaObjetivo, since);
                this.esperandoPulsos.Add(esperador);
            }

            using var ctsDemora = CancellationTokenSource.CreateLinkedTokenSource(tokenCancelacion);
            var tareaEspera = Task.Delay(esperaMs, ctsDemora.Token);
            var completada = await Task.WhenAny(esperador.Tcs.Task, tareaEspera).ConfigureAwait(false);

            if (completada == esperador.Tcs.Task)
            {
                ctsDemora.Cancel();
                return await esperador.Tcs.Task.ConfigureAwait(false);
            }

            lock (this.sync)
            {
                this.esperandoPulsos.Remove(esperador);

                if (esperador.Tcs.Task.IsCompleted)
                {
                    return esperador.Tcs.Task.Result;
                }

                if (!this.mundoCargado || this.epocaVigente != epocaObjetivo)
                {
                    return ResultadoConsultaPulsos.Conflicto(this.epocaVigente, this.mundoCargado);
                }

                var recientes = this.pulsosOrdenados.Where(p => p.Id > since).ToList();
                return ResultadoConsultaPulsos.Ok(epocaObjetivo, recientes);
            }
        }

        /// <summary>Registra un evento de solo añadir en el registro del motor (<c>GET /api/v1/eventos</c>).</summary>
        public EventoMotor RegistrarEvento(
            string tipo,
            string? personaje,
            string resumen,
            Dictionary<string, object?>? datos = null)
        {
            if (string.IsNullOrWhiteSpace(tipo))
            {
                throw new ArgumentException("El tipo de evento no puede estar vacío.", nameof(tipo));
            }

            lock (this.sync)
            {
                return this.AgregarEventoBloqueado(tipo.Trim(), personaje, resumen, datos);
            }
        }

        /// <summary>Devuelve los eventos con <c>id &gt; since</c> en orden creciente.</summary>
        public RespuestaEventos ObtenerEventos(long since = 0, int limite = 500)
        {
            if (since < 0)
            {
                since = 0;
            }

            if (limite <= 0)
            {
                limite = 500;
            }

            limite = Math.Min(limite, 2000);

            lock (this.sync)
            {
                var filtrados = this.eventos
                    .Where(e => e.Id > since)
                    .Take(limite)
                    .ToList();

                var ultimoId = filtrados.Count > 0 ? filtrados[^1].Id : this.siguienteEventoId - 1;
                return new RespuestaEventos
                {
                    Epoca = this.mundoCargado ? this.epocaVigente : null,
                    Since = since,
                    UltimoId = ultimoId,
                    Eventos = filtrados,
                };
            }
        }

        /// <summary>Registra un aplicador para un personaje concreto; se invoca en el hilo principal tras superar la validación.</summary>
        public void RegistrarAplicador(string personaje, Func<Pulso, Respuesta, Resultado> aplicador)
        {
            if (string.IsNullOrWhiteSpace(personaje))
            {
                throw new ArgumentException("El id del personaje no puede estar vacío.", nameof(personaje));
            }

            if (aplicador is null)
            {
                throw new ArgumentNullException(nameof(aplicador));
            }

            lock (this.sync)
            {
                this.aplicadoresPorPersonaje[personaje] = aplicador;
            }
        }

        /// <summary>Registra un aplicador por defecto cuando no hay uno específico para el personaje del pulso.</summary>
        public void RegistrarAplicadorPorDefecto(Func<Pulso, Respuesta, Resultado>? aplicador)
        {
            lock (this.sync)
            {
                this.aplicadorPorDefecto = aplicador;
            }
        }

        /// <summary>Registra el proveedor de instantánea de memoria para <c>GET /api/v1/memoria/{personaje}</c>.</summary>
        public void RegistrarProveedorMemoria(Func<string, InstantaneaMemoria?>? proveedor, bool enHiloPrincipal = false)
        {
            lock (this.sync)
            {
                this.proveedorMemoria = proveedor;
                this.proveedorMemoriaEnHiloPrincipal = enHiloPrincipal;
            }
        }

        /// <summary>Registra el proveedor de escena actual para <c>GET /api/v1/escena/{personaje}</c>.</summary>
        public void RegistrarProveedorEscena(Func<string, EscenaActual?>? proveedor, bool enHiloPrincipal = false)
        {
            lock (this.sync)
            {
                this.proveedorEscena = proveedor;
                this.proveedorEscenaEnHiloPrincipal = enHiloPrincipal;
            }
        }

        /// <summary>Consulta la instantánea de memoria de <paramref name="personaje"/>.</summary>
        public async Task<ResultadoConsultaRecurso<InstantaneaMemoria>> ObtenerMemoriaAsync(
            string personaje,
            CancellationToken tokenCancelacion = default)
        {
            if (string.IsNullOrWhiteSpace(personaje))
            {
                return ResultadoConsultaRecurso<InstantaneaMemoria>.Ausente();
            }

            Func<string, InstantaneaMemoria?>? proveedor;
            bool enHiloPrincipal;
            string? epoca;
            bool conocido;

            lock (this.sync)
            {
                if (!this.mundoCargado || string.IsNullOrEmpty(this.epocaVigente))
                {
                    return ResultadoConsultaRecurso<InstantaneaMemoria>.SinMundo();
                }

                epoca = this.epocaVigente;
                conocido = this.personajesActivos.Contains(personaje, StringComparer.Ordinal);
                proveedor = this.proveedorMemoria;
                enHiloPrincipal = this.proveedorMemoriaEnHiloPrincipal;
            }

            if (proveedor is not null)
            {
                var instantanea = enHiloPrincipal
                    ? await this.Cola.EncolarAsync(() => proveedor(personaje), tokenCancelacion: tokenCancelacion).ConfigureAwait(false)
                    : proveedor(personaje);

                if (instantanea is null)
                {
                    return ResultadoConsultaRecurso<InstantaneaMemoria>.Ausente();
                }

                if (string.IsNullOrWhiteSpace(instantanea.Epoca))
                {
                    instantanea.Epoca = epoca;
                }

                if (string.IsNullOrWhiteSpace(instantanea.Personaje))
                {
                    instantanea.Personaje = personaje;
                }

                return ResultadoConsultaRecurso<InstantaneaMemoria>.Ok(instantanea);
            }

            if (!conocido)
            {
                return ResultadoConsultaRecurso<InstantaneaMemoria>.Ausente();
            }

            return ResultadoConsultaRecurso<InstantaneaMemoria>.Ok(new InstantaneaMemoria
            {
                Epoca = epoca,
                Personaje = personaje,
            });
        }

        /// <summary>Consulta la escena actual de <paramref name="personaje"/> sin consumir presupuesto.</summary>
        public async Task<ResultadoConsultaRecurso<EscenaActual>> ObtenerEscenaAsync(
            string personaje,
            CancellationToken tokenCancelacion = default)
        {
            if (string.IsNullOrWhiteSpace(personaje))
            {
                return ResultadoConsultaRecurso<EscenaActual>.Ausente();
            }

            Func<string, EscenaActual?>? proveedor;
            bool enHiloPrincipal;
            string? epoca;
            bool conocido;

            lock (this.sync)
            {
                if (!this.mundoCargado || string.IsNullOrEmpty(this.epocaVigente))
                {
                    return ResultadoConsultaRecurso<EscenaActual>.SinMundo();
                }

                epoca = this.epocaVigente;
                conocido = this.personajesActivos.Contains(personaje, StringComparer.Ordinal);
                proveedor = this.proveedorEscena;
                enHiloPrincipal = this.proveedorEscenaEnHiloPrincipal;
            }

            if (proveedor is not null)
            {
                var escena = enHiloPrincipal
                    ? await this.Cola.EncolarAsync(() => proveedor(personaje), tokenCancelacion: tokenCancelacion).ConfigureAwait(false)
                    : proveedor(personaje);

                if (escena is null)
                {
                    return ResultadoConsultaRecurso<EscenaActual>.Ausente();
                }

                if (string.IsNullOrWhiteSpace(escena.Epoca))
                {
                    escena.Epoca = epoca;
                }

                if (string.IsNullOrWhiteSpace(escena.Personaje))
                {
                    escena.Personaje = personaje;
                }

                return ResultadoConsultaRecurso<EscenaActual>.Ok(escena);
            }

            if (!conocido)
            {
                return ResultadoConsultaRecurso<EscenaActual>.Ausente();
            }

            return ResultadoConsultaRecurso<EscenaActual>.Ok(new EscenaActual
            {
                Epoca = epoca,
                Personaje = personaje,
                ObservacionDisponible = false,
                Escena = string.Empty,
            });
        }

        /// <summary>
        /// Encola una respuesta de la mente para validarse y aplicarse en el próximo tick del hilo principal.
        /// </summary>
        public Task<Resultado> ProcesarRespuestaAsync(
            Respuesta respuesta,
            string? huellaCuerpo = null,
            TimeSpan? tiempoLimite = null,
            CancellationToken tokenCancelacion = default)
        {
            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            return this.Cola.EncolarAsync(
                () => this.ProcesarRespuestaEnHiloPrincipal(respuesta, huellaCuerpo),
                tiempoLimite,
                tokenCancelacion);
        }

        /// <summary>
        /// Valida época, idempotencia, obsolescencia y orígenes de diario (CP-211) en el hilo principal
        /// y, si todo es válido, invoca el aplicador registrado.
        /// </summary>
        public Resultado ProcesarRespuestaEnHiloPrincipal(Respuesta respuesta, string? huellaCuerpo = null)
        {
            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            Pulso pulso;
            Func<Pulso, Respuesta, Resultado>? aplicador;
            string huella = huellaCuerpo ?? JsonSerializer.Serialize(respuesta, JsonCompacto);
            var clave = new ClaveRespuesta(respuesta.Epoca ?? string.Empty, respuesta.BasadaEn);
            var claveRechazo = new ClaveRechazoHuella(respuesta.Epoca ?? string.Empty, respuesta.BasadaEn, huella);

            lock (this.sync)
            {
                if (!this.mundoCargado || string.IsNullOrEmpty(this.epocaVigente))
                {
                    return Resultado.Rechazar(new MotivoRechazo(
                        ValidadorEpoca.CodigoEpocaObsoleta,
                        "epoca",
                        "no hay ninguna partida cargada en este momento"));
                }

                var errorEpoca = ValidadorEpoca.Comprobar(respuesta, this.epocaVigente);
                if (errorEpoca is not null)
                {
                    this.AgregarEventoBloqueado(
                        "respuesta_rechazada",
                        null,
                        $"Respuesta rechazada por época obsoleta (basada_en={respuesta.BasadaEn})",
                        new Dictionary<string, object?>
                        {
                            ["basada_en"] = respuesta.BasadaEn,
                            ["codigo"] = errorEpoca.Codigo,
                        });
                    return Resultado.Rechazar(errorEpoca);
                }

                if (this.aceptadasPorClave.TryGetValue(clave, out var resultadoPrevio))
                {
                    return resultadoPrevio;
                }

                if (this.rechazosPorHuella.TryGetValue(claveRechazo, out var rechazoPrevio))
                {
                    return rechazoPrevio;
                }

                if (!this.pulsosPorId.TryGetValue(respuesta.BasadaEn, out var pulsoEncontrado))
                {
                    var rechazo = Resultado.Rechazar(new MotivoRechazo(
                        "pulso_desconocido",
                        "basada_en",
                        "ese momento no existe en la sesión actual"));
                    this.rechazosPorHuella[claveRechazo] = rechazo;
                    this.AgregarEventoBloqueado(
                        "respuesta_rechazada",
                        null,
                        $"Respuesta rechazada: pulso #{respuesta.BasadaEn} desconocido",
                        new Dictionary<string, object?>
                        {
                            ["basada_en"] = respuesta.BasadaEn,
                            ["codigo"] = "pulso_desconocido",
                        });
                    return rechazo;
                }

                pulso = pulsoEncontrado;

                if (this.EstaSuperadaPorPulsoPosterior(pulso))
                {
                    var rechazo = Resultado.Rechazar(new MotivoRechazo(
                        "pulso_obsoleto",
                        "basada_en",
                        "ha ocurrido algo más reciente desde que empezaste a pensar en ello"));
                    this.rechazosPorHuella[claveRechazo] = rechazo;
                    this.AgregarEventoBloqueado(
                        "respuesta_obsoleta",
                        pulso.Personaje,
                        $"Respuesta al pulso #{pulso.Id} descartada como obsoleta",
                        new Dictionary<string, object?>
                        {
                            ["pulso_id"] = pulso.Id,
                            ["codigo"] = "pulso_obsoleto",
                        });
                    return rechazo;
                }

                var errorOrigen = ValidadorOrigenDiario.Comprobar(pulso, respuesta);
                if (errorOrigen is not null)
                {
                    var rechazo = Resultado.Rechazar(errorOrigen);
                    this.rechazosPorHuella[claveRechazo] = rechazo;
                    this.AgregarEventoBloqueado(
                        "respuesta_rechazada",
                        pulso.Personaje,
                        $"Respuesta al pulso #{pulso.Id} rechazada ({errorOrigen.Codigo})",
                        new Dictionary<string, object?>
                        {
                            ["pulso_id"] = pulso.Id,
                            ["codigo"] = errorOrigen.Codigo,
                            ["campo"] = errorOrigen.Campo,
                        });
                    return rechazo;
                }

                if (!this.aplicadoresPorPersonaje.TryGetValue(pulso.Personaje, out aplicador))
                {
                    aplicador = this.aplicadorPorDefecto;
                }
            }

            var resultado = aplicador is not null
                ? aplicador(pulso, respuesta)
                : new Resultado { Estado = Resultado.Aceptada };

            if (resultado.Estado == Resultado.Rechazada && (resultado.Motivos is null || resultado.Motivos.Count == 0))
            {
                resultado = Resultado.Rechazar(new MotivoRechazo(
                    "rechazo_motor",
                    "respuesta",
                    "no se pudo aplicar la respuesta en este momento"));
            }

            lock (this.sync)
            {
                if (this.mundoCargado && this.epocaVigente == clave.Epoca)
                {
                    if (resultado.Estado == Resultado.Aceptada)
                    {
                        this.aceptadasPorClave[clave] = resultado;
                        this.AgregarEventoBloqueado(
                            "respuesta_aceptada",
                            pulso.Personaje,
                            $"Respuesta aceptada para pulso #{pulso.Id} ({pulso.Personaje})",
                            new Dictionary<string, object?>
                            {
                                ["pulso_id"] = pulso.Id,
                            });
                    }
                    else
                    {
                        this.rechazosPorHuella[claveRechazo] = resultado;
                        this.AgregarEventoBloqueado(
                            "respuesta_rechazada",
                            pulso.Personaje,
                            $"Respuesta rechazada para pulso #{pulso.Id} ({pulso.Personaje})",
                            new Dictionary<string, object?>
                            {
                                ["pulso_id"] = pulso.Id,
                                ["motivos"] = resultado.Motivos?.Select(m => m.Codigo).ToList(),
                            });
                    }
                }
            }

            return resultado;
        }

        /// <summary>Drena la cola de acciones pendientes en el hilo principal (<c>UpdateTicked</c>).</summary>
        public int DrenarCola(int maxAcciones = 64) => this.Cola.Drenar(maxAcciones);

        private bool EstaSuperadaPorPulsoPosterior(Pulso pulso)
        {
            if (pulso.Motivo == Motivos.Interlocucion)
            {
                return false;
            }

            var prioridadBase = PrioridadMotivo(pulso.Motivo);
            for (var i = this.pulsosOrdenados.Count - 1; i >= 0; i--)
            {
                var candidato = this.pulsosOrdenados[i];
                if (candidato.Id <= pulso.Id)
                {
                    break;
                }

                if (string.Equals(candidato.Personaje, pulso.Personaje, StringComparison.Ordinal) &&
                    PrioridadMotivo(candidato.Motivo) >= prioridadBase)
                {
                    return true;
                }
            }

            return false;
        }

        private static int PrioridadMotivo(string motivo) => motivo switch
        {
            Motivos.Interlocucion => 3,
            Motivos.Hito or Motivos.FinIntencion or Motivos.Compromiso => 2,
            _ => 1,
        };

        private EventoMotor AgregarEventoBloqueado(
            string tipo,
            string? personaje,
            string resumen,
            Dictionary<string, object?>? datos)
        {
            var evento = new EventoMotor
            {
                Id = this.siguienteEventoId++,
                Epoca = this.epocaVigente ?? string.Empty,
                Tipo = tipo,
                Personaje = personaje,
                TiempoRealUtc = DateTimeOffset.UtcNow.ToString("O"),
                Resumen = resumen,
                Datos = datos,
            };

            this.eventos.Add(evento);
            if (this.eventos.Count > MaxEventosRetenidos)
            {
                this.eventos.RemoveAt(0);
            }

            return evento;
        }
    }
}
