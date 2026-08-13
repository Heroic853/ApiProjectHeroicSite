using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Dto
{
    public class Clasification
    {
        public int Id { get; set; }

        /// <summary>Il mostro a cui si riferisce.</summary>
        public string Monster { get; set; } = string.Empty;

        /// <summary>
        /// Il voto rapido, scelto dal menu a tendina (DragonsInfo.Feedbacks).
        /// </summary>
        public string Feedback { get; set; } = string.Empty;

        /// <summary>
        /// La recensione scritta, opzionale: chi ha fretta vota e basta.
        /// Colonna aggiunta a mano su Supabase, vedi
        /// WebApi/Migrations/SQL-manuale-recensioni.sql
        /// </summary>
        public string? Review { get; set; }

        /// <summary>Quando e' stata inserita, per ordinare le recensioni.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Chi l'ha scritta. Lo imposta il SERVER dal token, non il client.
        /// ATTENZIONE: non va mai mostrato in pagina, e' un dato personale:
        /// per il pubblico si usa AuthorLabel.
        /// </summary>
        public string? UserEmail { get; set; }
    }

    /// <summary>
    /// Recensione come viene mostrata ai visitatori.
    ///
    /// E' un tipo separato di proposito: l'entita' Clasification contiene
    /// l'email di chi ha scritto, e quella NON deve uscire dall'API su un
    /// endpoint pubblico. Qui c'e' solo un'etichetta gia' mascherata.
    /// </summary>
    public class ReviewDto
    {
        public int Id { get; set; }
        public string Monster { get; set; } = string.Empty;
        public string Feedback { get; set; } = string.Empty;
        public string? Review { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Nome mostrato, es. "amal***" oppure "Hunter".</summary>
        public string AuthorLabel { get; set; } = "Hunter";
    }
}
