namespace CorazonPelican.Core.Contrato
{
    /// <summary>
    /// Primer paso de la validación de respuestas (ADS §4.1): una respuesta de otra carga de mundo
    /// se rechaza, porque sus ids de pulso y de obra pueden coincidir con los nuevos (P-1).
    /// </summary>
    public static class ValidadorEpoca
    {
        public const string CodigoEpocaObsoleta = "epoca_obsoleta";

        /// <summary>Devuelve el motivo de rechazo, o <c>null</c> si la respuesta es de la época vigente.</summary>
        public static MotivoRechazo? Comprobar(Respuesta respuesta, string epocaVigente)
        {
            if (!string.IsNullOrEmpty(respuesta.Epoca) && respuesta.Epoca == epocaVigente)
            {
                return null;
            }

            return new MotivoRechazo(
                CodigoEpocaObsoleta,
                "epoca",
                "eso lo pensaste en otro momento del mundo: lo que veías ya no es lo que hay");
        }
    }
}
