using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Dto;
using System.Security.Claims;
using WebApi.Data;
using WebApi.Services;

namespace WebApi.Controllers
{
    /// <summary>
    /// Ticket di segnalazione per il server MHXR, con la loro conversazione.
    ///
    /// Sta in un controller a parte e non dentro DragonController perche'
    /// quello ha gia' superato le 800 righe e non c'entra niente col resto.
    /// </summary>
    [ApiController]
    [Route("api/mhxr")]
    [Authorize] // di default serve il token; l'apertura ticket e' esplicitamente pubblica
    public class MhxrTicketController : ControllerBase
    {
        /// <summary>Oltre questa lunghezza un messaggio (apertura o risposta) viene rifiutato.</summary>
        private const int MaxLunghezzaMessaggio = 4000;

        /// <summary>Oltre questa lunghezza il nome facoltativo di chi non e' loggato viene rifiutato.</summary>
        private const int MaxLunghezzaNomeAnonimo = 60;

        /// <summary>Quanti ticket puo' aprire lo stesso indirizzo in un'ora.</summary>
        private const int MaxTicketPerOra = 5;

        private readonly DragonListDbContext _db;
        private readonly ILogger<MhxrTicketController> _logger;
        private readonly EmailService _email;

        public MhxrTicketController(
            DragonListDbContext db,
            ILogger<MhxrTicketController> logger,
            EmailService email)
        {
            _db = db;
            _logger = logger;
            _email = email;
        }

        // ------------------------------------------------------------------
        // FRENO ANTI-ABUSO
        // ------------------------------------------------------------------
        //
        // L'apertura ticket e' pubblica: senza un freno, chiunque puo'
        // riempire la tabella con uno script. Qui si tiene in memoria quando
        // ha scritto ogni indirizzo nell'ultima ora.
        //
        // LIMITE NOTO: sta in memoria, quindi si azzera a ogni riavvio del
        // container (su Render capita spesso). Ferma lo spam accidentale e i
        // doppi click, non un attacco deciso. Per quello servirebbe una
        // tabella sul database o un servizio davanti.

        private static readonly Dictionary<string, List<DateTime>> _apertureRecenti = new();
        private static readonly object _lockAperture = new();

        private static bool TroppiTicketDa(string indirizzo)
        {
            var adesso = DateTime.UtcNow;
            var unOraFa = adesso.AddHours(-1);

            lock (_lockAperture)
            {
                if (!_apertureRecenti.TryGetValue(indirizzo, out var quando))
                {
                    quando = new List<DateTime>();
                    _apertureRecenti[indirizzo] = quando;
                }

                // Butta via quelle vecchie, cosi' la lista non cresce all'infinito
                quando.RemoveAll(q => q < unOraFa);

                if (quando.Count >= MaxTicketPerOra)
                    return true;

                quando.Add(adesso);

                // Ogni tanto ripulisce gli indirizzi che non scrivono piu',
                // altrimenti il dizionario cresce per sempre
                if (_apertureRecenti.Count > 500)
                {
                    var morti = _apertureRecenti
                        .Where(k => k.Value.Count == 0 || k.Value.Max() < unOraFa)
                        .Select(k => k.Key)
                        .ToList();

                    foreach (var morto in morti)
                        _apertureRecenti.Remove(morto);
                }

                return false;
            }
        }

        /// <summary>
        /// Chi ha aperto il ticket, letto dal token Auth0.
        ///
        /// Stessa logica di DragonController: Auth0 mette "email" nell'ID
        /// token e NON nell'access token, quindi serve una Action nel tenant
        /// che aggiunga il claim custom. Senza, ripiega sull'id utente (sub).
        /// </summary>
        private string? AutoreDalToken()
        {
            if (User.Identity?.IsAuthenticated != true)
                return null;

            return User.FindFirst("https://heroic853.github.io/email")?.Value
                   ?? User.FindFirst(ClaimTypes.Email)?.Value
                   ?? User.FindFirst("email")?.Value
                   ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? User.FindFirst("sub")?.Value;
        }

