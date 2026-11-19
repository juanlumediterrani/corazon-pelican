using System.Collections.Generic;
using System.Linq;
using CorazonPelican.Core.Perfil;

namespace CorazonPelican.Core.Intencion
{
    /// <summary>Una entrada del horario que el juego eligió hoy (la lee el adaptador, CP-011).</summary>
    public sealed record EntradaHorario(int Hora, string Ubicacion, int X, int Y, string? Animacion = null);

    /// <summary>El horario de hoy: su clave (p. ej. <c>fall</c>, <c>Fri</c>) y sus entradas en orden.</summary>
    public sealed record HorarioDia(string Clave, IReadOnlyList<EntradaHorario> Entradas);

    /// <summary>Un hito con lo que la anotación del perfil dice de él (GSS §4.1).</summary>
    public sealed record HitoResuelto(
        string Id,
        string Origen,
        string Forma,
        IReadOnlyList<string> Contexto,
        bool OmitirSiYaPenso,
        IReadOnlyList<string> Cambios);

    /// <summary>
    /// Convierte entradas del horario y planes de intención en hitos, aplicando la primera anotación del
    /// perfil cuyo patrón coincide (y <c>por_defecto</c> para lo que no diga). Los hitos no se listan: salen
    /// del horario del día. Solo <c>despertar</c> y <c>acostarse</c> son del motor.
    /// </summary>
    public static class HitosRutina
    {
        public const string Despertar = "despertar";

        public const string Acostarse = "acostarse";

        public const string OrigenRutina = "rutina";

        public const string OrigenIntencion = "intencion";

        public const string FormaPensamiento = "pensamiento";

        public const string FormaCadena = "cadena";

        public static string IdDe(HorarioDia horario, EntradaHorario entrada) => $"{horario.Clave}:{entrada.Hora}";

        public static HitoResuelto Resolver(HorarioDia horario, EntradaHorario entrada, Rutina rutina)
        {
            var anotacion = BuscarAnotacion(entrada, rutina);
            return Combinar(IdDe(horario, entrada), OrigenRutina, anotacion, rutina.PorDefecto);
        }

        /// <summary>Primera anotación del perfil cuyo patrón coincide con la entrada, o <c>null</c> si aplica <c>por_defecto</c>.</summary>
        public static AnotacionHito? BuscarAnotacion(EntradaHorario entrada, Rutina rutina)
            => rutina.Anotaciones.FirstOrDefault(a => a.Id is not Despertar and not Acostarse && Coincide(a.Si, entrada));

        /// <summary>Hito al terminar un plan pedido por la mente (llegada, en el sentido amplio).</summary>
        public static HitoResuelto DeIntencion(string idPlan, Rutina rutina)
            => Combinar(idPlan, OrigenIntencion, null, rutina.PorDefecto);

        /// <summary><c>despertar</c> o <c>acostarse</c>: siempre cadena, nunca omitible ni cambiable.</summary>
        public static HitoResuelto DelMotor(string id, Rutina rutina)
        {
            var anotacion = rutina.Anotaciones.FirstOrDefault(a => a.Id == id);
            var contexto = anotacion?.Contexto ?? rutina.PorDefecto.Contexto ?? new List<string>();
            return new HitoResuelto(id, OrigenRutina, FormaCadena, contexto, OmitirSiYaPenso: false, new List<string>());
        }

        private static HitoResuelto Combinar(string id, string origen, AnotacionHito? anotacion, AnotacionHito porDefecto)
            => new(
                id,
                origen,
                anotacion?.Forma ?? porDefecto.Forma ?? FormaPensamiento,
                anotacion?.Contexto ?? porDefecto.Contexto ?? new List<string>(),
                anotacion?.OmitirSiYaPenso ?? porDefecto.OmitirSiYaPenso ?? true,
                anotacion?.Cambios ?? porDefecto.Cambios ?? new List<string>());

        private static bool Coincide(PatronEntrada? si, EntradaHorario entrada)
            => si is not null
               && (si.Ubicacion is null || si.Ubicacion == entrada.Ubicacion)
               && (si.Animacion is null || si.Animacion == entrada.Animacion)
               && (si.DesdeHora is null || entrada.Hora >= si.DesdeHora)
               && (si.HastaHora is null || entrada.Hora < si.HastaHora);
    }
}
