using System;
using System.Collections.Generic;
using CorazonPelican.Core.Perfil;
using CorazonPelican.Core.Tiempo;

namespace CorazonPelican.Core.Config
{
    /// <summary>
    /// Resultado de cruzar los personajes configurados con los perfiles válidos cargados (CP-005).
    /// </summary>
    public sealed record ResultadoSeleccionPersonajes(
        IReadOnlyDictionary<string, PerfilPersonaje> Gobernados,
        IReadOnlyList<string> SinPerfil);

    /// <summary>Fallo capturado al registrar un módulo independiente.</summary>
    public sealed record FalloModulo(string Nombre, Exception Error);

    /// <summary>Resultado del registro aislado de módulos.</summary>
    public sealed record ResultadoRegistroModulos(
        IReadOnlyList<string> Activos,
        IReadOnlyList<FalloModulo> Fallidos);

    /// <summary>
    /// Registra una secuencia de módulos con <c>try/catch</c> individual para que el fallo de uno
    /// lo desactive sin tumbar el resto del mod (patrón de Pelican 4Ever, CP-005).
    /// </summary>
    public static class RegistroModulos
    {
        public static ResultadoRegistroModulos RegistrarTodos<TModulo>(
            IEnumerable<TModulo> modulos,
            Func<TModulo, string> obtenerNombre,
            Action<TModulo> registrar,
            Action<string>? alRegistrar = null,
            Action<FalloModulo>? alFallar = null)
        {
            var activos = new List<string>();
            var fallidos = new List<FalloModulo>();

            foreach (var modulo in modulos)
            {
                var nombre = obtenerNombre(modulo);
                try
                {
                    registrar(modulo);
                    activos.Add(nombre);
                    alRegistrar?.Invoke(nombre);
                }
                catch (Exception ex)
                {
                    var fallo = new FalloModulo(nombre, ex);
                    fallidos.Add(fallo);
                    alFallar?.Invoke(fallo);
                }
            }

            return new ResultadoRegistroModulos(activos, fallidos);
        }
    }

    /// <summary>
    /// Configuración del mod serializada en <c>config.json</c> (ADS §12.1).
    /// El núcleo permanece agnóstico de personajes concretos: el adaptador inicializa el personaje por defecto
    /// al crear un <c>config.json</c> nuevo.
    /// </summary>
    public class CorazonConfig
    {
        public const int PuertoMinimo = 1024;
        public const int PuertoMaximo = 65535;
        public const string ValidacionMencionesRechazar = "rechazar";
        public const string ValidacionMencionesAviso = "aviso";

        public List<string> Personajes { get; set; } = new();

        public bool MenteHabilitada { get; set; } = true;

        public bool ModoDesarrollo { get; set; } = false;

        public int ApiPuerto { get; set; } = 25560;

        public string ApiToken { get; set; } = string.Empty;

        public int SensorioCadenciaTicks { get; set; } = 15;

        public string ModoReloj { get; set; } = "auto";

        public int TiempoMaxRespuestaMs { get; set; } = 8000;

        public bool AnticipacionHabilitada { get; set; } = true;

        public int TamanoMaxMemoriaKB { get; set; } = 512;

        public List<string> ValleyTalkDesactivadoPara { get; set; } = new();

        public string PelicanApiUrl { get; set; } = string.Empty;

        public string PelicanApiToken { get; set; } = string.Empty;

        public bool Telemetria { get; set; } = true;

        public string ModoValidacionMenciones { get; set; } = ValidacionMencionesRechazar;

