using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>Estados válidos de una entrada de la agenda (<c>schemas/comun.schema.json#/$defs/estadoHito</c>).</summary>
    public static class EstadoHito
    {
        public const string Pendiente = "pendiente";
        public const string EnCurso = "en_curso";
        public const string Cumplido = "cumplido";
        public const string Saltado = "saltado";
        public const string Movido = "movido";
        public const string Sustituido = "sustituido";

        public static readonly IReadOnlyCollection<string> Todos = new HashSet<string>
        {
            Pendiente,
            EnCurso,
            Cumplido,
            Saltado,
            Movido,
            Sustituido,
        };
    }

    /// <summary>Tipos de cambio de rutina permitidos por el contrato (GSS §5.3, R-16).</summary>
    public static class TiposCambioRutina
    {
        public const string Saltar = "saltar";
        public const string Mover = "mover";
        public const string Sustituir = "sustituir";
    }

    /// <summary>Entrada vigente de la agenda del día, derivada del horario canónico y las anotaciones del perfil.</summary>
    public sealed class EntradaAgendaDia
    {
        public EntradaAgendaDia(
            string hito,
            int hora,
            string prosa,
            HitoResuelto hitoResuelto,
            string? idAnotacion,
            LimitesCambio? limites,
            string estado = EstadoHito.Pendiente,
            IReadOnlyList<PasoPlan>? planSustituto = null)
        {
            this.Hito = hito;
            this.Hora = hora;
            this.HoraOriginal = hora;
            this.Prosa = prosa;
            this.HitoResuelto = hitoResuelto;
            this.IdAnotacion = idAnotacion;
            this.Limites = limites;
            this.Estado = estado;
            this.PlanSustituto = planSustituto;
        }

        public string Hito { get; }

        public int Hora { get; internal set; }

        public int HoraOriginal { get; }

        public string Prosa { get; }

        public string Estado { get; internal set; }

        public IReadOnlyList<PasoPlan>? PlanSustituto { get; internal set; }

        public HitoResuelto HitoResuelto { get; }

        public string? IdAnotacion { get; }

        public LimitesCambio? Limites { get; }

        /// <summary>Proyecta esta entrada al DTO del contrato (<c>pulso.agenda</c>).</summary>
        public EntradaAgenda AEntradaContrato()
            => new() { Hito = this.Hito, Prosa = this.Prosa, Estado = this.Estado };
    }

    /// <summary>
    /// Agenda del personaje para un día concreto (GSS §4.1, §5.3).
    /// Se construye cada día desde el horario canónico y sus cambios nunca contaminan el día siguiente.
    /// </summary>
    public sealed class AgendaDia
    {
        private readonly List<EntradaAgendaDia> entradas;

        private AgendaDia(string? claveHorario, List<EntradaAgendaDia> entradas)
        {
            this.ClaveHorario = claveHorario;
            this.entradas = entradas;
        }

        public string? ClaveHorario { get; }

        public IReadOnlyList<EntradaAgendaDia> Entradas => this.entradas;

        /// <summary>Construye la agenda canónica del día a partir del horario y el perfil completo.</summary>
        public static AgendaDia DesdeHorario(
            HorarioDia? horario,
            PerfilPersonaje perfil,
            Func<EntradaHorario, HitoResuelto, string>? prosaDeEntrada = null)
        {
            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            return DesdeHorario(horario, perfil.Rutina, perfil.Lexico, prosaDeEntrada);
        }

        /// <summary>Construye la agenda canónica del día a partir de <see cref="HorarioDia"/> y la <see cref="Rutina"/> del perfil.</summary>
        public static AgendaDia DesdeHorario(
            HorarioDia? horario,
            Rutina rutina,
            Lexico? lexico = null,
            Func<EntradaHorario, HitoResuelto, string>? prosaDeEntrada = null)
        {
            if (rutina is null)
            {
                throw new ArgumentNullException(nameof(rutina));
            }

            if (horario is null || horario.Entradas.Count == 0)
            {
                return new AgendaDia(horario?.Clave, new List<EntradaAgendaDia>());
            }

            var entradas = new List<EntradaAgendaDia>(horario.Entradas.Count);
            foreach (var entrada in horario.Entradas)
            {
                var anotacion = HitosRutina.BuscarAnotacion(entrada, rutina);
                var hitoResuelto = HitosRutina.Resolver(horario, entrada, rutina);
                var limites = CombinarLimites(anotacion?.Limites, rutina.PorDefecto.Limites);
                var prosa = ResolverProsa(entrada, hitoResuelto, lexico, prosaDeEntrada);
                entradas.Add(new EntradaAgendaDia(
                    hitoResuelto.Id,
                    entrada.Hora,
                    prosa,
                    hitoResuelto,
                    anotacion?.Id,
                    limites));
            }

            return new AgendaDia(horario.Clave, entradas);
        }

        public EntradaAgendaDia? Buscar(string? hito)
            => string.IsNullOrWhiteSpace(hito)
                ? null
                : this.entradas.Find(e => string.Equals(e.Hito, hito, StringComparison.Ordinal));

        public bool Contiene(string? hito) => this.Buscar(hito) is not null;

        /// <summary>Proyecta todas las entradas de la agenda al formato <c>Pulso.Agenda</c>.</summary>
        public List<EntradaAgenda> Proyectar()
            => this.entradas.Select(e => e.AEntradaContrato()).ToList();

        /// <summary>Actualiza el estado operativo de un hito existente (p. ej. <c>en_curso</c> o <c>cumplido</c>).</summary>
        public void MarcarEstado(string hito, string estado)
        {
            if (!EstadoHito.Todos.Contains(estado))
            {
                throw new ArgumentException($"«{estado}» no es un estado de agenda válido.", nameof(estado));
            }

            var entrada = this.Buscar(hito)
                ?? throw new KeyNotFoundException($"El hito «{hito}» no existe en la agenda del día.");
            entrada.Estado = estado;
        }

        internal void AplicarCambio(CambioRutina cambio)
        {
            var entrada = this.Buscar(cambio.Hito)
                ?? throw new InvalidOperationException($"El hito «{cambio.Hito}» no existe en la agenda del día.");

            switch (cambio.Cambio)
            {
                case TiposCambioRutina.Saltar:
                    entrada.Estado = EstadoHito.Saltado;
                    entrada.PlanSustituto = null;
                    break;
                case TiposCambioRutina.Mover:
                    entrada.Estado = EstadoHito.Movido;
                    entrada.Hora = cambio.Hora!.Value;
                    entrada.PlanSustituto = null;
                    break;
                case TiposCambioRutina.Sustituir:
                    entrada.Estado = EstadoHito.Sustituido;
                    entrada.PlanSustituto = cambio.Plan!.Select(IntencionPersonaje.ClonarPaso).ToList();
                    break;
                default:
                    throw new InvalidOperationException($"Tipo de cambio desconocido: «{cambio.Cambio}».");
            }
        }

        private static LimitesCambio? CombinarLimites(LimitesCambio? especifica, LimitesCambio? porDefecto)
        {
            if (especifica is null && porDefecto is null)
            {
                return null;
            }

            return new LimitesCambio
            {
                MaxPorSemana = especifica?.MaxPorSemana ?? porDefecto?.MaxPorSemana,
                Nunca = especifica?.Nunca ?? porDefecto?.Nunca,
            };
        }

        private static string ResolverProsa(
            EntradaHorario entrada,
            HitoResuelto hito,
            Lexico? lexico,
            Func<EntradaHorario, HitoResuelto, string>? prosaDeEntrada)
        {
            var personalizada = prosaDeEntrada?.Invoke(entrada, hito);
            if (!string.IsNullOrWhiteSpace(personalizada))
            {
                return personalizada;
            }

            if (lexico is not null)
            {
                var exacto = lexico.Lugares.FirstOrDefault(l =>
                    l.Ubicacion == entrada.Ubicacion
                    && l.Area.Count == 4
                    && entrada.X >= l.Area[0] && entrada.X < l.Area[0] + l.Area[2]
                    && entrada.Y >= l.Area[1] && entrada.Y < l.Area[1] + l.Area[3]);
                var porUbicacion = exacto ?? lexico.Lugares.FirstOrDefault(l => l.Ubicacion == entrada.Ubicacion);
                if (porUbicacion is not null && !string.IsNullOrWhiteSpace(porUbicacion.Prosa))
                {
                    return porUbicacion.Prosa;
                }
            }

            return !string.IsNullOrWhiteSpace(entrada.Ubicacion) ? entrada.Ubicacion : hito.Id;
        }
    }

    /// <summary>Registro de un cambio de rutina aplicado en un día de juego concreto.</summary>
    public sealed record RegistroCambioRutina(int DiaJuego, string AnotacionId, string Hito = "", string Cambio = "");

    /// <summary>Contexto temporal y de condiciones del día para validar límites de R-16.</summary>
    public sealed class ContextoRutina
    {
        private readonly HashSet<string> condiciones;
        private readonly List<RegistroCambioRutina> historial;

        public ContextoRutina(
            int diaJuego,
            IEnumerable<string>? condicionesActivas = null,
            IEnumerable<RegistroCambioRutina>? historialCambios = null)
        {
            this.DiaJuego = diaJuego;
            this.condiciones = new HashSet<string>(
                condicionesActivas?.Where(c => !string.IsNullOrWhiteSpace(c)) ?? Enumerable.Empty<string>(),
                StringComparer.OrdinalIgnoreCase);
            this.historial = historialCambios?.ToList() ?? new List<RegistroCambioRutina>();
        }

        public int DiaJuego { get; private set; }

        public IReadOnlyCollection<string> CondicionesActivas => this.condiciones;

        public IReadOnlyList<RegistroCambioRutina> HistorialCambios => this.historial;

        /// <summary>Número de cambios registrados para la anotación en los últimos <paramref name="dias"/> días (incluyendo hoy).</summary>
        public int CambiosEnUltimosDias(string anotacionId, int dias = 7)
            => this.historial.Count(r =>
                string.Equals(r.AnotacionId, anotacionId, StringComparison.Ordinal)
                && r.DiaJuego <= this.DiaJuego
                && (this.DiaJuego - r.DiaJuego) < dias);

        public void RegistrarCambio(string anotacionId, string hito = "", string cambio = "")
            => this.historial.Add(new RegistroCambioRutina(this.DiaJuego, anotacionId, hito, cambio));

        /// <summary>Actualiza el día y las condiciones activas conservando el historial reciente.</summary>
        public void AvanzarDia(int nuevoDia, IEnumerable<string>? nuevasCondiciones = null)
        {
            this.DiaJuego = nuevoDia;
            this.condiciones.Clear();
            if (nuevasCondiciones is null)
            {
                return;
            }

            foreach (var condicion in nuevasCondiciones.Where(c => !string.IsNullOrWhiteSpace(c)))
            {
                this.condiciones.Add(condicion);
            }
        }
    }

    /// <summary>
    /// Validador determinista de cambios de rutina y agendas propuestas contra el perfil y el horario vigente (R-16).
    /// </summary>
    public static class ValidadorCambiosRutina
    {
        private const string ClavePorDefecto = "__por_defecto__";

        /// <summary>
        /// Valida una lista de <see cref="CambioRutina"/> sin modificar <paramref name="agenda"/> ni <paramref name="contexto"/>.
        /// Devuelve la lista de motivos de rechazo (vacía si todos son válidos).
        /// </summary>
        public static IReadOnlyList<MotivoRechazo> Validar(
            IReadOnlyList<CambioRutina>? cambios,
            AgendaDia agenda,
            PerfilPersonaje perfil,
            ContextoRutina contexto)
        {
            if (agenda is null)
            {
                throw new ArgumentNullException(nameof(agenda));
            }

            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            if (cambios is null || cambios.Count == 0)
            {
                return Array.Empty<MotivoRechazo>();
            }

            var motivos = new List<MotivoRechazo>();
            var hitosEnLote = new HashSet<string>(StringComparer.Ordinal);
            var cambiosEnLotePorAnotacion = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var i = 0; i < cambios.Count; i++)
            {
                ValidarUno(cambios[i], i, agenda, perfil, contexto, hitosEnLote, cambiosEnLotePorAnotacion, motivos);
            }

            return motivos;
        }

        /// <summary>
        /// Valida todos los cambios de forma atómica; solo si ninguno falla los aplica sobre <paramref name="agenda"/>
        /// y los registra en <paramref name="contexto"/>.
        /// </summary>
        public static IReadOnlyList<MotivoRechazo> ValidarYAplicar(
            IReadOnlyList<CambioRutina>? cambios,
            AgendaDia agenda,
            PerfilPersonaje perfil,
            ContextoRutina contexto)
        {
            var motivos = Validar(cambios, agenda, perfil, contexto);
            if (motivos.Count > 0 || cambios is null || cambios.Count == 0)
            {
                return motivos;
            }

            foreach (var cambio in cambios)
            {
                agenda.AplicarCambio(cambio);
                var entrada = agenda.Buscar(cambio.Hito)!;
                contexto.RegistrarCambio(ClaveAnotacion(entrada), cambio.Hito, cambio.Cambio);
            }

            return Array.Empty<MotivoRechazo>();
        }

        /// <summary>
        /// Valida la lista <c>agenda</c> propuesta en una respuesta de <c>despertar</c>:
        /// solo admite hitos reales de la agenda de hoy o planes de intención permitidos, sin duplicados ni hitos del motor.
        /// </summary>
        public static IReadOnlyList<MotivoRechazo> ValidarAgendaDespertar(
            IReadOnlyList<EstadoAgenda>? agendaPropuesta,
            AgendaDia agendaDelDia,
            IReadOnlyCollection<string>? planesIntencionPermitidos = null)
        {
            if (agendaDelDia is null)
            {
                throw new ArgumentNullException(nameof(agendaDelDia));
            }

            if (agendaPropuesta is null)
            {
                return Array.Empty<MotivoRechazo>();
            }

            var motivos = new List<MotivoRechazo>();
            var vistos = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < agendaPropuesta.Count; i++)
            {
                var entrada = agendaPropuesta[i];
                if (entrada is null || string.IsNullOrWhiteSpace(entrada.Hito))
                {
                    motivos.Add(new MotivoRechazo(
                        "hito_inexistente",
                        $"agenda[{i}].hito",
                        "cada entrada de la agenda debe indicar un identificador de hito"));
                    continue;
                }

                if (PerfilValidator.HitosDelMotor.Contains(entrada.Hito))
                {
                    motivos.Add(new MotivoRechazo(
                        "hito_del_motor",
                        $"agenda[{i}].hito",
                        $"el hito «{entrada.Hito}» pertenece al motor y no se edita en la agenda"));
                }
                else if (!vistos.Add(entrada.Hito))
                {
                    motivos.Add(new MotivoRechazo(
                        "hito_duplicado",
                        $"agenda[{i}].hito",
                        $"el hito «{entrada.Hito}» aparece repetido en la agenda"));
                }
                else if (!agendaDelDia.Contiene(entrada.Hito)
                         && (planesIntencionPermitidos is null || !planesIntencionPermitidos.Contains(entrada.Hito)))
                {
                    motivos.Add(new MotivoRechazo(
                        "hito_inexistente",
                        $"agenda[{i}].hito",
                        $"el hito «{entrada.Hito}» no está en el horario de hoy ni corresponde a un plan de intención permitido"));
                }

                if (!EstadoHito.Todos.Contains(entrada.Estado))
                {
                    motivos.Add(new MotivoRechazo(
                        "estado_hito_invalido",
                        $"agenda[{i}].estado",
                        $"«{entrada.Estado}» no es un estado válido para una entrada de la agenda"));
                }
            }

            return motivos;
        }

        /// <summary>
        /// Valida una respuesta completa de <c>despertar</c>: sus <c>cambios_rutina</c> (R-16), su <c>agenda</c>
        /// propuesta y la coherencia de los estados alterados en ella.
        /// </summary>
        public static IReadOnlyList<MotivoRechazo> ValidarRespuestaDespertar(
            Respuesta respuesta,
            AgendaDia agendaDelDia,
            PerfilPersonaje perfil,
            ContextoRutina contexto,
            IReadOnlyCollection<string>? planesIntencionPermitidos = null)
        {
            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            var motivos = new List<MotivoRechazo>();
            var cambios = respuesta.Intencion?.CambiosRutina;
            motivos.AddRange(Validar(cambios, agendaDelDia, perfil, contexto));
            motivos.AddRange(ValidarAgendaDespertar(respuesta.Agenda, agendaDelDia, planesIntencionPermitidos));

            if (respuesta.Agenda is null)
            {
                return motivos;
            }

            var estadoPorCambio = (cambios ?? new List<CambioRutina>())
                .Where(c => !string.IsNullOrWhiteSpace(c.Hito))
                .GroupBy(c => c.Hito, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => EstadoDeCambio(g.Last().Cambio), StringComparer.Ordinal);

            for (var i = 0; i < respuesta.Agenda.Count; i++)
            {
                var item = respuesta.Agenda[i];
                if (item is null || string.IsNullOrWhiteSpace(item.Hito))
                {
                    continue;
                }

                var entradaDia = agendaDelDia.Buscar(item.Hito);
                if (entradaDia is null)
                {
                    continue;
                }

                if (item.Estado is EstadoHito.Saltado or EstadoHito.Movido or EstadoHito.Sustituido
                    && entradaDia.Estado != item.Estado
                    && (!estadoPorCambio.TryGetValue(item.Hito, out var esperado) || esperado != item.Estado))
                {
                    motivos.Add(new MotivoRechazo(
                        "cambio_no_autorizado",
                        $"agenda[{i}].estado",
                        $"no puedes marcar «{item.Hito}» como «{item.Estado}» sin un cambio de rutina válido"));
                }
            }

            return motivos;
        }

        private static void ValidarUno(
            CambioRutina? cambio,
            int indice,
            AgendaDia agenda,
            PerfilPersonaje perfil,
            ContextoRutina contexto,
            HashSet<string> hitosEnLote,
            Dictionary<string, int> cambiosEnLotePorAnotacion,
            List<MotivoRechazo> motivos)
        {
            var baseCampo = $"intencion.cambios_rutina[{indice}]";
            if (cambio is null || string.IsNullOrWhiteSpace(cambio.Hito))
            {
                motivos.Add(new MotivoRechazo(
                    "hito_inexistente",
                    $"{baseCampo}.hito",
                    "el cambio de rutina debe indicar el hito que quieres modificar"));
                return;
            }

            if (PerfilValidator.HitosDelMotor.Contains(cambio.Hito))
            {
                motivos.Add(new MotivoRechazo(
                    "hito_del_motor",
                    $"{baseCampo}.hito",
                    $"el hito «{cambio.Hito}» es del motor y nunca se puede saltar, mover ni sustituir"));
                return;
            }

            var entrada = agenda.Buscar(cambio.Hito);
            if (entrada is null)
            {
                motivos.Add(new MotivoRechazo(
                    "hito_inexistente",
                    $"{baseCampo}.hito",
                    $"el hito «{cambio.Hito}» no figura en la agenda de hoy"));
                return;
            }

            if (!hitosEnLote.Add(cambio.Hito))
            {
                motivos.Add(new MotivoRechazo(
                    "hito_duplicado",
                    $"{baseCampo}.hito",
                    $"el hito «{cambio.Hito}» tiene más de un cambio propuesto en la misma respuesta"));
                return;
            }

            if (entrada.Estado == EstadoHito.Cumplido)
            {
                motivos.Add(new MotivoRechazo(
                    "hito_ya_cumplido",
                    $"{baseCampo}.hito",
                    $"el hito «{cambio.Hito}» ya se cumplió y no puede cambiarse"));
                return;
            }

            if (!PerfilValidator.CambiosRutina.Contains(cambio.Cambio))
            {
                motivos.Add(new MotivoRechazo(
                    "cambio_desconocido",
                    $"{baseCampo}.cambio",
                    $"«{cambio.Cambio}» no es un cambio de rutina válido (saltar, mover o sustituir)"));
                return;
            }

            if (!entrada.HitoResuelto.Cambios.Contains(cambio.Cambio))
            {
                motivos.Add(new MotivoRechazo(
                    "cambio_no_permitido",
                    $"{baseCampo}.cambio",
                    entrada.HitoResuelto.Cambios.Count == 0
                        ? $"el hito «{cambio.Hito}» no admite cambios en tu rutina"
                        : $"el hito «{cambio.Hito}» no permite «{cambio.Cambio}»"));
                return;
            }

            if (!ValidarLimites(cambio, entrada, baseCampo, contexto, cambiosEnLotePorAnotacion, motivos))
            {
                return;
            }

            ValidarDetalleCambio(cambio, baseCampo, perfil, motivos);
        }

        private static bool ValidarLimites(
            CambioRutina cambio,
            EntradaAgendaDia entrada,
            string baseCampo,
            ContextoRutina contexto,
            Dictionary<string, int> cambiosEnLotePorAnotacion,
            List<MotivoRechazo> motivos)
        {
            var limites = entrada.Limites;
            if (limites?.Nunca is not null)
            {
                var prohibida = limites.Nunca.FirstOrDefault(c => contexto.CondicionesActivas.Contains(c));
                if (prohibida is not null)
                {
                    motivos.Add(new MotivoRechazo(
                        "condicion_prohibida",
                        baseCampo,
                        $"no puedes cambiar «{cambio.Hito}» en un día con «{prohibida}»"));
                    return false;
                }
            }

            if (limites?.MaxPorSemana is int maxPorSemana)
            {
                var claveAnotacion = ClaveAnotacion(entrada);
                var usados = contexto.CambiosEnUltimosDias(claveAnotacion)
                    + cambiosEnLotePorAnotacion.GetValueOrDefault(claveAnotacion);
                if (usados >= maxPorSemana)
                {
                    motivos.Add(new MotivoRechazo(
                        "limite_semanal",
                        baseCampo,
                        $"ya alcanzaste el máximo de {maxPorSemana} cambio(s) por semana para este hito"));
                    return false;
                }

                cambiosEnLotePorAnotacion[claveAnotacion] = cambiosEnLotePorAnotacion.GetValueOrDefault(claveAnotacion) + 1;
            }

            return true;
        }

        private static void ValidarDetalleCambio(
            CambioRutina cambio,
            string baseCampo,
            PerfilPersonaje perfil,
            List<MotivoRechazo> motivos)
        {
            switch (cambio.Cambio)
            {
                case TiposCambioRutina.Saltar:
                    return;

                case TiposCambioRutina.Mover:
                    if (cambio.Hora is not int hora)
                    {
                        motivos.Add(new MotivoRechazo(
                            "hora_requerida",
                            $"{baseCampo}.hora",
                            $"para mover «{cambio.Hito}» debes indicar una hora"));
                        return;
                    }

                    if (!EsHoraValida(hora))
                    {
                        motivos.Add(new MotivoRechazo(
                            "hora_invalida",
                            $"{baseCampo}.hora",
                            $"la hora {hora} no es válida: debe estar entre {HoraJuego.InicioDia} y {HoraJuego.FinDia - 1} con minutos menores que 60"));
                    }

                    return;

                case TiposCambioRutina.Sustituir:
                    if (cambio.Plan is null || cambio.Plan.Count == 0)
                    {
                        motivos.Add(new MotivoRechazo(
                            "plan_requerido",
                            $"{baseCampo}.plan",
                            $"para sustituir «{cambio.Hito}» debes proponer un plan no vacío"));
                        return;
                    }

                    for (var j = 0; j < cambio.Plan.Count; j++)
                    {
                        ValidarPasoSustituto(cambio.Plan[j], $"{baseCampo}.plan[{j}]", perfil, motivos);
                    }

                    return;
            }
        }

        private static void ValidarPasoSustituto(
            PasoPlan? paso,
            string campoPaso,
            PerfilPersonaje perfil,
            List<MotivoRechazo> motivos)
        {
            if (paso is null || string.IsNullOrWhiteSpace(paso.Accion))
            {
                motivos.Add(new MotivoRechazo(
                    "accion_requerida",
                    $"{campoPaso}.accion",
                    "cada paso del plan debe indicar una acción"));
                return;
            }

            if (paso.CamposExtra is { Count: > 0 })
            {
                foreach (var claveExtra in paso.CamposExtra.Keys)
                {
                    motivos.Add(new MotivoRechazo(
                        "argumento_invalido",
                        $"{campoPaso}.{claveExtra}",
                        $"el campo «{claveExtra}» no está permitido en un paso del plan (no se admiten coordenadas, casillas ni rutas)"));
                }
            }

            if (!Acciones.VocabularioV1.Contains(paso.Accion) || !perfil.Acciones.Contains(paso.Accion))
            {
                motivos.Add(new MotivoRechazo(
                    "accion_no_permitida",
                    $"{campoPaso}.accion",
                    $"la acción «{paso.Accion}» no forma parte de las acciones válidas del personaje"));
                return;
            }

            switch (paso.Accion)
            {
                case "ir_a":
                case "sentarse":
                    if (string.IsNullOrWhiteSpace(paso.Lugar))
                    {
                        motivos.Add(new MotivoRechazo(
                            "lugar_requerido",
                            $"{campoPaso}.lugar",
                            $"la acción «{paso.Accion}» requiere un lugar del léxico"));
                    }
                    else if (!PerfilValidator.EsIdValido(paso.Lugar))
                    {
                        motivos.Add(new MotivoRechazo(
                            "lugar_invalido",
                            $"{campoPaso}.lugar",
                            $"«{paso.Lugar}» no es un identificador válido del léxico (no se admiten coordenadas ni rutas)"));
                    }
                    else if (perfil.Lexico.Lugares.Count > 0 && !perfil.Lexico.Lugares.Any(l => l.Id == paso.Lugar))
                    {
                        motivos.Add(new MotivoRechazo(
                            "lugar_desconocido",
                            $"{campoPaso}.lugar",
                            $"el lugar «{paso.Lugar}» no existe en el léxico del personaje"));
                    }

                    break;

                case "mirar":
                    if (string.IsNullOrWhiteSpace(paso.Objetivo) || paso.Objetivo.Any(char.IsWhiteSpace))
                    {
                        motivos.Add(new MotivoRechazo(
                            "objetivo_requerido",
                            $"{campoPaso}.objetivo",
                            "la acción «mirar» requiere una referencia de objetivo válida"));
                    }

                    break;

                case "dibujar":
                    if (string.IsNullOrWhiteSpace(paso.Obra))
                    {
                        motivos.Add(new MotivoRechazo(
                            "obra_requerida",
                            $"{campoPaso}.obra",
                            "la acción «dibujar» requiere indicar «nueva» o un id de obra"));
                    }

                    break;

                case "gesto":
                    if (string.IsNullOrWhiteSpace(paso.Tipo))
                    {
                        motivos.Add(new MotivoRechazo(
                            "gesto_requerido",
                            $"{campoPaso}.tipo",
                            "la acción «gesto» requiere indicar un tipo de gesto"));
                    }

                    break;

                case "murmurar":
                    if (string.IsNullOrWhiteSpace(paso.Texto))
                    {
                        motivos.Add(new MotivoRechazo(
                            "texto_requerido",
                            $"{campoPaso}.texto",
                            "la acción «murmurar» requiere el texto a decir"));
                    }

                    break;

                case "esperar":
                    if (paso.DuracionMin is not >= 10)
                    {
                        motivos.Add(new MotivoRechazo(
                            "duracion_invalida",
                            $"{campoPaso}.duracion_min",
                            "la acción «esperar» requiere una duración mínima de 10 minutos de juego"));
                    }

                    break;
            }
        }

        private static bool EsHoraValida(int hora)
            => hora >= HoraJuego.InicioDia && hora < HoraJuego.FinDia && (hora % 100) < 60;

        private static string ClaveAnotacion(EntradaAgendaDia entrada)
            => entrada.IdAnotacion ?? ClavePorDefecto;

        private static string? EstadoDeCambio(string cambio) => cambio switch
        {
            TiposCambioRutina.Saltar => EstadoHito.Saltado,
            TiposCambioRutina.Mover => EstadoHito.Movido,
            TiposCambioRutina.Sustituir => EstadoHito.Sustituido,
            _ => null,
        };
    }
}
