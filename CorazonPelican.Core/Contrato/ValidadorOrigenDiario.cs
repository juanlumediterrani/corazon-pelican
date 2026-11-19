namespace CorazonPelican.Core.Contrato
{
    /// <summary>Comprueba el origen de recuerdos de una respuesta ya validada contra su esquema.</summary>
    public static class ValidadorOrigenDiario
    {
        public static MotivoRechazo? Comprobar(Pulso pulso, Respuesta respuesta)
        {
            var errorEpoca = ValidadorEpoca.Comprobar(respuesta, pulso.Epoca);
            if (errorEpoca != null)
            {
                return errorEpoca;
            }

            if (respuesta.BasadaEn != pulso.Id)
            {
                return new MotivoRechazo("pulso_distinto", "basada_en", "esos episodios pertenecen a otro momento");
            }

            var memoria = respuesta.Memoria;
            if (pulso.EpisodiosDiario != null && memoria?.Recordar?.Count > 0)
            {
                return new MotivoRechazo("origen_ausente", "memoria.recordar", "recuerda qué episodio te dejó esa impresión");
            }

            if (memoria?.Reflexiones == null)
            {
                return null;
            }

            for (int i = 0; i < memoria.Reflexiones.Count; i++)
            {
                var origen = memoria.Reflexiones[i].OrigenDiario;
                for (int j = 0; j < origen.Count; j++)
                {
                    if (pulso.EpisodiosDiario?.ContainsKey(origen[j]) != true)
                    {
                        return new MotivoRechazo("episodio_desconocido", $"memoria.reflexiones[{i}].origen_diario[{j}]",
                            "ese episodio no figura entre los que recuerdas ahora");
                    }
                }
            }

            return null;
        }
    }
}