        /// <summary>Valida la configuración y devuelve los errores legibles en español (lista vacía si es válida).</summary>
        public IReadOnlyList<string> Validate()
        {
            var errores = new List<string>();

            this.ValidarPersonajes(errores);

            if (this.ApiPuerto is < PuertoMinimo or > PuertoMaximo)
            {
                errores.Add($"{nameof(this.ApiPuerto)} debe situarse entre {PuertoMinimo} y {PuertoMaximo} (actual: {this.ApiPuerto}).");
            }

            if (this.SensorioCadenciaTicks <= 0)
            {
                errores.Add($"{nameof(this.SensorioCadenciaTicks)} debe ser mayor que 0 (actual: {this.SensorioCadenciaTicks}).");
            }

            if (!EsModoRelojValido(this.ModoReloj))
            {
                errores.Add($"{nameof(this.ModoReloj)} debe ser 'auto', 'vanilla' o 'pelican' (actual: '{this.ModoReloj}').");
            }

            if (this.TiempoMaxRespuestaMs <= 0)
            {
                errores.Add($"{nameof(this.TiempoMaxRespuestaMs)} debe ser mayor que 0 (actual: {this.TiempoMaxRespuestaMs}).");
            }

            if (this.TamanoMaxMemoriaKB <= 0)
            {
                errores.Add($"{nameof(this.TamanoMaxMemoriaKB)} debe ser mayor que 0 (actual: {this.TamanoMaxMemoriaKB}).");
            }

            if (this.ValleyTalkDesactivadoPara is null)
            {
                errores.Add($"{nameof(this.ValleyTalkDesactivadoPara)} no puede ser null.");
            }
            else
            {
                for (var i = 0; i < this.ValleyTalkDesactivadoPara.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(this.ValleyTalkDesactivadoPara[i]))
                    {
                        errores.Add($"{nameof(this.ValleyTalkDesactivadoPara)}[{i}] no puede estar vacío.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(this.PelicanApiUrl))
            {
                if (!Uri.TryCreate(this.PelicanApiUrl, UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    errores.Add($"{nameof(this.PelicanApiUrl)} debe ser una URL HTTP/HTTPS absoluta válida (actual: '{this.PelicanApiUrl}').");
                }
            }

            if (!EsModoValidacionMencionesValido(this.ModoValidacionMenciones))
            {
                errores.Add($"{nameof(this.ModoValidacionMenciones)} debe ser 'rechazar' o 'aviso' (actual: '{this.ModoValidacionMenciones}').");
            }

            return errores;
        }

        /// <summary>Resuelve el modo de reloj efectivo a partir de <see cref="ModoReloj"/> y la presencia de Pelican 4Ever.</summary>
        public Tiempo.ModoReloj ResolverModoReloj(bool pelicanPresente)
        {
            if (string.Equals(this.ModoReloj, "pelican", StringComparison.OrdinalIgnoreCase))
            {
                return Tiempo.ModoReloj.Pelican;
            }

            if (string.Equals(this.ModoReloj, "vanilla", StringComparison.OrdinalIgnoreCase))
            {
                return Tiempo.ModoReloj.Vanilla;
            }

            return pelicanPresente ? Tiempo.ModoReloj.Pelican : Tiempo.ModoReloj.Vanilla;
        }

        /// <summary>
        /// Cruza los personajes configurados con los perfiles válidos cargados, señalando los que faltan o son inválidos.
        /// </summary>
        public ResultadoSeleccionPersonajes SeleccionarPersonajes(IReadOnlyDictionary<string, PerfilPersonaje> perfilesValidos)
        {
            var gobernados = new Dictionary<string, PerfilPersonaje>(StringComparer.Ordinal);
            var sinPerfil = new List<string>();

            foreach (var id in this.Personajes)
            {
                if (perfilesValidos.TryGetValue(id, out var perfil))
                {
                    gobernados[id] = perfil;
                }
                else
                {
                    sinPerfil.Add(id);
                }
            }

            return new ResultadoSeleccionPersonajes(gobernados, sinPerfil);
        }

        private void ValidarPersonajes(List<string> errores)
        {
            if (this.Personajes is null || this.Personajes.Count == 0)
            {
                errores.Add($"{nameof(this.Personajes)} debe incluir al menos un id de personaje.");
                return;
            }

            var vistos = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < this.Personajes.Count; i++)
            {
                var id = this.Personajes[i];
                if (!PerfilValidator.EsIdValido(id))
                {
                    errores.Add($"{nameof(this.Personajes)}[{i}] («{id}») no es un id válido: minúsculas, dígitos y «_», empezando por letra.");
                }
                else if (!vistos.Add(id))
                {
                    errores.Add($"{nameof(this.Personajes)} contiene el id duplicado «{id}».");
                }
            }
        }

        private static bool EsModoRelojValido(string? modo) =>
            string.Equals(modo, "auto", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(modo, "vanilla", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(modo, "pelican", StringComparison.OrdinalIgnoreCase);

        private static bool EsModoValidacionMencionesValido(string? modo) =>
            string.Equals(modo, ValidacionMencionesRechazar, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(modo, ValidacionMencionesAviso, StringComparison.OrdinalIgnoreCase);
    }
}