        /// <summary>Carica un ticket con l'intera conversazione, in ordine di data.</summary>
        private async Task<MhxrTicketDto> CaricaDtoAsync(MhxrTicket ticket)
        {
            var messaggi = await _db.MhxrTicketMessaggi
                .AsNoTracking()
                .Where(m => m.IdTicket == ticket.Id)
                .OrderBy(m => m.CreatedAt)
                .Select(m => new MhxrTicketMessaggioDto
                {
                    Id = m.Id,
                    Mittente = m.Mittente,
                    Testo = m.Testo,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return new MhxrTicketDto
            {
                Id = ticket.Id,
                Categoria = ticket.Categoria,
                Anonimo = ticket.Anonimo,
                Autore = ticket.Autore,
                Stato = ticket.Stato,
                CreatedAt = ticket.CreatedAt,
                Messaggi = messaggi,
                LookupToken = ticket.LookupToken
            };
        }

        /// <summary>
        /// Carica una lista di ticket con le loro conversazioni in due sole
        /// query (una per i ticket, una per TUTTI i loro messaggi), invece di
        /// una query per ogni ticket: evita N+1 chiamate al database quando
        /// la lista e' lunga.
        /// </summary>
        private async Task<List<MhxrTicketDto>> CaricaListaDtoAsync(IQueryable<MhxrTicket> query)
        {
            var tickets = await query.ToListAsync();

            var idTicket = tickets.Select(t => t.Id).ToList();
            var messaggiPerTicket = await _db.MhxrTicketMessaggi
                .AsNoTracking()
                .Where(m => idTicket.Contains(m.IdTicket))
                .OrderBy(m => m.CreatedAt)
                .ToListAsync();

            return tickets.Select(t => new MhxrTicketDto
            {
                Id = t.Id,
                Categoria = t.Categoria,
                Anonimo = t.Anonimo,
                Autore = t.Autore,
                Stato = t.Stato,
                CreatedAt = t.CreatedAt,
                Messaggi = messaggiPerTicket
                    .Where(m => m.IdTicket == t.Id)
                    .Select(m => new MhxrTicketMessaggioDto
                    {
                        Id = m.Id,
                        Mittente = m.Mittente,
                        Testo = m.Testo,
                        CreatedAt = m.CreatedAt
                    })
                    .ToList(),
                LookupToken = t.LookupToken
            }).ToList();
        }

        // ------------------------------------------------------------------
        // APERTURA TICKET (pubblica)
        // ------------------------------------------------------------------

        /// <summary>
        /// Apre un ticket (e il suo primo messaggio). Pubblico di proposito:
        /// chi gioca sul server MHXR spesso non ha un account sul sito, e
        /// obbligarlo a registrarsi per segnalare un crash vorrebbe dire non
        /// ricevere mai la segnalazione.
        ///
        /// Se pero' e' loggato, il server registra chi e': il campo Anonimo
        /// lo decide il TOKEN, non quello che manda il browser. Chi non e'
        /// loggato puo' scrivere un nome facoltativo (richiesta.NomeAnonimo)
        /// solo per farsi riconoscere: resta comunque "Anonimo", perche'
        /// quel nome non e' verificato.
        /// </summary>
        [HttpPost("ticket")]
        [AllowAnonymous]
        public async Task<IActionResult> ApriTicket([FromBody] MhxrTicketRequest richiesta)
        {
            if (richiesta is null)
                return BadRequest(new { message = "Empty request" });

            // La categoria deve essere una di quelle previste: non ci si fida di
            // quello che arriva dal browser, che potrebbe mandare qualsiasi cosa
            if (!MhxrTicketCategorie.EValida(richiesta.Categoria))
            {
                _logger.LogWarning("Ticket MHXR con categoria non valida: {Categoria}", richiesta.Categoria);
                return BadRequest(new { message = "Choose a valid category" });
            }

            var messaggio = richiesta.Messaggio?.Trim();

            if (string.IsNullOrWhiteSpace(messaggio))
                return BadRequest(new { message = "Write a message before sending" });

            if (messaggio.Length > MaxLunghezzaMessaggio)
                return BadRequest(new { message = $"Message too long (max {MaxLunghezzaMessaggio} characters)" });

            var indirizzo = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";
            if (TroppiTicketDa(indirizzo))
            {
                _logger.LogWarning("Troppi ticket MHXR dallo stesso indirizzo, respinto");
                return StatusCode(429, new { message = "Too many tickets sent. Please try again later." });
            }

            var autoreToken = AutoreDalToken();
            var isAnonimo = autoreToken is null;

            // Solo per chi NON e' loggato: un'etichetta scelta da lui,
            // facoltativa, NON verificata (chiunque puo' scrivere un nome a
            // caso). Per chi e' loggato viene ignorata di proposito: non
            // deve mai poter scavalcare l'identita' vera letta dal token.
            var nomeAnonimo = isAnonimo ? richiesta.NomeAnonimo?.Trim() : null;
            if (!string.IsNullOrEmpty(nomeAnonimo) && nomeAnonimo.Length > MaxLunghezzaNomeAnonimo)
            {
                return BadRequest(new
                {
                    message = $"Name too long (max {MaxLunghezzaNomeAnonimo} characters)"
                });
            }

            var autoreDaSalvare = isAnonimo ? (string.IsNullOrEmpty(nomeAnonimo) ? null : nomeAnonimo) : autoreToken;

            // Un solo ticket aperto alla volta: SOLO l'admin puo' chiuderlo
            // (vedi CambiaStato), quindi questo e' anche l'unico modo in cui
            // se ne libera uno. Chi e' loggato si riconosce dall'Autore
            // (verificato); chi non lo e' non ha un'identita' stabile —
            // anche se ha scritto un nome, non e' verificato — quindi si usa
            // l'indirizzo IP: piu' debole (una rete condivisa puo' bloccare
            // piu' persone), ma senza account e' la sola cosa con cui il
            // server puo' riconoscerlo.
            var haGiaUnTicketAperto = !isAnonimo
                ? await _db.MhxrTickets.AsNoTracking().AnyAsync(t => t.Autore == autoreToken && t.Stato != "Chiuso")
                : await _db.MhxrTickets.AsNoTracking().AnyAsync(t => t.Anonimo && t.IndirizzoIp == indirizzo && t.Stato != "Chiuso");

            if (haGiaUnTicketAperto)
            {
                return Conflict(new
                {
                    message = "You already have an open ticket. Wait for me to close it before sending a new one."
                });
            }

            // Solo i ticket anonimi ricevono un "biglietto": e' l'unico modo
            // con cui chi non e' loggato puo' dimostrare in seguito che quel
            // ticket e' suo (il Client lo salva in localStorage). Chi e'
            // loggato non ne ha bisogno, si riconosce dal token.
            var lookupToken = isAnonimo ? Guid.NewGuid().ToString("N") : null;

            var ticket = new MhxrTicket
            {
                Categoria = richiesta.Categoria,
                Anonimo = isAnonimo,
                Autore = autoreDaSalvare,
                Stato = "Aperto",
                CreatedAt = DateTime.UtcNow,
                LookupToken = lookupToken,
                IndirizzoIp = isAnonimo ? indirizzo : null
            };

            try
            {
                await _db.MhxrTickets.AddAsync(ticket);
                await _db.SaveChangesAsync();

                await _db.MhxrTicketMessaggi.AddAsync(new MhxrTicketMessaggio
                {
                    IdTicket = ticket.Id,
                    Mittente = MhxrMittente.Utente,
                    Testo = messaggio,
                    CreatedAt = ticket.CreatedAt
                });
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Il caso piu' probabile: le tabelle non sono ancora state create
                _logger.LogError(ex,
                    "Salvataggio ticket MHXR fallito. Se le tabelle non esistono, lancia " +
                    "WebApi/Migrations/SQL-manuale-mhxr-tickets.sql su Supabase.");
                return StatusCode(500, new { message = "Could not save your ticket. Please try again later." });
            }

            _logger.LogInformation(
                "TICKET MHXR #{Id} aperto: {Categoria} ({Chi}, {Lunghezza} caratteri)",
                ticket.Id, ticket.Categoria,
                ticket.Anonimo ? "anonimo" : ticket.Autore,
                messaggio.Length);

            // La mail e' un di piu': se fallisce, il ticket resta comunque salvato
            await _email.SendMhxrTicketNotificationAsync(
                ticket.Id, ticket.Categoria, messaggio,
                ticket.Anonimo, ticket.Autore, ticket.CreatedAt);

            return Ok(new
            {
                id = ticket.Id,
                anonimo = ticket.Anonimo,
                lookupToken,
                message = "Ticket sent"
            });
        }

        // ------------------------------------------------------------------
        // IL MIO TICKET (utente loggato, non admin)
        // ------------------------------------------------------------------

        /// <summary>
        /// L'ultimo ticket aperto da CHI CHIAMA (letto dal token, mai da un
        /// id passato dal Client), con l'intera conversazione. Il Client lo
        /// usa per decidere se mostrare il modulo "nuovo ticket" oppure lo
        /// stato di quello gia' in corso. Null se l'utente non ha mai
        /// scritto nulla.
        /// </summary>
        [HttpGet("mio-ticket")]
        [Authorize]
        public async Task<IActionResult> GetMioTicket()
        {
            var autore = AutoreDalToken();
            if (autore is null)
                return Ok((MhxrTicketDto?)null);

            var ticket = await _db.MhxrTickets
                .AsNoTracking()
                .Where(t => t.Autore == autore)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            if (ticket is null)
                return Ok((MhxrTicketDto?)null);

            return Ok(await CaricaDtoAsync(ticket));
        }

        /// <summary>
        /// Aggiunge un messaggio a un TUO ticket ancora aperto. Non e' un
        /// modo per chiuderlo: solo l'admin puo' farlo (vedi CambiaStato).
        /// Se il ticket era "Risposto" torna "Aperto": e' di nuovo il tuo
        /// turno di scrivergli, quindi ora aspetta lui.
        /// </summary>
        [HttpPost("ticket/{id:int}/messaggi")]
        [Authorize]
        public async Task<IActionResult> AggiungiMioMessaggio(int id, [FromBody] MhxrTicketMessaggioRequest richiesta)
        {
            var autore = AutoreDalToken();
            if (autore is null)
                return Forbid();

            var testo = richiesta?.Testo?.Trim();
            if (string.IsNullOrWhiteSpace(testo))
                return BadRequest(new { message = "Write a message before sending" });

            if (testo.Length > MaxLunghezzaMessaggio)
                return BadRequest(new { message = $"Message too long (max {MaxLunghezzaMessaggio} characters)" });

            var ticket = await _db.MhxrTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null || ticket.Autore != autore)
                return NotFound(new { message = "Ticket not found" });

            if (ticket.Stato == "Chiuso")
                return BadRequest(new { message = "This ticket is closed. Open a new one if you still need help." });

            await _db.MhxrTicketMessaggi.AddAsync(new MhxrTicketMessaggio
            {
                IdTicket = ticket.Id,
                Mittente = MhxrMittente.Utente,
                Testo = testo
            });
            ticket.Stato = "Aperto";
            await _db.SaveChangesAsync();

            _logger.LogInformation("Ticket MHXR #{Id}: nuovo messaggio dell'utente {Autore}", id, autore);

            await _email.SendMhxrTicketFollowUpNotificationAsync(ticket.Id, ticket.Categoria, testo, ticket.Anonimo ? "anonimo" : autore);

            return Ok(await CaricaDtoAsync(ticket));
        }

        /// <summary>
        /// I TUOI ticket gia' chiusi (l'attivo, se c'e', lo da' mio-ticket).
        /// Serve per lo storico in fondo alla pagina: "cosa avevo scritto le
        /// altre volte e come e' andata a finire".
        /// </summary>
        [HttpGet("mio-storico")]
        [Authorize]
        public async Task<IActionResult> GetMioStorico()
        {
            var autore = AutoreDalToken();
            if (autore is null)
                return Ok(new List<MhxrTicketDto>());

            var query = _db.MhxrTickets
                .AsNoTracking()
                .Where(t => t.Autore == autore && t.Stato == "Chiuso")
                .OrderByDescending(t => t.CreatedAt)
                .Take(20);

            return Ok(await CaricaListaDtoAsync(query));
        }

        // ------------------------------------------------------------------
        // IL MIO TICKET, VERSIONE ANONIMA (col "biglietto" invece del token)
        // ------------------------------------------------------------------

        /// <summary>
        /// Legge lo stato di un ticket anonimo, con l'intera conversazione.
        /// Al posto del token (che chi non e' loggato non ha) serve il
        /// "lookupToken" ricevuto alla creazione: senza quello, o se il
        /// ticket non e' anonimo, risponde 404 in entrambi i casi — cosi'
        /// non si scopre nemmeno se un id esiste, indovinando token a caso.
        /// </summary>
        [HttpGet("ticket-anonimo/{id:int}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTicketAnonimo(int id, [FromQuery] string? token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return NotFound(new { message = "Ticket not found" });

            var ticket = await _db.MhxrTickets.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null || !ticket.Anonimo || ticket.LookupToken != token)
                return NotFound(new { message = "Ticket not found" });

            return Ok(await CaricaDtoAsync(ticket));
        }

        /// <summary>
        /// Aggiunge un messaggio a un ticket anonimo ancora aperto, stesso
        /// controllo col "biglietto" di sopra. Anche qui, chi scrive non
        /// puo' chiudere il ticket: solo aprire, scrivere e aspettare.
        /// </summary>
        [HttpPost("ticket-anonimo/{id:int}/messaggi")]
        [AllowAnonymous]
        public async Task<IActionResult> AggiungiMessaggioAnonimo(
            int id, [FromQuery] string? token, [FromBody] MhxrTicketMessaggioRequest richiesta)
        {
            if (string.IsNullOrWhiteSpace(token))
                return NotFound(new { message = "Ticket not found" });

            var testo = richiesta?.Testo?.Trim();
            if (string.IsNullOrWhiteSpace(testo))
                return BadRequest(new { message = "Write a message before sending" });

            if (testo.Length > MaxLunghezzaMessaggio)
                return BadRequest(new { message = $"Message too long (max {MaxLunghezzaMessaggio} characters)" });

            var ticket = await _db.MhxrTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null || !ticket.Anonimo || ticket.LookupToken != token)
                return NotFound(new { message = "Ticket not found" });

            if (ticket.Stato == "Chiuso")
                return BadRequest(new { message = "This ticket is closed. Open a new one if you still need help." });

            await _db.MhxrTicketMessaggi.AddAsync(new MhxrTicketMessaggio
            {
                IdTicket = ticket.Id,
                Mittente = MhxrMittente.Utente,
                Testo = testo
            });
            ticket.Stato = "Aperto";
            await _db.SaveChangesAsync();

            _logger.LogInformation("Ticket MHXR #{Id}: nuovo messaggio anonimo", id);

            await _email.SendMhxrTicketFollowUpNotificationAsync(ticket.Id, ticket.Categoria, testo, "anonimo");

            return Ok(await CaricaDtoAsync(ticket));
        }

