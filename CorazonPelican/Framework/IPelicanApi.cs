using System;

namespace CorazonPelican.Framework
{
    /// <summary>
    /// Subconjunto de la API SMAPI de Pelican 4Ever (<c>Pelican4Ever.SVRTMP</c>) consumido por
    /// Corazón Pelican mediante <c>Helper.ModRegistry.GetApi&lt;IPelicanApi&gt;</c> (CP-604).
    /// </summary>
    public interface IPelicanApi
    {
        void RegisterPreSaveHook(Action callback);

        void UnregisterPreSaveHook(Action callback);

        bool IsNightHold();

        bool IsRolloverBusy();
    }
}
