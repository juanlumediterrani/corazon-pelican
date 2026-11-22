using System;
using System.Collections.Generic;
using System.Linq;

namespace CorazonPelican.Core.Dialogo
{
    /// <summary>Proyección mínima de un mod cargado en SMAPI para evaluar convivencia sin depender de SMAPI (CP-505).</summary>
    public sealed record ModInstaladoResumen(string UniqueId, string Nombre, string Autor);

    /// <summary>
    /// Regla de convivencia con ValleyTalk (GSS §8.6, ADS §11.3, R-12, CP-505):
    /// nunca dos voces para la misma persona. No lee ni modifica la configuración de ValleyTalk.
    /// </summary>
    public static class ConvivenciaValleyTalk
    {
        public const string UniqueIdOficial = "dandm1.ValleyTalk";

        private static readonly string[] IdsConocidos =
        {
            UniqueIdOficial,
            "ValleyTalk",
            "purrplingcat.ValleyTalk",
            "Satozaki.ValleyTalk",
        };

        public static bool EsModValleyTalk(ModInstaladoResumen? mod)
        {
            if (mod is null)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(mod.UniqueId)
                && IdsConocidos.Any(id => string.Equals(id, mod.UniqueId.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(mod.Nombre)
                && mod.Nombre.IndexOf("ValleyTalk", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(mod.Autor)
                && mod.Autor.IndexOf("dandm1", StringComparison.OrdinalIgnoreCase) >= 0
                && !string.IsNullOrWhiteSpace(mod.Nombre)
                && mod.Nombre.IndexOf("Valley", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static ModInstaladoResumen? DetectarValleyTalk(IEnumerable<ModInstaladoResumen>? modsCargados)
            => modsCargados?.FirstOrDefault(EsModValleyTalk);

        public static bool EstaDesactivadoPara(
            string personajeId,
            string npcName,
            IEnumerable<string>? valleyTalkDesactivadoPara)
        {
            if (valleyTalkDesactivadoPara is null)
            {
                return false;
            }

            foreach (var entrada in valleyTalkDesactivadoPara)
            {
                if (string.IsNullOrWhiteSpace(entrada))
                {
                    continue;
                }

                var limpia = entrada.Trim();
                if ((!string.IsNullOrWhiteSpace(personajeId) && string.Equals(limpia, personajeId.Trim(), StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(npcName) && string.Equals(limpia, npcName.Trim(), StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool DebeCederAVanilla(
            string personajeId,
            string npcName,
            IEnumerable<ModInstaladoResumen>? modsCargados,
            IEnumerable<string>? valleyTalkDesactivadoPara)
        {
            var valleyTalk = DetectarValleyTalk(modsCargados);
            if (valleyTalk is null)
            {
                return false;
            }

            return !EstaDesactivadoPara(personajeId, npcName, valleyTalkDesactivadoPara);
        }

        public static string ConstruirAvisoConsola(string personajeId, string npcName, string? uniqueIdDetectado = null)
        {
            var nombreVisible = string.IsNullOrWhiteSpace(npcName) ? personajeId : npcName;
            var modId = string.IsNullOrWhiteSpace(uniqueIdDetectado) ? UniqueIdOficial : uniqueIdDetectado;
            return $"[Convivencia:{personajeId}] Detectado ValleyTalk ({modId}). " +
                   $"Para evitar dos voces sobre {nombreVisible} (R-12), Corazón Pelican cede el diálogo a vanilla hasta que " +
                   $"desactives a {nombreVisible} en ValleyTalk y añadas \"{personajeId}\" a la clave " +
                   $"\"ValleyTalkDesactivadoPara\" en config.json.";
        }
    }
}
