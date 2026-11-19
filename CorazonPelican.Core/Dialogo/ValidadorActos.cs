using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>
    /// Validador determinista de actos de habla (GSS §8.1–§8.2, ADS §4.1, ADR-0003, R-07, R-08).
    /// Cumple «todos válidos o ninguno»: evalúa cada acto de la respuesta y, si al menos uno falla,
    /// rechaza el lote completo devolviendo todos los motivos en dos formas (<c>codigo</c> y <c>prosa</c>).
    /// </summary>
    public static class ValidadorActos
    {
        public static ResultadoValidacionActos Validar(Respuesta respuesta, ContextoValidacionActos contexto)
        {
            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            return Validar(respuesta.Actos, contexto);
        }

        public static ResultadoValidacionActos Validar(IReadOnlyList<Acto>? actos, ContextoValidacionActos contexto)
        {
            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            if (contexto.Observacion is null)
            {
                throw new ArgumentNullException(nameof(contexto), "La observación vigente es obligatoria para validar actos.");
            }

            if (contexto.Perfil is null)
            {
                throw new ArgumentNullException(nameof(contexto), "El perfil del personaje es obligatorio para validar actos.");
            }

            if (contexto.Estado is null)
            {
                throw new ArgumentNullException(nameof(contexto), "El estado del personaje es obligatorio para validar actos.");
            }

            if (actos is null || actos.Count == 0)
            {
                return new ResultadoValidacionActos(Array.Empty<MotivoRechazo>());
            }

            var motivos = new List<MotivoRechazo>();
            var lugaresOfrecidos = contexto.LugaresEfectivos;
            var apertura = AperturaPerfil.TramoPara(contexto.Perfil, contexto.CorazonesEfectivos);
            var obrasRegaladasEnLote = new HashSet<string>(StringComparer.Ordinal);

            for (var i = 0; i < actos.Count; i++)
            {
                ValidarActo(
                    actos[i],
                    $"actos[{i}]",
                    contexto,
                    lugaresOfrecidos,
                    apertura,
                    obrasRegaladasEnLote,
                    motivos);
            }

            return new ResultadoValidacionActos(motivos);
        }

        private static void ValidarActo(
            Acto? acto,
            string campoActo,
            ContextoValidacionActos contexto,
            LugaresOfrecidos lugaresOfrecidos,
            TramoApertura apertura,
            HashSet<string> obrasRegaladasEnLote,
            List<MotivoRechazo> motivos)
        {
            if (acto is null)
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    campoActo,
                    "el acto no puede estar vacío"));
                return;
            }

            if (string.IsNullOrWhiteSpace(acto.Tipo))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.acto",
                    "cada acto debe indicar su tipo"));
                return;
            }

            switch (acto.Tipo)
            {
                case TiposActo.Regala:
                    ValidarRegala(acto, campoActo, contexto, apertura, obrasRegaladasEnLote, motivos);
                    break;

                case TiposActo.Ensena:
                    ValidarEnsena(acto, campoActo, contexto, apertura, obrasRegaladasEnLote, motivos);
                    break;

                case TiposActo.Promete:
                    ValidarPromete(acto, campoActo, contexto, apertura, motivos);
                    break;

                case TiposActo.Cita:
                    ValidarCita(acto, campoActo, contexto, lugaresOfrecidos, motivos);
                    break;

                case TiposActo.Rechaza:
                    ValidarRechaza(acto, campoActo, motivos);
                    break;

                case TiposActo.AceptaRegalo:
                    ValidarAceptaRegalo(campoActo, contexto, motivos);
                    break;

                default:
                    motivos.Add(new MotivoRechazo(
                        "acto_desconocido",
                        $"{campoActo}.acto",
                        $"el acto «{acto.Tipo}» no forma parte del vocabulario permitido"));
                    break;
            }
        }

        private static void ValidarRegala(
            Acto acto,
            string campoActo,
            ContextoValidacionActos contexto,
            TramoApertura apertura,
            HashSet<string> obrasRegaladasEnLote,
            List<MotivoRechazo> motivos)
        {
            if (!apertura.Regalar)
            {
                motivos.Add(new MotivoRechazo(
                    "apertura_insuficiente",
                    campoActo,
                    "todavía no tienes suficiente confianza con esa persona para regalarle una obra"));
            }

            ValidarDestinatario(acto.Para, $"{campoActo}.para", obligatorio: true, contexto, exigirIgualAlInterlocutor: true, motivos);

            var obraValida = true;
            if (string.IsNullOrWhiteSpace(acto.ObraId))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.obra_id",
                    "el acto «regala» requiere indicar el identificador de la obra"));
                obraValida = false;
            }
            else if (!Acciones.EsIdObraValido(acto.ObraId))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.obra_id",
                    $"«{acto.ObraId}» no es un identificador de obra válido"));
                obraValida = false;
            }
            else
            {
                var obra = BuscarObra(contexto.Estado, acto.ObraId);
                if (obra is null)
                {
                    motivos.Add(new MotivoRechazo(
                        "obra_inexistente",
                        $"{campoActo}.obra_id",
                        $"no tienes ninguna obra con el identificador «{acto.ObraId}» en tu portafolio"));
                    obraValida = false;
                }
                else if (obra.Estado == EstadosObra.Regalada || obrasRegaladasEnLote.Contains(obra.ObraId))
                {
                    motivos.Add(new MotivoRechazo(
                        "obra_ya_regalada",
                        $"{campoActo}.obra_id",
                        $"la obra «{acto.ObraId}» ya fue regalada y no está disponible"));
                    obraValida = false;
                }
                else if (!EstadosObra.EsRegalable(obra.Estado))
                {
                    motivos.Add(new MotivoRechazo(
                        "obra_no_regalable",
                        $"{campoActo}.obra_id",
                        $"la obra «{acto.ObraId}» está en estado «{obra.Estado}» y solo puedes regalar obras terminadas o colgadas"));
                    obraValida = false;
                }
            }

            if (obraValida && acto.ObraId is not null)
            {
                obrasRegaladasEnLote.Add(acto.ObraId);
            }
        }

        private static void ValidarEnsena(
            Acto acto,
            string campoActo,
            ContextoValidacionActos contexto,
            TramoApertura apertura,
            HashSet<string> obrasRegaladasEnLote,
            List<MotivoRechazo> motivos)
        {
            if (!apertura.Ensenar)
            {
                motivos.Add(new MotivoRechazo(
                    "apertura_insuficiente",
                    campoActo,
                    "todavía no tienes suficiente confianza con esa persona para enseñarle tu trabajo"));
            }

            if (!string.IsNullOrWhiteSpace(acto.Para))
            {
                ValidarDestinatario(acto.Para, $"{campoActo}.para", obligatorio: false, contexto, exigirIgualAlInterlocutor: true, motivos);
            }

            if (string.IsNullOrWhiteSpace(acto.ObraId))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.obra_id",
                    "el acto «ensena» requiere indicar el identificador de la obra"));
                return;
            }

            if (!Acciones.EsIdObraValido(acto.ObraId))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.obra_id",
                    $"«{acto.ObraId}» no es un identificador de obra válido"));
                return;
            }

            var obra = BuscarObra(contexto.Estado, acto.ObraId);
            if (obra is null)
            {
                motivos.Add(new MotivoRechazo(
                    "obra_inexistente",
                    $"{campoActo}.obra_id",
                    $"no tienes ninguna obra con el identificador «{acto.ObraId}» en tu portafolio"));
                return;
            }

            if (obra.Estado == EstadosObra.Regalada || obrasRegaladasEnLote.Contains(obra.ObraId))
            {
                motivos.Add(new MotivoRechazo(
                    "obra_ya_regalada",
                    $"{campoActo}.obra_id",
                    $"la obra «{acto.ObraId}» ya la regalaste y no la tienes para enseñarla"));
                return;
            }

            if (!EstadosObra.EsEnsenable(obra.Estado))
            {
                motivos.Add(new MotivoRechazo(
                    "obra_no_disponible",
                    $"{campoActo}.obra_id",
                    $"la obra «{acto.ObraId}» está en estado «{obra.Estado}» y no puede enseñarse"));
            }
        }

        private static void ValidarPromete(
            Acto acto,
            string campoActo,
            ContextoValidacionActos contexto,
            TramoApertura apertura,
            List<MotivoRechazo> motivos)
        {
            if (!apertura.Encargos)
            {
                motivos.Add(new MotivoRechazo(
                    "apertura_insuficiente",
                    campoActo,
                    "todavía no tienes suficiente confianza con esa persona para aceptar un encargo o hacer esa promesa"));
            }

            if (string.IsNullOrWhiteSpace(acto.Obra))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.obra",
                    "el acto «promete» requiere indicar «nueva» o el identificador de una obra"));
            }
            else if (acto.Obra != "nueva")
            {
                if (!Acciones.EsIdObraValido(acto.Obra))
                {
                    motivos.Add(new MotivoRechazo(
                        "argumento_invalido",
                        $"{campoActo}.obra",
                        $"«{acto.Obra}» no es una obra válida (usa «nueva» o un identificador con formato «nombre-0001»)"));
                }
                else
                {
                    var existente = BuscarObra(contexto.Estado, acto.Obra);
                    if (existente is not null && existente.Estado is EstadosObra.Regalada or EstadosObra.Descartada or EstadosObra.Vendida)
                    {
                        motivos.Add(new MotivoRechazo(
                            "obra_no_disponible",
                            $"{campoActo}.obra",
                            $"la obra «{acto.Obra}» ya está «{existente.Estado}» y no puedes prometer trabajar sobre ella"));
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(acto.Tema))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.tema",
                    "el acto «promete» requiere indicar el tema de la obra prometida"));
            }

            ValidarDestinatario(acto.Para, $"{campoActo}.para", obligatorio: true, contexto, exigirIgualAlInterlocutor: true, motivos);
            ValidarCuandoFuturo(acto.Cuando, $"{campoActo}.cuando", contexto.Observacion, motivos);
        }

        private static void ValidarCita(
            Acto acto,
            string campoActo,
            ContextoValidacionActos contexto,
            LugaresOfrecidos lugaresOfrecidos,
            List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(acto.Lugar))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.lugar",
                    "el acto «cita» requiere indicar un lugar del léxico"));
            }
            else if (!PerfilValidator.EsIdValido(acto.Lugar))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.lugar",
                    $"«{acto.Lugar}» no es un identificador de lugar válido del léxico"));
            }
            else
            {
                var lugar = lugaresOfrecidos.DestinoDe(acto);
                if (lugar is null)
                {
                    motivos.Add(new MotivoRechazo(
                        "lugar_desconocido",
                        $"{campoActo}.lugar",
                        $"no conoces ni tienes disponible el lugar «{acto.Lugar}» en este momento"));
                }
                else if (PerfilValidator.AreaPendiente(lugar) || !TieneAreaValida(lugar))
                {
                    motivos.Add(new MotivoRechazo(
                        "lugar_no_alcanzable",
                        $"{campoActo}.lugar",
                        $"el lugar «{lugar.Prosa}» todavía no tiene un área alcanzable"));
                }
            }

            ValidarDestinatario(acto.Para, $"{campoActo}.para", obligatorio: true, contexto, exigirIgualAlInterlocutor: true, motivos);
            ValidarCuandoFuturo(acto.Cuando, $"{campoActo}.cuando", contexto.Observacion, motivos);
        }

        private static void ValidarRechaza(Acto acto, string campoActo, List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(acto.Motivo))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoActo}.motivo",
                    "el acto «rechaza» requiere indicar un motivo"));
            }
        }

        private static void ValidarAceptaRegalo(
            string campoActo,
            ContextoValidacionActos contexto,
            List<MotivoRechazo> motivos)
        {
            var haySucesoRegalo = contexto.Observacion.Sucesos is not null
                && contexto.Observacion.Sucesos.Any(s =>
                    EsSucesoRegalo(s.Tipo)
                    && (string.IsNullOrWhiteSpace(contexto.InterlocutorRef)
                        || string.IsNullOrWhiteSpace(s.ActorRef)
                        || string.Equals(s.ActorRef, contexto.InterlocutorRef, StringComparison.Ordinal)));

            var hayObjetoRegalo = contexto.ObjetosRegaloDisponibles is not null
                && contexto.ObjetosRegaloDisponibles.Any(o => !string.IsNullOrWhiteSpace(o));

            if (!haySucesoRegalo && !hayObjetoRegalo)
            {
                motivos.Add(new MotivoRechazo(
                    "sin_regalo_ofrecido",
                    campoActo,
                    "no has recibido ni te han ofrecido ningún regalo que aceptar ahora mismo"));
            }
        }

        private static void ValidarDestinatario(
            string? para,
            string campoPara,
            bool obligatorio,
            ContextoValidacionActos contexto,
            bool exigirIgualAlInterlocutor,
            List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(para))
            {
                if (obligatorio)
                {
                    motivos.Add(new MotivoRechazo(
                        "argumento_invalido",
                        campoPara,
                        "debes indicar la referencia de la persona destinataria"));
                }

                return;
            }

            if (!Acciones.EsRefValida(para))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    campoPara,
                    $"«{para}» no es una referencia válida de destinatario"));
                return;
            }

            var presenteEnEscena = contexto.Observacion.Entidades.Any(e =>
                string.Equals(e.Ref, para, StringComparison.Ordinal)
                && e.Categoria is CategoriaEntidad.Granjero or CategoriaEntidad.Npc);

            if (!presenteEnEscena)
            {
                motivos.Add(new MotivoRechazo(
                    "destinatario_ausente",
                    campoPara,
                    $"«{para}» no está presente en esta escena"));
                return;
            }

            if (exigirIgualAlInterlocutor
                && !string.IsNullOrWhiteSpace(contexto.InterlocutorRef)
                && !string.Equals(para, contexto.InterlocutorRef, StringComparison.Ordinal))
            {
                motivos.Add(new MotivoRechazo(
                    "destinatario_distinto",
                    campoPara,
                    $"estás hablando con «{contexto.InterlocutorRef}», no con «{para}»"));
            }
        }

        private static void ValidarCuandoFuturo(
            TiempoJuego? cuando,
            string campoCuando,
            Observacion observacion,
            List<MotivoRechazo> motivos)
        {
            if (cuando is null)
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    campoCuando,
                    "debes indicar la fecha y hora de juego del compromiso"));
                return;
            }

            if (cuando.Dia < 1 || !EsHoraJuegoValida(cuando.Hora))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    campoCuando,
                    $"el momento propuesto (día {cuando.Dia}, hora {cuando.Hora}) no es una fecha u hora de juego válida"));
                return;
            }

            if (!IntencionPersonaje.EsTiempoFuturo(cuando, observacion.Dia, observacion.Hora))
            {
                motivos.Add(new MotivoRechazo(
                    "plazo_pasado",
                    campoCuando,
                    $"la fecha propuesta (día {cuando.Dia}, {FormatearHora(cuando.Hora)}) no es posterior al momento actual (día {observacion.Dia}, {FormatearHora(observacion.Hora)})"));
            }
        }

        internal static ObraPortafolio? BuscarObra(EstadoPersonaje estado, string obraId)
            => estado.Portafolio.FirstOrDefault(o => string.Equals(o.ObraId, obraId, StringComparison.Ordinal));

        private static bool EsSucesoRegalo(string? tipo)
            => !string.IsNullOrWhiteSpace(tipo)
               && (string.Equals(tipo, "regalo", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(tipo, "regalo_ofrecido", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(tipo, "regalo_recibido", StringComparison.OrdinalIgnoreCase));

        private static bool TieneAreaValida(Lugar lugar)
            => lugar.Area.Count == 4 && lugar.Area[0] >= 0 && lugar.Area[1] >= 0 && lugar.Area[2] > 0 && lugar.Area[3] > 0;

        private static bool EsHoraJuegoValida(int hora)
            => hora >= HoraJuego.InicioDia && hora <= HoraJuego.FinDia && (hora % 100) < 60;

        private static string FormatearHora(int hora) => $"{hora / 100}:{hora % 100:00}";
    }

    /// <summary>
    /// Valida y aplica atómicamente una respuesta completa de diálogo en el orden exacto
    /// <b>intención → actos → memoria → frase</b> (ADS §4.1, ADR-0003, R-07, R-08).
    /// Si falla la intención, cualquier acto o la memoria, no muta el estado ni devuelve frase visible.
    /// </summary>
    public static class AplicadorTurnoDialogo
    {
        public static ResultadoTurnoDialogo Aplicar(
            Respuesta respuesta,
            ContextoValidacionActos contexto,
            string? idIntencion = null)
        {
            if (respuesta is null)
            {
                throw new ArgumentNullException(nameof(respuesta));
            }

            if (contexto is null)
            {
                throw new ArgumentNullException(nameof(contexto));
            }

            if (contexto.Observacion is null)
            {
                throw new ArgumentNullException(nameof(contexto), "La observación vigente es obligatoria.");
            }

            if (contexto.Perfil is null)
            {
                throw new ArgumentNullException(nameof(contexto), "El perfil del personaje es obligatorio.");
            }

            if (contexto.Estado is null)
            {
                throw new ArgumentNullException(nameof(contexto), "El estado del personaje es obligatorio.");
            }

            // Fase de validación completa previa a cualquier mutación de estado.
            if (contexto.Pulso is not null)
            {
                var rechazoOrigen = ValidadorOrigenDiario.Comprobar(contexto.Pulso, respuesta);
                if (rechazoOrigen is not null)
                {
                    return ResultadoTurnoDialogo.Rechazar(new[] { rechazoOrigen });
                }
            }

            IntencionPersonaje? intencionValidada = null;
            if (respuesta.Intencion is not null)
            {
                var idEfectivo = string.IsNullOrWhiteSpace(idIntencion)
                    ? $"int-dialogo-{(respuesta.BasadaEn > 0 ? respuesta.BasadaEn : 1)}"
                    : idIntencion.Trim();
                long? basadaEn = respuesta.BasadaEn > 0 ? respuesta.BasadaEn : null;

                var (creada, resIntencion) = ValidadorIntencion.ValidarYCrear(
                    idEfectivo,
                    respuesta.Intencion,
                    contexto.Observacion,
                    contexto.Perfil,
                    contexto.LugaresEfectivos,
                    contexto.Agenda,
                    contexto.ContextoRutina,
                    basadaEn: basadaEn);

                if (!resIntencion.EsValido)
                {
                    return ResultadoTurnoDialogo.Rechazar(resIntencion.Motivos);
                }

                intencionValidada = creada;
            }

            var resActos = ValidadorActos.Validar(respuesta.Actos, contexto);
            if (!resActos.EsValido)
            {
                return ResultadoTurnoDialogo.Rechazar(resActos.Motivos);
            }

            var errorMemoria = PrevalidarMemoria(respuesta.Memoria);
            if (errorMemoria is not null)
            {
                return ResultadoTurnoDialogo.Rechazar(new[] { errorMemoria });
            }

            // Fase de aplicación atómica en orden: intención → actos → memoria → frase.
            var etapas = new List<string>(4);

            if (intencionValidada is not null)
            {
                intencionValidada.Activar();
                contexto.Estado.IntencionActiva = intencionValidada.AIntencionActivaContrato();
                etapas.Add(EtapasTurno.Intencion);
            }

            var actosAplicados = new List<Acto>();
            var compromisosCreados = new List<CompromisoEstado>();
            var obrasRegaladas = new List<ObraPortafolio>();

            if (respuesta.Actos is { Count: > 0 })
            {
                foreach (var acto in respuesta.Actos)
                {
                    AplicarActo(acto, contexto.Estado, compromisosCreados, obrasRegaladas);
                    actosAplicados.Add(acto);
                }

                etapas.Add(EtapasTurno.Actos);
            }

            var recuerdosCreados = new List<Recuerdo>();
            if (respuesta.Memoria is not null)
            {
                if (contexto.Pulso is not null)
                {
                    var resMemoria = Consolidacion.AplicarMemoria(contexto.Estado, contexto.Pulso, respuesta);
                    if (!resMemoria.Aceptada && resMemoria.MotivoRechazo is not null)
                    {
                        // Si la memoria fallase aquí sin prevalidación previa, no se debe ocultar el motivo;
                        // la prevalidación anterior evita llegar aquí salvo inconsistencia externa.
                        return ResultadoTurnoDialogo.Rechazar(new[] { resMemoria.MotivoRechazo });
                    }

                    recuerdosCreados.AddRange(resMemoria.RecuerdosCreados);
                }
                else
                {
                    AplicarMemoriaSinPulso(respuesta.Memoria, contexto.Estado, recuerdosCreados);
                }

                etapas.Add(EtapasTurno.Memoria);
            }

            var fraseVisible = respuesta.Frase;
            if (!string.IsNullOrWhiteSpace(fraseVisible))
            {
                etapas.Add(EtapasTurno.Frase);
            }

            return ResultadoTurnoDialogo.Aceptar(
                fraseVisible,
                intencionValidada,
                actosAplicados,
                compromisosCreados,
                obrasRegaladas,
                recuerdosCreados,
                etapas);
        }

        private static MotivoRechazo? PrevalidarMemoria(MemoriaPropuesta? memoria)
        {
            if (memoria?.Reflexiones is not { } reflexiones)
            {
                return null;
            }

            for (var i = 0; i < reflexiones.Count; i++)
            {
                var origen = reflexiones[i].OrigenDiario;
                if (origen is null || origen.Count == 0)
                {
                    return new MotivoRechazo(
                        "origen_ausente",
                        $"memoria.reflexiones[{i}].origen_diario",
                        "recuerda qué episodio te dejó esa impresión");
                }

                if (string.IsNullOrWhiteSpace(reflexiones[i].Texto))
                {
                    return new MotivoRechazo(
                        "texto_vacio",
                        $"memoria.reflexiones[{i}].texto",
                        "la reflexión debe expresar qué recuerdas");
                }
            }

            return null;
        }

        private static void AplicarMemoriaSinPulso(
            MemoriaPropuesta memoria,
            EstadoPersonaje estado,
            List<Recuerdo> recuerdosCreados)
        {
            if (memoria.Reflexiones is { Count: > 0 } reflexiones)
            {
                foreach (var reflexion in reflexiones)
                {
                    recuerdosCreados.Add(estado.AgregarRecuerdo(reflexion.Texto, 0.75, reflexion.OrigenDiario));
                }

                return;
            }

            if (memoria.Recordar is { Count: > 0 } recordar)
            {
                foreach (var texto in recordar)
                {
                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        recuerdosCreados.Add(estado.AgregarRecuerdo(texto, 0.5));
                    }
                }
            }
        }

        private static void AplicarActo(
            Acto acto,
            EstadoPersonaje estado,
            List<CompromisoEstado> compromisosCreados,
            List<ObraPortafolio> obrasRegaladas)
        {
            switch (acto.Tipo)
            {
                case TiposActo.Regala:
                {
                    var obra = ValidadorActos.BuscarObra(estado, acto.ObraId!)!;
                    obra.Estado = EstadosObra.Regalada;
                    obra.Destinatario = acto.Para;
                    obrasRegaladas.Add(obra);
                    break;
                }

                case TiposActo.Promete:
                {
                    var objeto = acto.Obra == "nueva"
                        ? acto.Tema!.Trim()
                        : $"{acto.Obra}: {acto.Tema!.Trim()}";
                    var compromiso = new CompromisoEstado
                    {
                        Id = SiguienteIdCompromiso(estado),
                        Tipo = TiposActo.Promete,
                        Destinatario = acto.Para!,
                        Objeto = objeto,
                        PlazoVigente = new TiempoJuego { Dia = acto.Cuando!.Dia, Hora = acto.Cuando.Hora },
                        Estado = "pendiente",
                    };
                    estado.Compromisos.Add(compromiso);
                    compromisosCreados.Add(compromiso);
                    break;
                }

                case TiposActo.Cita:
                {
                    var compromiso = new CompromisoEstado
                    {
                        Id = SiguienteIdCompromiso(estado),
                        Tipo = TiposActo.Cita,
                        Destinatario = acto.Para!,
                        Objeto = acto.Lugar!,
                        PlazoVigente = new TiempoJuego { Dia = acto.Cuando!.Dia, Hora = acto.Cuando.Hora },
                        Estado = "pendiente",
                    };
                    estado.Compromisos.Add(compromiso);
                    compromisosCreados.Add(compromiso);
                    break;
                }
            }
        }

        private static string SiguienteIdCompromiso(EstadoPersonaje estado)
        {
            var existentes = new HashSet<string>(estado.Compromisos.Select(c => c.Id), StringComparer.Ordinal);
            var numero = Math.Max(1, estado.Compromisos.Count + 1);
            foreach (var c in estado.Compromisos)
            {
                if (c.Id.StartsWith("comp-", StringComparison.Ordinal)
                    && int.TryParse(c.Id.AsSpan(5), out var n)
                    && n >= numero)
                {
                    numero = n + 1;
                }
            }

            string id;
            do
            {
                id = $"comp-{numero}";
                numero++;
            }
            while (!existentes.Add(id));

            return id;
        }
    }
}
