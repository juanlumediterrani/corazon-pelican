using System;
using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Contrato;
using CorazonPelican.Core.Intencion;
using CorazonPelican.Core.Memoria;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Sensorio;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>
    /// Vocabulario cerrado de actos de habla v1 (GSS §8.2, ADS §3.4).
    /// Coincide con las variantes de <c>actos</c> en <c>schemas/respuesta.schema.json</c>.
    /// </summary>
    public static class TiposActo
    {
        public const string Promete = "promete";
        public const string Regala = "regala";
        public const string Cita = "cita";
        public const string Ensena = "ensena";
        public const string Rechaza = "rechaza";
        public const string AceptaRegalo = "acepta_regalo";

        public static readonly IReadOnlyCollection<string> VocabularioV1 = new HashSet<string>(StringComparer.Ordinal)
        {
            Promete,
            Regala,
            Cita,
            Ensena,
            Rechaza,
            AceptaRegalo,
        };
    }

    /// <summary>
    /// Estados de una obra en el portafolio del personaje (GSS §6.3, ADS §3.5, R-08).
    /// </summary>
    public static class EstadosObra
    {
        public const string EnCurso = "en_curso";
        public const string Terminada = "terminada";
        public const string Colgada = "colgada";
        public const string Regalada = "regalada";
        public const string Boceto = "boceto";
        public const string Descartada = "descartada";
        public const string Vendida = "vendida";

        public static readonly IReadOnlyCollection<string> Regalables = new HashSet<string>(StringComparer.Ordinal)
        {
            Terminada,
            Colgada,
        };

        public static readonly IReadOnlyCollection<string> Ensenables = new HashSet<string>(StringComparer.Ordinal)
        {
            EnCurso,
            Terminada,
            Colgada,
            Boceto,
        };

        public static bool EsRegalable(string? estado) => estado is not null && Regalables.Contains(estado);

        public static bool EsEnsenable(string? estado) => estado is not null && Ensenables.Contains(estado);
    }

    /// <summary>Etapas ordenadas de aplicación de un turno de diálogo (ADS §4.1, ADR-0003).</summary>
    public static class EtapasTurno
    {
        public const string Intencion = "intencion";
        public const string Actos = "actos";
        public const string Memoria = "memoria";
        public const string Frase = "frase";
    }

    /// <summary>Consulta determinista de la tabla de apertura del perfil según corazones (GSS §6.4).</summary>
    public static class AperturaPerfil
    {
        private static readonly TramoApertura Cerrado = new()
        {
            DesdeCorazones = 0,
            Ensenar = false,
            Encargos = false,
            Regalar = false,
        };

        /// <summary>Devuelve el <see cref="TramoApertura"/> aplicable a <paramref name="corazones"/> en <paramref name="perfil"/>.</summary>
        public static TramoApertura TramoPara(PerfilPersonaje perfil, int corazones)
        {
            if (perfil is null)
            {
                throw new ArgumentNullException(nameof(perfil));
            }

            return TramoPara(perfil.Apertura, corazones);
        }

        /// <summary>Devuelve el <see cref="TramoApertura"/> aplicable a <paramref name="corazones"/>.</summary>
        public static TramoApertura TramoPara(IReadOnlyList<TramoApertura>? tramos, int corazones)
        {
            if (tramos is null || tramos.Count == 0)
            {
                return Cerrado;
            }

            TramoApertura? elegido = null;
            foreach (var tramo in tramos)
            {
                if (tramo is null || tramo.DesdeCorazones > corazones)
                {
                    continue;
                }

                if (elegido is null || tramo.DesdeCorazones > elegido.DesdeCorazones)
                {
                    elegido = tramo;
                }
            }

            return elegido ?? Cerrado;
        }
    }

    /// <summary>
    /// Contexto del mundo y del personaje sobre el que se validan y aplican los actos de un turno de diálogo.
    /// </summary>
    public sealed record ContextoValidacionActos(
        Observacion Observacion,
        PerfilPersonaje Perfil,
        LugaresOfrecidos? LugaresOfrecidos,
        EstadoPersonaje Estado,
        string? InterlocutorRef = null,
        int? CorazonesInterlocutor = null,
        IReadOnlyCollection<string>? ObjetosRegaloDisponibles = null,
        Pulso? Pulso = null,
        AgendaDia? Agenda = null,
        ContextoRutina? ContextoRutina = null)
    {
        /// <summary>Lugares ofrecidos en la escena, o todos los del perfil si no se filtraron.</summary>
        public LugaresOfrecidos LugaresEfectivos
            => this.LugaresOfrecidos ?? this.Perfil.Lexico.OfrecerLugares(_ => true);

        /// <summary>Corazones efectivos del interlocutor (explícitos o deducidos de la observación vigente).</summary>
        public int CorazonesEfectivos
        {
            get
            {
                if (this.CorazonesInterlocutor is int explicitos)
                {
                    return explicitos;
                }

                if (!string.IsNullOrWhiteSpace(this.InterlocutorRef))
                {
                    var entidad = this.Observacion.Entidades.FirstOrDefault(e =>
                        string.Equals(e.Ref, this.InterlocutorRef, StringComparison.Ordinal));
                    if (entidad?.Corazones is int desdeEscena)
                    {
                        return desdeEscena;
                    }
                }

                return 0;
            }
        }
    }

    /// <summary>Resultado de validar un lote de actos según R-07 («todos válidos o ninguno»).</summary>
    public sealed class ResultadoValidacionActos
    {
        public ResultadoValidacionActos(IReadOnlyList<MotivoRechazo> motivos)
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
    /// Resultado atómico de validar y aplicar un turno de diálogo en el orden
    /// <b>intención → actos → memoria → frase</b> (ADS §4.1, ADR-0003).
    /// </summary>
    public sealed class ResultadoTurnoDialogo
    {
        private ResultadoTurnoDialogo(
            bool aceptado,
            IReadOnlyList<MotivoRechazo> motivos,
            string? fraseVisible,
            IntencionPersonaje? intencionActivada,
            IReadOnlyList<Acto> actosAplicados,
            IReadOnlyList<CompromisoEstado> compromisosCreados,
            IReadOnlyList<ObraPortafolio> obrasRegaladas,
            IReadOnlyList<Recuerdo> recuerdosCreados,
            IReadOnlyList<string> etapasAplicadas)
        {
            this.Aceptado = aceptado;
            this.Motivos = motivos;
            this.FraseVisible = fraseVisible;
            this.IntencionActivada = intencionActivada;
            this.ActosAplicados = actosAplicados;
            this.CompromisosCreados = compromisosCreados;
            this.ObrasRegaladas = obrasRegaladas;
            this.RecuerdosCreados = recuerdosCreados;
            this.EtapasAplicadas = etapasAplicadas;
        }

        public bool Aceptado { get; }

        public IReadOnlyList<MotivoRechazo> Motivos { get; }

        public string? FraseVisible { get; }

        public IntencionPersonaje? IntencionActivada { get; }

        public IReadOnlyList<Acto> ActosAplicados { get; }

        public IReadOnlyList<CompromisoEstado> CompromisosCreados { get; }

        public IReadOnlyList<ObraPortafolio> ObrasRegaladas { get; }

        public IReadOnlyList<Recuerdo> RecuerdosCreados { get; }

        public IReadOnlyList<string> EtapasAplicadas { get; }

        public Resultado AResultadoContrato()
            => this.Aceptado
                ? new Resultado { Estado = Resultado.Aceptada }
                : new Resultado { Estado = Resultado.Rechazada, Motivos = this.Motivos.ToList() };

        public static ResultadoTurnoDialogo Rechazar(IReadOnlyList<MotivoRechazo> motivos)
            => new(
                aceptado: false,
                motivos: motivos,
                fraseVisible: null,
                intencionActivada: null,
                actosAplicados: Array.Empty<Acto>(),
                compromisosCreados: Array.Empty<CompromisoEstado>(),
                obrasRegaladas: Array.Empty<ObraPortafolio>(),
                recuerdosCreados: Array.Empty<Recuerdo>(),
                etapasAplicadas: Array.Empty<string>());

        public static ResultadoTurnoDialogo Aceptar(
            string? fraseVisible,
            IntencionPersonaje? intencionActivada,
            IReadOnlyList<Acto> actosAplicados,
            IReadOnlyList<CompromisoEstado> compromisosCreados,
            IReadOnlyList<ObraPortafolio> obrasRegaladas,
            IReadOnlyList<Recuerdo> recuerdosCreados,
            IReadOnlyList<string> etapasAplicadas)
            => new(
                aceptado: true,
                motivos: Array.Empty<MotivoRechazo>(),
                fraseVisible: fraseVisible,
                intencionActivada: intencionActivada,
                actosAplicados: actosAplicados,
                compromisosCreados: compromisosCreados,
                obrasRegaladas: obrasRegaladas,
                recuerdosCreados: recuerdosCreados,
                etapasAplicadas: etapasAplicadas);
    }
}
