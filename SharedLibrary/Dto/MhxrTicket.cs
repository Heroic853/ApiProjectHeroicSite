using System;

namespace SharedLibrary.Dto
{
    /// <summary>
    /// Le categorie di segnalazione ammesse, in un posto solo.
    ///
    /// Sia il menu a tendina del Client sia la validazione del server
    /// leggono da qui: aggiungere una categoria nuova significa aggiungere
    /// una riga in questo array, non toccare due file diversi che potrebbero
    /// disallinearsi.
    /// </summary>
    public static class MhxrTicketCategorie
    {
        public const string Bug = "Bug";
        public const string WrongMonster = "Wrong monster in quest";
        public const string Crash = "Crash";
        public const string Multiplayer = "Multiplayer";
        public const string LanguagePatch = "Language patch bug";
        public const string Other = "Other";

        public static readonly string[] Tutte =
        {
            Bug, WrongMonster, Crash, Multiplayer, LanguagePatch, Other
        };

        public static bool EValida(string? categoria) =>
            !string.IsNullOrWhiteSpace(categoria) && Array.IndexOf(Tutte, categoria) >= 0;
    }

    /// <summary>Cosa manda il Client per aprire un nuovo ticket.</summary>
    public class MhxrTicketRequest
    {
        public string Categoria { get; set; } = string.Empty;
        public string Messaggio { get; set; } = string.Empty;
    }

    /// <summary>Cosa manda l'Admin per rispondere a un ticket.</summary>
    public class MhxrTicketRispostaRequest
    {
        public string Risposta { get; set; } = string.Empty;
    }

    /// <summary>
    /// Un ticket come lo vedi tu nella pagina admin.
    ///
    /// A differenza delle recensioni pubbliche, qui l'Autore SI vede:
    /// questo endpoint e' protetto (solo Admin), non pubblico, quindi non
    /// c'e' bisogno di mascherare l'email — anzi ti serve per capire chi ti
    /// scrive quando non e' anonimo.
    /// </summary>
    public class MhxrTicketDto
    {
        public int Id { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public string Messaggio { get; set; } = string.Empty;
        public bool Anonimo { get; set; }
        public string? Autore { get; set; }
        public string Stato { get; set; } = "Aperto";
        public DateTime CreatedAt { get; set; }
        public string? Risposta { get; set; }
        public DateTime? RispostoAt { get; set; }
    }

    /// <summary>
    /// Il ticket come sta sul database (tabella "MhxrTickets").
    /// La tabella si crea con WebApi/Migrations/SQL-manuale-mhxr-tickets.sql
    /// </summary>
    public class MhxrTicket
    {
        public int Id { get; set; }

        public string Categoria { get; set; } = string.Empty;

        public string Messaggio { get; set; } = string.Empty;

        /// <summary>true se chi ha scritto NON era loggato.</summary>
        public bool Anonimo { get; set; } = true;

        /// <summary>
        /// Email (o id Auth0) di chi ha scritto, null se anonimo.
        /// Lo riempie il SERVER leggendo il token, mai il browser.
        /// </summary>
        public string? Autore { get; set; }

        /// <summary>Aperto / Preso in carico / Risposto / Chiuso.</summary>
        public string Stato { get; set; } = "Aperto";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>La risposta scritta dall'admin, null finche' non risponde.</summary>
        public string? Risposta { get; set; }

        /// <summary>Quando e' stata scritta la risposta.</summary>
        public DateTime? RispostoAt { get; set; }

        /// <summary>
        /// Solo per i ticket anonimi: il "biglietto" che il Client salva in
        /// localStorage per ritrovare questo ticket senza un account. Null
        /// per i ticket di chi e' loggato, che si riconosce dal token.
        /// </summary>
        public string? LookupToken { get; set; }

        /// <summary>
        /// Indirizzo IP di chi ha scritto, salvato SOLO per i ticket anonimi:
        /// e' il modo con cui il server applica "un ticket alla volta" a chi
        /// non ha un account da controllare.
        /// </summary>
        public string? IndirizzoIp { get; set; }
    }
}
