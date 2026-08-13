using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibrary;
using SharedLibrary.Dto;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
using System.Text.Json;
using WebApi.Data;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/dragon")]
    // Di default TUTTO richiede un token Auth0 valido.
    // Gli endpoint pubblici sono marcati esplicitamente con [AllowAnonymous].
    [Authorize]
    public class DragonController : ControllerBase
    {
        private readonly DragonListDbContext _dragonListDbContext;
        private readonly ILogger<DragonController> _logger;
        private readonly IConfiguration _config;

        public DragonController(
            ILogger<DragonController> logger,
            DragonListDbContext dragonListDbContext,
            IConfiguration config)
        {
            _logger = logger;
            _dragonListDbContext = dragonListDbContext;
            _config = config;
        }

        /// <summary>
        /// Legge un segreto: prima la variabile d'ambiente (Render), poi la
        /// configurazione (in locale i user secrets). Vedi Program.cs.
        /// </summary>
        private string? Secret(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnv
                ? fromEnv
                : _config[name];

        /// <summary>
        /// Chi ha fatto la richiesta, ricavato dal token Auth0.
        ///
        /// ATTENZIONE: Auth0 mette "email" nell'ID token, NON nell'access token
        /// che arriva a questa API. Per avere l'email qui serve una Action nel
        /// tenant che aggiunga un claim custom, esattamente come si fa gia' per
        /// i ruoli ("https://heroic853.github.io/roles"):
        ///
        ///   api.accessToken.setCustomClaim("https://heroic853.github.io/email", event.user.email);
        ///
        /// Finche' quella Action non c'e', ripiega sull'id utente Auth0 (sub),
        /// che c'e' sempre: meglio "auth0|abc123" che null.
        /// </summary>
        private string? CurrentUserIdentifier()
        {
            var email = User.FindFirst("https://heroic853.github.io/email")?.Value
                        ?? User.FindFirst(ClaimTypes.Email)?.Value
                        ?? User.FindFirst("email")?.Value;

            if (!string.IsNullOrWhiteSpace(email))
                return email;

            var sub = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? User.FindFirst("sub")?.Value;

            if (!string.IsNullOrWhiteSpace(sub))
            {
                _logger.LogWarning(
                    "Il token non contiene l'email: salvo l'id Auth0 ({Sub}). " +
                    "Aggiungi una Action che metta il claim custom email nell'access token.", sub);
                return sub;
            }

            return null;
        }

        // ------------------------------------------------------------------
        // DRAGHI
        // ------------------------------------------------------------------

        [HttpGet] // leggere
        public async Task<IEnumerable<Dragon>> Get()
        {
            // AsNoTracking: EF non tiene traccia delle entita' per il change
            // tracking, che su una lista in sola lettura e' lavoro sprecato.
            var dragons = await _dragonListDbContext.DragonSet.AsNoTracking().ToListAsync();
            _logger.LogInformation("Restituiti {Count} draghi", dragons.Count);
            return dragons;
        }

        [HttpPost] // scrivere
        public async Task<IActionResult> Post([FromBody] Dragon dragon)
        {
            // L'autore lo decide il server dal token, non il client: prima
            // restava sempre null (nessuno lo riempiva) e non si sapeva chi
            // avesse inserito cosa.
            dragon.UserEmail = CurrentUserIdentifier();

            await _dragonListDbContext.DragonSet.AddAsync(dragon);
            await _dragonListDbContext.SaveChangesAsync();
            _logger.LogInformation("Nuovo drago salvato: {Name} (da {User})", dragon.Name, dragon.UserEmail);
            return Ok(new { message = "Dragon created" });
        }

        // ------------------------------------------------------------------
        // UTENTI
        // ------------------------------------------------------------------

        [HttpPost("register")]
        [AllowAnonymous] // la registrazione avviene prima di avere un token
        public async Task<IActionResult> Register([FromBody] User user)
        {
            try
            {
                // Un solo giro sul database invece di due query separate
                var clash = await _dragonListDbContext.User
                    .AsNoTracking()
                    .Where(u => u.Account == user.Account || u.Username == user.Username)
                    .Select(u => new { u.Account, u.Username })
                    .FirstOrDefaultAsync();

                if (clash != null)
                {
                    if (clash.Account == user.Account)
                    {
                        _logger.LogInformation("Registrazione rifiutata: email gia' presente");
                        return BadRequest(new { message = "Account (email) already exists" });
                    }

                    _logger.LogInformation("Registrazione rifiutata: username gia' presente");
                    return BadRequest(new { message = "Username already exists" });
                }

                await _dragonListDbContext.User.AddAsync(user);
                await _dragonListDbContext.SaveChangesAsync();

                _logger.LogInformation("Nuovo utente registrato: {Username}", user.Username);
                return Ok(new { message = "Registration successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la registrazione");
                return StatusCode(500, new { message = "Registration failed" });
            }
        }

        [HttpGet("users")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                // NON restituire mai la password: prima questo endpoint la
                // mandava in chiaro a chiunque la chiedesse.
                var users = await _dragonListDbContext.User
                    .AsNoTracking()
                    .Select(u => new
                    {
                        u.Id,
                        u.Username,
                        u.Account,
                        u.RegistrationDate
                    })
                    .ToListAsync();

                _logger.LogInformation("Admin ha letto la lista utenti ({Count} righe)", users.Count);
                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel caricamento utenti");
                return StatusCode(500, new { message = "Failed to load users" });
            }
        }

        [HttpGet("get-user")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetUser([FromQuery] string account)
        {
            var user = await _dragonListDbContext.User
                .AsNoTracking()
                .Where(u => u.Account == account)
                .Select(u => new { u.Id, u.Username, u.Account, u.RegistrationDate })
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }

        [HttpGet("user-profile")]
        public async Task<IActionResult> GetUserProfile([FromQuery] string username)
        {
            try
            {
                var user = await _dragonListDbContext.User
                    .AsNoTracking()
                    .Where(u => u.Username == username)
                    .Select(u => new
                    {
                        u.Username,
                        u.Account,
                        u.RegistrationDate
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel caricamento del profilo utente");
                return StatusCode(500, new { message = "Failed to load user profile" });
            }
        }

        // API per eliminare account
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                         ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userId))
            {
                _logger.LogWarning("delete-account chiamato senza claim 'sub' nel token");
                return Unauthorized();
            }

            var domain = Secret("AUTH0_DOMAIN");
            var clientId = Secret("AUTH0_M2M_CLIENT_ID");
            var clientSecret = Secret("AUTH0_M2M_CLIENT_SECRET");

            if (string.IsNullOrWhiteSpace(domain)
                || string.IsNullOrWhiteSpace(clientId)
                || string.IsNullOrWhiteSpace(clientSecret))
            {
                _logger.LogError("Cancellazione account impossibile: variabili AUTH0_M2M_* non configurate");
                return StatusCode(503, new { message = "Account deletion is not configured" });
            }

            try
            {
                _logger.LogInformation("Richiesta cancellazione account per {UserId}", userId);

                // Un solo HttpClient con using: prima ne creava due senza mai
                // liberarli, esaurendo i socket sotto carico.
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

                // 1. Token Machine-to-Machine per la Management API
                var tokenResponse = await http.PostAsJsonAsync(
                    $"https://{domain}/oauth/token",
                    new
                    {
                        client_id = clientId,
                        client_secret = clientSecret,
                        audience = $"https://{domain}/api/v2/",
                        grant_type = "client_credentials"
                    });

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Auth0 ha rifiutato il token M2M: {Status}", tokenResponse.StatusCode);
                    return StatusCode(502, new { message = "Could not authenticate with Auth0" });
                }

                var tokenData = await tokenResponse.Content.ReadFromJsonAsync<JsonElement>();
                if (!tokenData.TryGetProperty("access_token", out var tokenProp))
                {
                    _logger.LogError("Risposta token Auth0 senza access_token");
                    return StatusCode(502, new { message = "Unexpected Auth0 response" });
                }

                // 2. Cancella l'utente da Auth0
                using var deleteRequest = new HttpRequestMessage(
                    HttpMethod.Delete,
                    $"https://{domain}/api/v2/users/{Uri.EscapeDataString(userId)}");
                deleteRequest.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenProp.GetString());

                var deleteResponse = await http.SendAsync(deleteRequest);

                if (!deleteResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Auth0 non ha cancellato l'utente {UserId}: {Status}",
                        userId, deleteResponse.StatusCode);
                    return StatusCode(502, new { message = "Failed to delete user from Auth0" });
                }

                _logger.LogInformation("Account {UserId} cancellato da Auth0", userId);
                return Ok(new { message = "Account deleted" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante la cancellazione dell'account {UserId}", userId);
                return StatusCode(500, new { message = "Error deleting account" });
            }
        }

        // ------------------------------------------------------------------
        // CLASSIFICA / FEEDBACK
        // ------------------------------------------------------------------

        [HttpPost("Clasification")]
        public async Task<IActionResult> Clasification([FromBody] Clasification clasification)
        {
            // Stesso discorso del voto sui draghi: chi ha votato lo ricava il
            // server dal token, cosi' non e' falsificabile dal browser.
            clasification.UserEmail = CurrentUserIdentifier();

            await _dragonListDbContext.Clasification.AddAsync(clasification);
            await _dragonListDbContext.SaveChangesAsync();
            _logger.LogInformation("Nuovo feedback salvato su {Monster} (da {User})",
                clasification.Monster, clasification.UserEmail);
            return Ok(new { message = "Feedback saved" });
        }

        [HttpGet("Clasification")]
        public async Task<IEnumerable<Clasification>> GetAniversary()
        {
            return await _dragonListDbContext.Clasification.AsNoTracking().ToListAsync();
        }

        // ------------------------------------------------------------------
        // STATISTICHE VISITE
        // ------------------------------------------------------------------

        /// <summary>
        /// Registra una visita. Deve restare pubblico: la maggior parte dei
        /// visitatori non e' loggata, ed e' proprio quel traffico che vogliamo contare.
        /// </summary>
        [HttpPost("log-visit")]
        [AllowAnonymous]
        public async Task<IActionResult> LogVisit()
        {
            try
            {
                var visit = new PageVisit
                {
                    VisitedAt = DateTime.UtcNow,
                    // Resta null per i visitatori anonimi: e' normale ed e'
                    // proprio il traffico che ci interessa contare.
                    UserEmail = User.Identity?.IsAuthenticated == true
                        ? CurrentUserIdentifier()
                        : null
                };

                await _dragonListDbContext.PageVisits.AddAsync(visit);
                await _dragonListDbContext.SaveChangesAsync();

                _logger.LogInformation("Visita registrata ({Who})",
                    visit.UserEmail is null ? "anonimo" : "utente loggato");
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore nel salvataggio della visita");
                return StatusCode(500);
            }
        }

        /// <summary>
        /// Visite raggruppate per giorno, con i giorni a zero inclusi.
        ///
        /// Prima restituiva solo i giorni che avevano almeno una visita: il grafico
        /// univa 13 marzo e 11 giugno con una retta, come se fosse traffico continuo.
        /// Ora restituisce ogni giorno della finestra richiesta, cosi' la linea
        /// mostra il traffico reale (zeri compresi).
        /// </summary>
        [HttpGet("daily-stats")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> GetDailyStats([FromQuery] int days = 30)
        {
            // Limita la finestra: senza questo un ?days=100000 farebbe generare
            // centomila punti al server e al grafico.
            days = Math.Clamp(days, 1, 365);

            // DateTime.UtcNow.Date restituisce Kind=Unspecified, ma la colonna
            // visited_at e' "timestamp with time zone": Npgsql rifiuta un
            // parametro non-UTC con ArgumentException. SpecifyKind lo marca come UTC.
            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            var from = today.AddDays(-(days - 1));

            // Il raggruppamento lo fa Postgres, non il server: torna una riga
            // per giorno invece di tutte le visite.
            var raw = await _dragonListDbContext.PageVisits
                .AsNoTracking()
                .Where(v => v.VisitedAt >= from)
                .GroupBy(v => v.VisitedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();

            var byDay = raw.ToDictionary(r => r.Date, r => r.Count);

            // Riempi i buchi con zero, giorno per giorno
            var stats = Enumerable.Range(0, days)
                .Select(offset =>
                {
                    var date = from.AddDays(offset);
                    return new VisitStat
                    {
                        Date = date,
                        Count = byDay.TryGetValue(date, out var count) ? count : 0
                    };
                })
                .ToList();

            _logger.LogInformation(
                "Statistiche visite: {Days} giorni, {Total} visite totali, ultimo giorno con traffico: {Last}",
                days,
                stats.Sum(s => s.Count),
                stats.LastOrDefault(s => s.Count > 0)?.Date.ToString("yyyy-MM-dd") ?? "nessuno");

            return Ok(stats);
        }

        // ------------------------------------------------------------------
        // PAGAMENTI STRIPE
        // ------------------------------------------------------------------

        [HttpPost("create-checkout")]
        [AllowAnonymous] // si compra anche senza essere registrati
        public async Task<IActionResult> CreateCheckout([FromBody] CheckoutRequest request)
        {
            if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
            {
                _logger.LogError("create-checkout chiamato ma STRIPE_SECRET_KEY non e' configurata");
                return StatusCode(503, new { message = "Payments are temporarily unavailable" });
            }

            // Il prezzo lo decide il server, non il browser. Prima l'importo
            // arrivava dal client: si poteva comprare il piano da 50€ per 1 centesimo.
            var plan = CommissionPlans.Find(request?.PlanName);
            if (plan == null)
            {
                _logger.LogWarning("create-checkout con piano sconosciuto: {Plan}", request?.PlanName ?? "(null)");
                return BadRequest(new { message = "Unknown plan" });
            }

            if (request!.AmountCents != 0 && request.AmountCents != plan.AmountCents)
            {
                _logger.LogWarning(
                    "Importo manipolato per {Plan}: il client chiedeva {Sent} centesimi, uso {Real}",
                    plan.Id, request.AmountCents, plan.AmountCents);
            }

            try
            {
                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string> { "card" },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                Currency = "eur",
                                UnitAmount = plan.AmountCents,
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = plan.DisplayName
                                }
                            },
                            Quantity = 1
                        }
                    },
                    Mode = "payment",

                    // {CHECKOUT_SESSION_ID} lo sostituisce Stripe al redirect.
                    // Senza questo la pagina di conferma non sapeva quale
                    // pagamento mostrare e finiva sempre in errore.
                    SuccessUrl = "https://heroic853.github.io/Heroic853SiteV1/payment-success?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl = "https://heroic853.github.io/Heroic853SiteV1/commissions"
                };

                var session = await new SessionService().CreateAsync(options);

                _logger.LogInformation(
                    "Sessione Stripe creata per {Plan} ({Amount} centesimi): {SessionId}",
                    plan.Id, plan.AmountCents, session.Id);

                return Ok(new { url = session.Url });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex, "Stripe ha rifiutato la creazione della sessione per {Plan}", plan.Id);
                return StatusCode(502, new { message = "Payment provider error" });
            }
        }

        /// <summary>
        /// Dettagli di una sessione di pagamento, per la pagina /payment-success.
        ///
        /// Questo endpoint mancava del tutto: la pagina lo chiamava e riceveva 404,
        /// quindi dopo ogni pagamento riuscito il cliente vedeva la schermata di errore.
        /// </summary>
        [HttpGet("get-session")]
        [AllowAnonymous]
        public async Task<IActionResult> GetSession([FromQuery] string sessionId)
        {
            if (string.IsNullOrWhiteSpace(sessionId))
                return BadRequest(new { message = "sessionId is required" });

            if (string.IsNullOrWhiteSpace(StripeConfiguration.ApiKey))
            {
                _logger.LogError("get-session chiamato ma STRIPE_SECRET_KEY non e' configurata");
                return StatusCode(503, new { message = "Payments are temporarily unavailable" });
            }

            try
            {
                var session = await new SessionService().GetAsync(
                    sessionId,
                    new SessionGetOptions { Expand = new List<string> { "payment_intent" } });

                // Non confermare nulla se il pagamento non e' andato a buon fine
                if (session.PaymentStatus != "paid")
                {
                    _logger.LogWarning("get-session per {SessionId}: stato pagamento '{Status}'",
                        sessionId, session.PaymentStatus);
                    return NotFound(new { message = "Payment not completed" });
                }

                var paymentMethod = session.PaymentMethodTypes?.FirstOrDefault() ?? "card";
                var date = session.Created == default ? DateTime.UtcNow : session.Created;

                _logger.LogInformation("get-session OK per {SessionId} ({Amount} centesimi)",
                    sessionId, session.AmountTotal);

                return Ok(new
                {
                    Amount = (session.AmountTotal ?? 0) / 100m,
                    ModName = ResolveModName(session),
                    TransactionId = session.PaymentIntentId ?? session.Id,
                    PaymentMethod = paymentMethod == "card" ? "Credit Card" : paymentMethod,
                    Date = date
                });
            }
            catch (StripeException ex)
            {
                _logger.LogWarning(ex, "Stripe non ha trovato la sessione {SessionId}", sessionId);
                return NotFound(new { message = "Session not found" });
            }
        }

        /// <summary>
        /// Ricava il nome del prodotto dalla sessione, con fallback sul catalogo
        /// in base all'importo se Stripe non ha espanso le line items.
        /// </summary>
        private static string ResolveModName(Session session)
        {
            var byAmount = CommissionPlans.All
                .FirstOrDefault(p => p.AmountCents == (session.AmountTotal ?? 0));

            return byAmount?.DisplayName ?? "Monster Hunter Commission";
        }
    }
}
