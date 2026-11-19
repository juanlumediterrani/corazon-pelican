using System;

namespace CorazonPelican.Framework
{
    /// <summary>Dónde se ejecuta la lógica de un módulo. El mismo DLL se instala en anfitrión y clientes.</summary>
    [Flags]
    public enum LadoModulo
    {
        /// <summary>Lógica autoritativa del motor: solo en el anfitrión (<c>Context.IsMainPlayer</c>).</summary>
        Anfitrion = 1,

        /// <summary>Lógica local o de sincronización en cada cliente.</summary>
        Cliente = 2,

        Ambos = Anfitrion | Cliente,
    }

    /// <summary>
    /// Subsistema independiente del mod. Cada módulo se registra por separado y con su propio <c>try/catch</c>,
    /// de modo que un fallo en uno lo desactiva sin tumbar el resto del mod (patrón de Pelican 4Ever, CP-005).
    /// </summary>
    public interface IModule
    {
        string Nombre { get; }

        LadoModulo Lado { get; }

        /// <summary>Suscribe eventos SMAPI y comandos. Se llama una vez desde <c>ModEntry.Entry</c>.</summary>
        void Registrar(ModContext contexto);
    }
}