        /// <summary>
        /// Storico dei ticket anonimi GIA' CHIUSI aperti dallo stesso
        /// indirizzo IP di chi chiama — senza "biglietto", perche' dopo un
        /// nuovo ticket il Client sovrascrive quello salvato in localStorage
        /// e quelli vecchi non sarebbero piu' raggiungibili altrimenti.
        ///
        /// COMPROMESSO DA CONOSCERE: una rete condivisa (WiFi pubblico,
        /// scuola, ufficio) mostra lo storico di chiunque abbia scritto da
        /// quella stessa rete, non solo il tuo. Per un modulo di feedback
        /// senza dati sensibili e' un compromesso accettabile — lo stesso
        /// giа accettato per il limite "un ticket alla volta" — ma va saputo.
        /// </summary>
        [HttpGet("storico-anonimo")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStoricoAnonimo()
        {
            var indirizzo = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "sconosciuto";

            var query = _db.MhxrTickets
                .AsNoTracking()
                .Where(t => t.Anonimo && t.IndirizzoIp == indirizzo && t.Stato == "Chiuso")
                .OrderByDescending(t => t.CreatedAt)
                .Take(20);

            var risultato = await CaricaListaDtoAsync(query);

            // Qui il chiamante ha dimostrato solo di scrivere dallo stesso IP,
            // non di possedere il biglietto di OGNI ticket restituito (vedi il
            // commento sul compromesso, sopra): il token e' una credenziale
            // vera, non va consegnato a chi potrebbe non essere il proprietario.
            foreach (var t in risultato)
                t.LookupToken = null;

            return Ok(risultato);
        }

        // ------------------------------------------------------------------
        // LETTURA E GESTIONE (solo tu)
        // ------------------------------------------------------------------

        /// <summary>
        /// L'elenco dei ticket con la loro conversazione. Solo Admin: qui
        /// l'autore NON viene mascherato (a differenza delle recensioni
        /// pubbliche), perche' l'endpoint e' protetto e ti serve sapere chi
        /// ti ha scritto.
        /// </summary>
        [HttpGet("tickets")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetTickets(
            [FromQuery] string? stato = null,
            [FromQuery] int limit = 100)
        {
            limit = Math.Clamp(limit, 1, 500);

            var query = _db.MhxrTickets.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(stato))
                query = query.Where(t => t.Stato == stato);

            query = query.OrderByDescending(t => t.CreatedAt).Take(limit);

            var risultato = await CaricaListaDtoAsync(query);

            _logger.LogInformation("Admin ha letto {Count} ticket MHXR", risultato.Count);
            return Ok(risultato);
        }

        /// <summary>
        /// Cambia lo stato di un ticket. E' il SOLO modo in cui un ticket
        /// puo' chiudersi: l'utente non ha un endpoint equivalente, di
        /// proposito.
        /// </summary>
        [HttpPost("tickets/{id:int}/stato")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CambiaStato(int id, [FromBody] string nuovoStato)
        {
            var ammessi = new[] { "Aperto", "Preso in carico", "Risposto", "Chiuso" };
            if (!ammessi.Contains(nuovoStato))
                return BadRequest(new { message = "Stato non valido" });

            var ticket = await _db.MhxrTickets.FindAsync(id);
            if (ticket is null)
                return NotFound(new { message = "Ticket non trovato" });

            ticket.Stato = nuovoStato;
            await _db.SaveChangesAsync();

            _logger.LogInformation("Ticket MHXR #{Id} passato a {Stato}", id, nuovoStato);
            return Ok(new { message = "Stato aggiornato" });
        }

        /// <summary>
        /// Aggiunge la tua risposta alla conversazione. Imposta lo stato su
        /// "Risposto" da solo (e' di nuovo il turno dell'utente): non chiude
        /// il ticket, per quello c'e' CambiaStato. Se chi ha aperto il
        /// ticket ha un'email riconoscibile (non e' anonimo), gli arriva
        /// anche una notifica — se fallisce non blocca comunque il salvataggio.
        /// </summary>
        [HttpPost("tickets/{id:int}/rispondi")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> Rispondi(int id, [FromBody] MhxrTicketMessaggioRequest richiesta)
        {
            var risposta = richiesta?.Testo?.Trim();
            if (string.IsNullOrWhiteSpace(risposta))
                return BadRequest(new { message = "Write a reply before sending" });

            var ticket = await _db.MhxrTickets.FirstOrDefaultAsync(t => t.Id == id);
            if (ticket is null)
                return NotFound(new { message = "Ticket non trovato" });

            await _db.MhxrTicketMessaggi.AddAsync(new MhxrTicketMessaggio
            {
                IdTicket = ticket.Id,
                Mittente = MhxrMittente.Admin,
                Testo = risposta
            });
            ticket.Stato = "Risposto";
            await _db.SaveChangesAsync();

            _logger.LogInformation("Ticket MHXR #{Id} risposto", id);

            // Solo indirizzi che sembrano davvero un'email: AutoreDalToken puo'
            // ripiegare su un id Auth0 (il "sub") quando il token non ha il
            // claim email, e quello non e' un indirizzo a cui scrivere.
            if (!ticket.Anonimo && ticket.Autore is { } autore && autore.Contains('@'))
            {
                await _email.SendMhxrTicketAnsweredNotificationAsync(
                    ticket.Id, ticket.Categoria, risposta, autore);
            }

            return Ok(await CaricaDtoAsync(ticket));
        }
    }
}
