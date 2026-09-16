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
    /// Ticket di segnalazione per il server MHXR.
    ///
    /// Sta in un controller a parte e non dentro DragonController perche'
    /// quello ha gia' superato le 800 righe e non c'entra niente col resto.
    /// </summary>
    [ApiController]
    [Route("api/mhxr")]
    [Authorize] // di default serve il token; l'apertura ticket e' esplicitamente pubblica
    public class MhxrTicketController : ControllerBase
    {
        /// <summary>Oltre questa lunghezza il messaggio viene rifiutato.</summary>
        private const int MaxLunghezzaMessaggio = 4000;

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

        // ------------------------------------------------------------------
        // APERTURA TICKET (pubblica)
        // ------------------------------------------------------------------

        /// <summary>
        /// Apre un ticket. Pubblico di proposito: chi gioca sul server MHXR
        /// spesso non ha un account sul sito, e obbligarlo a registrarsi per
        /// segnalare un crash vorrebbe dire non ricevere mai la segnalazione.
        ///
        /// Se pero' e' loggato, il server registra chi e': il campo Anonimo
        /// lo decide il TOKEN, non quello che manda il browser.
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

            var autore = AutoreDalToken();

            var ticket = new MhxrTicket
            {
                Categoria = richiesta.Categoria,
                Messaggio = messaggio,
                Anonimo = autore is null,
                Autore = autore,
                Stato = "Aperto",
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _db.MhxrTickets.AddAsync(ticket);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Il caso piu' probabile: la tabella non e' ancora stata creata
                _logger.LogError(ex,
                    "Salvataggio ticket MHXR fallito. Se la tabella non esiste, lancia " +
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
                ticket.Id, ticket.Categoria, ticket.Messaggio,
                ticket.Anonimo, ticket.Autore, ticket.CreatedAt);

            return Ok(new
            {
                id = ticket.Id,
                anonimo = ticket.Anonimo,
                message = "Ticket sent"
            });
        }

        // ------------------------------------------------------------------
        // LETTURA (solo tu)
        // ------------------------------------------------------------------

        /// <summary>
        /// L'elenco dei ticket. Solo Admin: qui l'autore NON viene mascherato
        /// (a differenza delle recensioni pubbliche), perche' l'endpoint e'
        /// protetto e ti serve sapere chi ti ha scritto.
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

            var tickets = await query
                .OrderByDescending(t => t.CreatedAt)
                .Take(limit)
                .Select(t => new MhxrTicketDto
                {
                    Id = t.Id,
                    Categoria = t.Categoria,
                    Messaggio = t.Messaggio,
                    Anonimo = t.Anonimo,
                    Autore = t.Autore,
                    Stato = t.Stato,
                    CreatedAt = t.CreatedAt
                })
                .ToListAsync();

            _logger.LogInformation("Admin ha letto {Count} ticket MHXR", tickets.Count);
            return Ok(tickets);
        }

        /// <summary>Cambia lo stato di un ticket.</summary>
        [HttpPost("tickets/{id:int}/stato")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> CambiaStato(int id, [FromBody] string nuovoStato)
        {
            var ammessi = new[] { "Aperto", "Preso in carico", "Chiuso" };
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
    }
}
