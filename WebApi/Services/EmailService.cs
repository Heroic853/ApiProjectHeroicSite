using System.Net.Http.Json;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace WebApi.Services
{
    /// <summary>
    /// Invio email tramite SendGrid: ricevuta al cliente, notifica di vendita
    /// e report giornaliero delle visite.
    ///
    /// NOTA sugli stili: le email usano CSS scritto INLINE su ogni tag.
    /// Gmail e la maggior parte dei client scartano i blocchi &lt;style&gt;,
    /// quindi un template che si affida alle classi arriva senza formattazione
    /// (era il caso della versione precedente di questo file).
    /// </summary>
    public class EmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;
        private readonly IHttpClientFactory _httpFactory;

        public EmailService(
            IConfiguration config,
            ILogger<EmailService> logger,
            IHttpClientFactory httpFactory)
        {
            _config = config;
            _logger = logger;
            _httpFactory = httpFactory;
        }

        // Variabile d'ambiente (Render) oppure configurazione (user secrets in locale)
        private string? Secret(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnv
                ? fromEnv
                : _config[name];

        // ------------------------------------------------------------------
        // QUALE FORNITORE USARE
        // ------------------------------------------------------------------
        //
        // Si sceglie con le variabili d'ambiente, senza toccare il codice:
        //   BREVO_API_KEY    impostata -> usa Brevo
        //   SENDGRID_API_KEY impostata -> usa SendGrid
        // Se ci sono entrambe vince Brevo.
        //
        // Perche' due: il piano gratuito di SendGrid e' un trial di 60 giorni,
        // scaduto il 26/04/2026, e da allora l'API risponde 401 con
        // "Maximum credits exceeded". Avere l'alternativa pronta evita di
        // restare a piedi se un fornitore cambia le condizioni.

        // SMTP: la via piu' semplice, nessun servizio esterno da registrare.
        // Con Gmail: host smtp.gmail.com, porta 587, e come password una
        // "password per le app" generata dall'account Google (NON la password
        // normale: Google non la accetta piu').
        private string? SmtpHost => Secret("EMAIL_SMTP_HOST");
        private string? SmtpUser => Secret("EMAIL_SMTP_USER");
        private string? SmtpPassword => Secret("EMAIL_SMTP_PASSWORD");
        private int SmtpPort => int.TryParse(Secret("EMAIL_SMTP_PORT"), out var p) ? p : 587;

        private string? BrevoKey => Secret("BREVO_API_KEY");

        private string? SendGridKey => Secret("SENDGRID_API_KEY") ?? _config["SendGrid:ApiKey"];

        private bool UsaSmtp =>
            !string.IsNullOrWhiteSpace(SmtpHost) &&
            !string.IsNullOrWhiteSpace(SmtpUser) &&
            !string.IsNullOrWhiteSpace(SmtpPassword);

        private bool UsaBrevo => !string.IsNullOrWhiteSpace(BrevoKey);

        /// <summary>
        /// Mittente. Deve essere un indirizzo VERIFICATO in SendGrid
        /// (Settings -> Sender Authentication), altrimenti SendGrid rifiuta con 403.
        /// </summary>
        private string FromEmail => Secret("MAIL_FROM") ?? "heroic853@gmail.com";

        /// <summary>Dove arrivano le notifiche di vendita e i report.</summary>
        private string? NotifyEmail => Secret("NOTIFY_EMAIL") ?? Secret("MAIL_FROM") ?? "heroic853@gmail.com";

        public bool IsConfigured => UsaSmtp || UsaBrevo || !string.IsNullOrWhiteSpace(SendGridKey);

        /// <summary>Nome del fornitore attivo, per i log di avvio.</summary>
        public string Fornitore =>
            UsaSmtp ? $"SMTP ({SmtpHost}:{SmtpPort})"
            : UsaBrevo ? "Brevo"
            : !string.IsNullOrWhiteSpace(SendGridKey) ? "SendGrid"
            : "nessuno";

        // ------------------------------------------------------------------
        // RICEVUTA AL CLIENTE
        // ------------------------------------------------------------------

        public Task<bool> SendPaymentReceiptAsync(
            string toEmail, string planName, decimal amount,
            string transactionId, DateTime date)
        {
            var body = Wrapper($"""
                {Heading("Payment Confirmed", "Your order has been processed successfully.")}
                {Badge("● CONFIRMED", "#1a7f37")}
                {AmountBox(amount)}
                {Row("Product", planName)}
                {Row("Date", date.ToString("dd/MM/yyyy HH:mm 'UTC'"))}
                {Row("Transaction ID", transactionId, mono: true)}
                {Row("Payment method", "Credit card")}
                <p style="margin:24px 0 0;color:#c0c0c0;font-size:13px;line-height:1.6;">
                  Thank you for your purchase! Keep this email as your receipt.<br />
                  For support, reply to this email quoting your Transaction ID.
                </p>
                """);

            return SendAsync(toEmail, $"Payment confirmed — {planName}", body, "ricevuta cliente");
        }

        // ------------------------------------------------------------------
        // NOTIFICA DI VENDITA (a te)
        // ------------------------------------------------------------------

        public Task<bool> SendSaleNotificationAsync(
            string planName, decimal amount, string transactionId,
            string? customerEmail, DateTime date)
        {
            var body = Wrapper($"""
                {Heading("Nuova vendita", "Qualcuno ha completato un pagamento sul sito.")}
                {Badge("● PAGATO", "#1a7f37")}
                {AmountBox(amount)}
                {Row("Prodotto", planName)}
                {Row("Cliente", customerEmail ?? "email non fornita")}
                {Row("Data", date.ToString("dd/MM/yyyy HH:mm 'UTC'"))}
                {Row("Transaction ID", transactionId, mono: true)}
                <p style="margin:24px 0 0;color:#c0c0c0;font-size:13px;line-height:1.6;">
                  Ricordati di consegnare la mod all'indirizzo del cliente.
                </p>
                """);

            if (string.IsNullOrWhiteSpace(NotifyEmail))
            {
                _logger.LogWarning("NOTIFY_EMAIL non impostata: notifica di vendita non inviata");
                return Task.FromResult(false);
            }

            return SendAsync(NotifyEmail, $"Vendita: {planName} — {amount:0.00} EUR", body, "notifica vendita");
        }

        // ------------------------------------------------------------------
        // REPORT GIORNALIERO DELLE VISITE
        // ------------------------------------------------------------------

        public Task<bool> SendDailyVisitsReportAsync(
            DateTime giorno, int totale, int anonime, int loggate, int utentiDistinti)
        {
            var percAnonime = totale == 0 ? 0 : (int)Math.Round(anonime * 100.0 / totale);

            var body = Wrapper($"""
                {Heading("Visite del sito", giorno.ToString("dddd d MMMM yyyy"))}
                {AmountBoxRaw(totale.ToString(), totale == 1 ? "visita" : "visite")}
                {Row("Anonime", $"{anonime} ({percAnonime}%)")}
                {Row("Con login", $"{loggate} ({100 - percAnonime}%)")}
                {Row("Utenti diversi loggati", utentiDistinti.ToString())}
                <p style="margin:24px 0 0;color:#c0c0c0;font-size:13px;line-height:1.6;">
                  {(totale == 0
                      ? "Nessuna visita registrata in questa giornata."
                      : "Il grafico completo lo trovi nella pagina admin del sito.")}
                </p>
                """);

            if (string.IsNullOrWhiteSpace(NotifyEmail))
            {
                _logger.LogWarning("NOTIFY_EMAIL non impostata: report giornaliero non inviato");
                return Task.FromResult(false);
            }

            return SendAsync(
                NotifyEmail,
                $"Visite del {giorno:dd/MM/yyyy}: {totale}",
                body,
                "report giornaliero");
        }

        // ------------------------------------------------------------------
        // INVIO
        // ------------------------------------------------------------------

        private async Task<bool> SendAsync(string to, string subject, string html, string tipo)
        {
            if (!IsConfigured)
            {
                _logger.LogWarning(
                    "Email non configurate (serve SMTP, BREVO_API_KEY o SENDGRID_API_KEY): {Tipo} non inviata a {To}",
                    tipo, to);
                return false;
            }

            try
            {
                // Ordine di preferenza: SMTP, poi Brevo, poi SendGrid.
                // Basta impostare le variabili di uno dei tre.
                if (UsaSmtp) return await InviaConSmtpAsync(to, subject, html, tipo);
                if (UsaBrevo) return await InviaConBrevoAsync(to, subject, html, tipo);
                return await InviaConSendGridAsync(to, subject, html, tipo);
            }
            catch (Exception ex)
            {
                // Un errore di invio non deve mai far fallire la richiesta
                // chiamante: un pagamento riuscito resta riuscito anche se la
                // ricevuta non parte.
                _logger.LogError(ex, "Errore nell'invio della {Tipo} a {To} tramite {Fornitore}",
                    tipo, to, Fornitore);
                return false;
            }
        }

        /// <summary>
        /// Invio tramite un server SMTP normale.
        ///
        /// E' la via piu' semplice: nessun servizio da registrare, nessuna
        /// verifica del mittente. Con Gmail servono:
        ///   EMAIL_SMTP_HOST     smtp.gmail.com
        ///   EMAIL_SMTP_PORT     587
        ///   EMAIL_SMTP_USER     heroic853@gmail.com
        ///   EMAIL_SMTP_PASSWORD la "password per le app" di Google, 16 caratteri
        ///
        /// La password normale dell'account NON funziona: Google ha smesso di
        /// accettarla. La password per le app si genera solo con la verifica in
        /// due passaggi attiva.
        ///
        /// Limite Gmail: circa 500 destinatari al giorno, piu' che sufficiente.
        ///
        /// Nota: si usa SmtpClient di .NET per non aggiungere pacchetti al
        /// progetto. Microsoft lo considera superato in favore di MailKit, ma
        /// con Gmail su porta 587 e STARTTLS funziona senza problemi. Se un
        /// domani serve un server piu' esigente, la sostituzione riguarda solo
        /// questo metodo.
        /// </summary>
        private async Task<bool> InviaConSmtpAsync(string to, string subject, string html, string tipo)
        {
            using var smtp = new System.Net.Mail.SmtpClient(SmtpHost, SmtpPort)
            {
                EnableSsl = true, // su 587 significa STARTTLS
                Credentials = new System.Net.NetworkCredential(SmtpUser, SmtpPassword),
                DeliveryMethod = System.Net.Mail.SmtpDeliveryMethod.Network,
                Timeout = 20_000
            };

            using var messaggio = new System.Net.Mail.MailMessage
            {
                // Molti server rifiutano un mittente diverso dall'utente
                // autenticato: si usa SmtpUser, non FromEmail.
                From = new System.Net.Mail.MailAddress(SmtpUser!, "Heroic853 Mods"),
                Subject = subject,
                Body = html,
                IsBodyHtml = true
            };
            messaggio.To.Add(to);

            try
            {
                await smtp.SendMailAsync(messaggio);
                _logger.LogInformation("Email inviata via SMTP ({Tipo}) a {To}", tipo, to);
                return true;
            }
            catch (System.Net.Mail.SmtpException ex)
            {
                // I casi tipici: password per le app sbagliata, verifica in due
                // passaggi non attiva, oppure porta bloccata.
                _logger.LogError("SMTP ha rifiutato la {Tipo}: {Stato} — {Motivo}",
                    tipo, ex.StatusCode, ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Invio tramite Brevo (ex Sendinblue).
        ///
        /// Chiamata HTTP diretta, senza pacchetti aggiuntivi: l'API e' un solo
        /// POST con la chiave nell'header "api-key".
        /// Documentazione: https://developers.brevo.com/reference/sendtransacemail
        ///
        /// ATTENZIONE: il mittente deve essere verificato in
        /// Brevo -> Settings -> Senders, altrimenti risponde 400.
        /// </summary>
        private async Task<bool> InviaConBrevoAsync(string to, string subject, string html, string tipo)
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(20);

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email");
            request.Headers.Add("api-key", BrevoKey);
            request.Headers.Add("accept", "application/json");

            request.Content = JsonContent.Create(new
            {
                sender = new { name = "Heroic853 Mods", email = FromEmail },
                to = new[] { new { email = to } },
                subject,
                htmlContent = html
            });

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                // Il motivo vero sta nel corpo, non nello status: i casi tipici
                // sono il mittente non verificato e la chiave sbagliata.
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogError("Brevo ha rifiutato la {Tipo}: {Status} — {Body}",
                    tipo, (int)response.StatusCode, Tronca(body));
                return false;
            }

            _logger.LogInformation("Email inviata via Brevo ({Tipo}) a {To} — {Status}",
                tipo, to, (int)response.StatusCode);
            return true;
        }

        /// <summary>
        /// Invio tramite SendGrid. Resta come alternativa: il piano gratuito e'
        /// un trial di 60 giorni e alla scadenza risponde 401 con
        /// "Maximum credits exceeded".
        /// </summary>
        private async Task<bool> InviaConSendGridAsync(string to, string subject, string html, string tipo)
        {
            var client = new SendGridClient(SendGridKey);
            var msg = new SendGridMessage
            {
                From = new EmailAddress(FromEmail, "Heroic853 Mods"),
                Subject = subject,
                HtmlContent = html
            };
            msg.AddTo(new EmailAddress(to));

            var response = await client.SendEmailAsync(msg);

            if ((int)response.StatusCode >= 300)
            {
                var body = await response.Body.ReadAsStringAsync();
                _logger.LogError("SendGrid ha rifiutato la {Tipo}: {Status} — {Body}",
                    tipo, response.StatusCode, Tronca(body));
                return false;
            }

            _logger.LogInformation("Email inviata via SendGrid ({Tipo}) a {To} — {Status}",
                tipo, to, response.StatusCode);
            return true;
        }

        /// <summary>Evita che una risposta di errore lunghissima allaghi i log.</summary>
        private static string Tronca(string? testo) =>
            string.IsNullOrEmpty(testo) ? "(vuoto)"
            : testo.Length <= 400 ? testo
            : testo[..400] + "...";

        // ------------------------------------------------------------------
        // PEZZI DI HTML (stili inline)
        // ------------------------------------------------------------------

        private static string Wrapper(string contenuto) => $"""
            <!DOCTYPE html>
            <html><head><meta charset="utf-8" /></head>
            <body style="margin:0;padding:24px 12px;background:#0d0500;font-family:Georgia,'Times New Roman',serif;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:560px;margin:0 auto;">
                <tr><td style="background:#160a03;border:1px solid rgba(212,175,55,0.35);border-radius:10px;padding:32px 28px;">
                  <div style="text-align:center;margin-bottom:26px;">
                    <div style="color:#ffd700;font-size:20px;letter-spacing:5px;text-transform:uppercase;font-weight:bold;">HEROIC853</div>
                    <div style="color:#c0c0c0;font-size:11px;letter-spacing:2px;margin-top:4px;">MONSTER HUNTER MODS</div>
                  </div>
                  {contenuto}
                  <div style="border-top:1px solid rgba(212,175,55,0.2);margin-top:26px;padding-top:16px;text-align:center;color:#7a7a7a;font-size:11px;">
                    © Heroic853 — Monster Hunter Mods
                  </div>
                </td></tr>
              </table>
            </body></html>
            """;

        private static string Heading(string titolo, string sottotitolo) => $"""
            <h1 style="margin:0 0 6px;color:#ffd700;font-size:22px;text-align:center;">{Esc(titolo)}</h1>
            <p style="margin:0 0 18px;color:#c0c0c0;font-size:13px;text-align:center;">{Esc(sottotitolo)}</p>
            """;

        private static string Badge(string testo, string colore) => $"""
            <div style="text-align:center;margin-bottom:22px;">
              <span style="display:inline-block;background:{colore};color:#fff;font-size:11px;
                           letter-spacing:1px;padding:5px 14px;border-radius:20px;font-family:Arial,sans-serif;">
                {Esc(testo)}
              </span>
            </div>
            """;

        private static string AmountBox(decimal importo) =>
            AmountBoxRaw($"€{importo:0.00}", "Totale addebitato");

        private static string AmountBoxRaw(string valore, string etichetta) => $"""
            <div style="background:rgba(139,0,0,0.35);border:1px solid rgba(212,175,55,0.3);
                        border-radius:8px;padding:18px;text-align:center;margin-bottom:22px;">
              <div style="color:#c0c0c0;font-size:11px;letter-spacing:1px;text-transform:uppercase;">{Esc(etichetta)}</div>
              <div style="color:#ffd700;font-size:30px;font-weight:bold;margin-top:6px;">{Esc(valore)}</div>
            </div>
            """;

        private static string Row(string chiave, string valore, bool mono = false) => $"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0"
                   style="border-bottom:1px solid rgba(212,175,55,0.12);">
              <tr>
                <td style="padding:9px 0;color:#c0c0c0;font-size:12px;">{Esc(chiave)}</td>
                <td style="padding:9px 0;color:#e8e8e8;font-size:12px;text-align:right;
                           {(mono ? "font-family:Consolas,monospace;font-size:11px;" : "")}">{Esc(valore)}</td>
              </tr>
            </table>
            """;

        /// <summary>
        /// Evita che un nome prodotto o un'email con &lt; o &amp; rompa l'HTML.
        /// </summary>
        private static string Esc(string? s) => System.Net.WebUtility.HtmlEncode(s ?? "");
    }
}
