using System;
using System.Collections.Generic;

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

    /// <summary>Chi ha scritto un messaggio dentro un ticket.</summary>
    public static class MhxrMittente
    {
        public const string Utente = "Utente";
        public const string Admin = "Admin";
    }

    /// <summary>Cosa manda il Client per aprire un nuovo ticket.</summary>
    public class MhxrTicketRequest
    {
        public string Categoria { get; set; } = string.Empty;
        public string Messaggio { get; set; } = string.Empty;

        /// <summary>
        /// Facoltativo, solo per chi NON e' loggato: un nome/account con cui
        /// farsi riconoscere senza fare il login vero (utile da dentro la
        /// webview del gioco, dove Auth0 e' fragile). Il server lo IGNORA
        /// per chi ha gia' un token valido: non puo' mai sovrascrivere
        /// un'identita' verificata. Non e' una prova d'identita', e' solo
        /// un'etichetta scelta da chi scrive.
        /// </summary>
        public string? NomeAnonimo { get; set; }
    }

    /// <summary>Cosa manda l'utente o l'admin per aggiungere un messaggio a un ticket gia' aperto.</summary>
    public class MhxrTicketMessaggioRequest
    {
        public string Testo { get; set; } = string.Empty;
    }

    /// <summary>Un singolo messaggio della conversazione di un ticket.</summary>
    public class MhxrTicketMessaggioDto
    {
        public int Id { get; set; }
        public string Mittente { get; set; } = string.Empty;
        public string Testo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// Un ticket con l'intera conversazione. Lo stesso DTO serve sia al
    /// Client (il proprio ticket) sia all'Admin (l'elenco di tutti): qui
    /// l'Autore non viene mai mascherato, e chi lo legge decide da solo
    /// se e' autorizzato in base a quale endpoint ha chiamato.
    /// </summary>
    public class MhxrTicketDto
    {
        public int Id { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public bool Anonimo { get; set; }
        public string? Autore { get; set; }
        public string Stato { get; set; } = "Aperto";
        public DateTime CreatedAt { get; set; }
        public List<MhxrTicketMessaggioDto> Messaggi { get; set; } = new();
    }

    /// <summary>
    /// Il ticket come sta sul database (tabella "MhxrTickets"). I messaggi
    /// (apertura, risposte, richieste di chiarimento) stanno nella tabella
    /// figlia "MhxrTicketMessaggi" (entita' <see cref="MhxrTicketMessaggio"/>),
    /// non qui: prima il primo messaggio e l'unica risposta erano due colonne
    /// fisse su questa riga, ma una conversazione vera puo' avere piu' scambi
    /// in entrambe le direzioni.
    /// La tabella si crea/aggiorna con WebApi/Migrations/SQL-manuale-mhxr-tickets.sql
    /// </summary>
    public class MhxrTicket
    {
        public int Id { get; set; }

        public string Categoria { get; set; } = string.Empty;

        /// <summary>true se chi ha scritto NON era loggato.</summary>
        public bool Anonimo { get; set; } = true;

        /// <summary>
        /// Chi ha scritto. Per chi e' loggato e' l'email (o id Auth0) letta
        /// dal SERVER dal token, mai dal browser: verificata. Per chi non e'
        /// loggato e' invece il "NomeAnonimo" facoltativo scelto da chi
        /// scrive, se l'ha compilato: NON verificato, e' solo un'etichetta.
        /// <see cref="Anonimo"/> e' sempre la fonte di verita' su quale dei
        /// due casi sia — un Autore non-null non significa "verificato".
        /// </summary>
        public string? Autore { get; set; }

        /// <summary>
        /// Aperto (in attesa di te) / Preso in carico / Risposto (in attesa
        /// di lui) / Chiuso. Solo tu puoi chiudere un ticket: lo decide
        /// sempre e solo <see cref="MhxrTicketController.CambiaStato"/>.
        /// </summary>
        public string Stato { get; set; } = "Aperto";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

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

    /// <summary>Un messaggio della conversazione, sulla tabella "MhxrTicketMessaggi".</summary>
    public class MhxrTicketMessaggio
    {
        public int Id { get; set; }

        public int IdTicket { get; set; }

        /// <summary>"Utente" o "Admin" — vedi <see cref="MhxrMittente"/>.</summary>
        public string Mittente { get; set; } = string.Empty;

        public string Testo { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
