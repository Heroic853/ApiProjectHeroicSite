using System.Collections.Generic;
using System.Linq;

namespace SharedLibrary
{
    /// <summary>
    /// Un piano di commissione: nome mostrato al cliente e prezzo in centesimi.
    /// </summary>
    public sealed class CommissionPlan
    {
        public CommissionPlan(string id, string displayName, long amountCents)
        {
            Id = id;
            DisplayName = displayName;
            AmountCents = amountCents;
        }

        /// <summary>Identificativo mandato al server. Non cambiarlo: finisce su Stripe.</summary>
        public string Id { get; }

        /// <summary>Nome mostrato nella card e sulla ricevuta Stripe.</summary>
        public string DisplayName { get; }

        /// <summary>Prezzo in centesimi. Questa e' l'unica fonte di verita'.</summary>
        public long AmountCents { get; }

        /// <summary>Prezzo formattato per la UI, es. "3 €".</summary>
        public string DisplayPrice =>
            AmountCents % 100 == 0
                ? $"{AmountCents / 100} €"
                : $"{AmountCents / 100m:0.00} €";
    }

    /// <summary>
    /// Catalogo dei piani, condiviso tra Client e WebApi.
    ///
    /// IMPORTANTE: i prezzi si cambiano SOLO qui. Il server non si fida piu'
    /// dell'importo mandato dal browser (prima si poteva pagare un mod da 50€
    /// con 1 centesimo manipolando la richiesta), quindi prende il prezzo da qui.
    /// </summary>
    public static class CommissionPlans
    {
        public static readonly CommissionPlan RaumFirst =
            new CommissionPlan("Raum First Faction", "Raum's First Faction", 300);

        public static readonly CommissionPlan RaumSecond =
            new CommissionPlan("Raum Second Faction", "Raum's Second Faction", 1000);

        public static readonly CommissionPlan RaumThirty =
            new CommissionPlan("Raum Thirty Faction", "Raum's Thirty Faction", 1500);

        public static readonly CommissionPlan RaumFourth =
            new CommissionPlan("Raum Fourth Faction", "Raum's Fourth Faction", 5000);

        public static readonly IReadOnlyList<CommissionPlan> All = new[]
        {
            RaumFirst,
            RaumSecond,
            RaumThirty,
            RaumFourth
        };

        /// <summary>
        /// Cerca un piano per Id. Restituisce null se non esiste, cosi' il server
        /// puo' rifiutare la richiesta invece di creare un pagamento inventato.
        /// </summary>
        public static CommissionPlan? Find(string? id) =>
            string.IsNullOrWhiteSpace(id)
                ? null
                : All.FirstOrDefault(p => p.Id == id);
    }
}
