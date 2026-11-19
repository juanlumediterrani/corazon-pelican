using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>Resultado de validar una intención contra el perfil y la observación vigente (R-06).</summary>
    public sealed class ResultadoValidacionIntencion
    {
        public ResultadoValidacionIntencion(IReadOnlyList<MotivoRechazo> motivos)
        {
            this.Motivos = motivos ?? Array.Empty<MotivoRechazo>();
        }

        public IReadOnlyList<MotivoRechazo> Motivos { get; }

        public bool EsValido => this.Motivos.Count == 0;

        public Resultado AResultadoContrato()
            => this.EsValido
                ? new Resultado { Estado = Resultado.Aceptada }
                : new Resultado { Estado = Resultado.Rechazada, Motivos = this.Motivos.ToList() };
    }

    /// <summary>
    /// Validador determinista de precondiciones de una intención (GSS §5.1, ADS §4.1, R-06, R-13, R-14).
    /// Comprueba lugares en <see cref="LugaresOfrecidos"/>, áreas medidas, sentabilidad, acciones
    /// permitidas por el perfil y el vocabulario v1, argumentos requeridos, objetivos presentes en la
    /// observación, ausencia de coordenadas/pasos inventados y plazos estrictamente futuros.
    /// </summary>
    public static class ValidadorIntencion
    {
        public static ResultadoValidacionIntencion Validar(
            IntencionPersonaje intencion,
            Observacion observacion,
            PerfilPersonaje perfil,
            LugaresOfrecidos? lugaresOfrecidos = null,
            AgendaDia? agenda = null,
            ContextoRutina? contextoRutina = null,
            IReadOnlyCollection<string>? obrasConocidas = null,
            IReadOnlyCollection<string>? objetivosConocidos = null)
        {
            if (intencion is null)
            {
                throw new ArgumentNullException(nameof(intencion));
            }

            return Validar(
                intencion.APropuestaContrato(),
                observacion,
                perfil,
                lugaresOfrecidos,
                agenda,
                contextoRutina,
                obrasConocidas,
                objetivosConocidos);
        }

        public static ResultadoValidacionIntencion Validar(
            IntencionPropuesta? propuesta,
            Observacion observacion,
            PerfilPersonaje perfil,
            LugaresOfrecidos? lugaresOfrecidos = null,
            AgendaDia? agenda = null,
            ContextoRutina? contextoRutina = null,
            IReadOnlyCollection<string>? obrasConocidas = null,
            IReadOnlyCollection<string>? objetivosConocidos = null)
        {
            if (observacion is null)
            {
                throw new ArgumentNullException(nameof(observacion));
            }

            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            var motivos = new List<MotivoRechazo>();
            if (propuesta is null)
            {
                motivos.Add(new MotivoRechazo(
                    "intencion_requerida",
                    "intencion",
                    "no has propuesto ninguna intención"));
                return new ResultadoValidacionIntencion(motivos);
            }

            ValidarProsaObligatoria(propuesta, motivos);
            ValidarPlazo(propuesta.PlazoJuego, observacion, motivos);
            ValidarPesosAtencion(propuesta.PesosAtencion, motivos);

            var ofrecidos = lugaresOfrecidos ?? perfil.Lexico.OfrecerLugares(_ => true);
            ValidarPlan(propuesta.Plan, observacion, perfil, ofrecidos, obrasConocidas, objetivosConocidos, motivos);

            if (propuesta.CambiosRutina is { Count: > 0 } && agenda is not null)
            {
                var contexto = contextoRutina ?? new ContextoRutina(observacion.Dia);
                motivos.AddRange(ValidadorCambiosRutina.Validar(propuesta.CambiosRutina, agenda, perfil, contexto));
            }

            return new ResultadoValidacionIntencion(motivos);
        }

        /// <summary>
        /// Construye la <see cref="IntencionPersonaje"/> desde <paramref name="propuesta"/>, la valida contra el mundo
        /// vigente y la deja en <see cref="EstadoIntencion.Validada"/> o <see cref="EstadoIntencion.Rechazada"/>.
        /// </summary>
        public static (IntencionPersonaje? Intencion, ResultadoValidacionIntencion Resultado) ValidarYCrear(
            string id,
            IntencionPropuesta? propuesta,
            Observacion observacion,
            PerfilPersonaje perfil,
            LugaresOfrecidos? lugaresOfrecidos = null,
            AgendaDia? agenda = null,
            ContextoRutina? contextoRutina = null,
            IReadOnlyCollection<string>? obrasConocidas = null,
            IReadOnlyCollection<string>? objetivosConocidos = null,
            long? basadaEn = null)
        {
            var resultado = Validar(
                propuesta,
                observacion,
                perfil,
                lugaresOfrecidos,
                agenda,
                contextoRutina,
                obrasConocidas,
                objetivosConocidos);

            if (propuesta is null)
            {
                return (null, resultado);
            }

            var intencion = IntencionPersonaje.DesdePropuesta(id, propuesta, basadaEn);
            if (resultado.EsValido)
            {
                intencion.MarcarValidada();
            }
            else
            {
                intencion.Rechazar(resultado.Motivos);
            }

            return (intencion, resultado);
        }

        private static void ValidarProsaObligatoria(IntencionPropuesta propuesta, List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(propuesta.Objetivo))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    "intencion.objetivo",
                    "tienes que indicar qué objetivo persigue tu intención"));
            }

            if (string.IsNullOrWhiteSpace(propuesta.Porque))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    "intencion.porque",
                    "tienes que explicar por qué quieres hacerlo"));
            }
        }

        private static void ValidarPlazo(TiempoJuego? plazo, Observacion observacion, List<MotivoRechazo> motivos)
        {
            if (plazo is null)
            {
                return;
            }

            if (plazo.Dia < 1 || !EsHoraJuegoValida(plazo.Hora))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    "intencion.plazo_juego",
                    $"el plazo (día {plazo.Dia}, hora {plazo.Hora}) no es un momento de juego válido"));
                return;
            }

            if (!IntencionPersonaje.EsTiempoFuturo(plazo, observacion.Dia, observacion.Hora))
            {
                motivos.Add(new MotivoRechazo(
                    "plazo_pasado",
                    "intencion.plazo_juego",
                    $"el plazo propuesto (día {plazo.Dia}, {FormatearHora(plazo.Hora)}) ya ha pasado respecto al momento actual (día {observacion.Dia}, {FormatearHora(observacion.Hora)})"));
            }
        }

        private static void ValidarPesosAtencion(Dictionary<string, double>? pesos, List<MotivoRechazo> motivos)
        {
            if (pesos is null || pesos.Count == 0)
            {
                return;
            }

            foreach (var (clave, valor) in pesos)
            {
                if (!PerfilValidator.EsIdValido(clave))
                {
                    motivos.Add(new MotivoRechazo(
                        "argumento_invalido",
                        $"intencion.pesos_atencion.{clave}",
                        $"la etiqueta de atención «{clave}» no es un identificador válido"));
                }

                if (double.IsNaN(valor) || double.IsInfinity(valor) || valor < 0)
                {
                    motivos.Add(new MotivoRechazo(
                        "argumento_invalido",
                        $"intencion.pesos_atencion.{clave}",
                        $"el peso de atención para «{clave}» debe ser un número finito mayor o igual que cero"));
                }
            }
        }

        private static void ValidarPlan(
            List<PasoPlan>? plan,
            Observacion observacion,
            PerfilPersonaje perfil,
            LugaresOfrecidos ofrecidos,
            IReadOnlyCollection<string>? obrasConocidas,
            IReadOnlyCollection<string>? objetivosConocidos,
            List<MotivoRechazo> motivos)
        {
            if (plan is null || plan.Count == 0)
            {
                motivos.Add(new MotivoRechazo(
                    "plan_vacio",
                    "intencion.plan",
                    "el plan de la intención no puede estar vacío"));
                return;
            }

            for (var i = 0; i < plan.Count; i++)
            {
                ValidarPaso(plan[i], $"intencion.plan[{i}]", observacion, perfil, ofrecidos, obrasConocidas, objetivosConocidos, motivos);
            }
        }

        private static void ValidarPaso(
            PasoPlan? paso,
            string campoPaso,
            Observacion observacion,
            PerfilPersonaje perfil,
            LugaresOfrecidos ofrecidos,
            IReadOnlyCollection<string>? obrasConocidas,
            IReadOnlyCollection<string>? objetivosConocidos,
            List<MotivoRechazo> motivos)
        {
            if (paso is null)
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    campoPaso,
                    "el paso del plan no puede estar vacío"));
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

            if (string.IsNullOrWhiteSpace(paso.Accion))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.accion",
                    "cada paso del plan debe indicar una acción"));
                return;
            }

            if (!Acciones.VocabularioV1.Contains(paso.Accion) || !perfil.Acciones.Contains(paso.Accion))
            {
                motivos.Add(new MotivoRechazo(
                    "accion_no_permitida",
                    $"{campoPaso}.accion",
                    $"la acción «{paso.Accion}» no está permitida en tu perfil o no existe en el vocabulario"));
                return;
            }

            switch (paso.Accion)
            {
                case "ir_a":
                case "sentarse":
                    ValidarPasoLugar(paso, campoPaso, ofrecidos, motivos);
                    break;

                case "mirar":
                    ValidarPasoMirar(paso, campoPaso, observacion, objetivosConocidos, motivos);
                    break;

                case "dibujar":
                    ValidarPasoDibujar(paso, campoPaso, obrasConocidas, motivos);
                    break;

                case "gesto":
                    if (string.IsNullOrWhiteSpace(paso.Tipo))
                    {
                        motivos.Add(new MotivoRechazo(
                            "argumento_invalido",
                            $"{campoPaso}.tipo",
                            "la acción «gesto» requiere indicar un tipo de gesto"));
                    }
                    else if (!Acciones.TiposGestoV1.Contains(paso.Tipo))
                    {
                        motivos.Add(new MotivoRechazo(
                            "argumento_invalido",
                            $"{campoPaso}.tipo",
                            $"«{paso.Tipo}» no es un tipo de gesto válido"));
                    }

                    break;

                case "murmurar":
                    if (string.IsNullOrWhiteSpace(paso.Texto))
                    {
                        motivos.Add(new MotivoRechazo(
                            "argumento_invalido",
                            $"{campoPaso}.texto",
                            "la acción «murmurar» requiere el texto que quieres decir"));
                    }

                    break;

                case "esperar":
                    if (paso.DuracionMin is not >= 10)
                    {
                        motivos.Add(new MotivoRechazo(
                            "argumento_invalido",
                            $"{campoPaso}.duracion_min",
                            "la acción «esperar» requiere una duración mínima de 10 minutos de juego"));
                    }

                    break;
            }
        }

        private static void ValidarPasoLugar(
            PasoPlan paso,
            string campoPaso,
            LugaresOfrecidos ofrecidos,
            List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(paso.Lugar))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.lugar",
                    $"la acción «{paso.Accion}» requiere indicar un lugar del léxico"));
                return;
            }

            if (!PerfilValidator.EsIdValido(paso.Lugar))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.lugar",
                    $"«{paso.Lugar}» no es un identificador válido del léxico (no se admiten coordenadas ni rutas)"));
                return;
            }

            var lugar = ofrecidos.ResolverLugar(paso.Lugar);
            if (lugar is null)
            {
                motivos.Add(new MotivoRechazo(
                    "lugar_desconocido",
                    $"{campoPaso}.lugar",
                    $"no conoces ni tienes disponible el lugar «{paso.Lugar}» en esta escena"));
                return;
            }

            if (PerfilValidator.AreaPendiente(lugar) || !TieneAreaValida(lugar))
            {
                motivos.Add(new MotivoRechazo(
                    "lugar_no_alcanzable",
                    $"{campoPaso}.lugar",
                    $"el lugar «{lugar.Prosa}» todavía no tiene un área alcanzable"));
                return;
            }

            if (paso.Accion == "sentarse" && !lugar.Sentable)
            {
                motivos.Add(new MotivoRechazo(
                    "lugar_no_sentable",
                    $"{campoPaso}.lugar",
                    $"no puedes sentarte en «{lugar.Prosa}»"));
            }
        }

        private static void ValidarPasoMirar(
            PasoPlan paso,
            string campoPaso,
            Observacion observacion,
            IReadOnlyCollection<string>? objetivosConocidos,
            List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(paso.Objetivo))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.objetivo",
                    "la acción «mirar» requiere indicar un objetivo"));
                return;
            }

            if (!Acciones.EsRefValida(paso.Objetivo))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.objetivo",
                    $"«{paso.Objetivo}» no es una referencia válida de objetivo (no se admiten coordenadas)"));
                return;
            }

            if (!ObjetivoPresenteEnEscena(paso.Objetivo, observacion, objetivosConocidos))
            {
                motivos.Add(new MotivoRechazo(
                    "objetivo_ausente",
                    $"{campoPaso}.objetivo",
                    $"no ves «{paso.Objetivo}» en lo que te rodea ahora"));
            }
        }

        private static void ValidarPasoDibujar(
            PasoPlan paso,
            string campoPaso,
            IReadOnlyCollection<string>? obrasConocidas,
            List<MotivoRechazo> motivos)
        {
            if (string.IsNullOrWhiteSpace(paso.Obra))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.obra",
                    "la acción «dibujar» requiere indicar «nueva» o el id de una obra"));
                return;
            }

            if (paso.Obra == "nueva")
            {
                return;
            }

            if (!Acciones.EsIdObraValido(paso.Obra))
            {
                motivos.Add(new MotivoRechazo(
                    "argumento_invalido",
                    $"{campoPaso}.obra",
                    $"«{paso.Obra}» no es un identificador de obra válido (usa «nueva» o un id con formato «nombre-0001»)"));
                return;
            }

            if (obrasConocidas is not null && !obrasConocidas.Contains(paso.Obra))
            {
                motivos.Add(new MotivoRechazo(
                    "obra_desconocida",
                    $"{campoPaso}.obra",
                    $"no tienes ninguna obra con el identificador «{paso.Obra}»"));
            }
        }

        private static bool ObjetivoPresenteEnEscena(
            string objetivo,
            Observacion observacion,
            IReadOnlyCollection<string>? objetivosConocidos)
        {
            if (observacion.Entidades.Any(e => string.Equals(e.Ref, objetivo, StringComparison.Ordinal)))
            {
                return true;
            }

            if (observacion.Sucesos is not null
                && observacion.Sucesos.Any(s =>
                    string.Equals(s.Ref, objetivo, StringComparison.Ordinal)
                    || string.Equals(s.ActorRef, objetivo, StringComparison.Ordinal)))
            {
                return true;
            }

            return objetivosConocidos is not null && objetivosConocidos.Contains(objetivo);
        }

        private static bool TieneAreaValida(Lugar lugar)
            => lugar.Area.Count == 4 && lugar.Area[0] >= 0 && lugar.Area[1] >= 0 && lugar.Area[2] > 0 && lugar.Area[3] > 0;

        private static bool EsHoraJuegoValida(int hora)
            => hora >= HoraJuego.InicioDia && hora <= HoraJuego.FinDia && (hora % 100) < 60;

        private static string FormatearHora(int hora) => $"{hora / 100}:{hora % 100:00}";
    }
}
