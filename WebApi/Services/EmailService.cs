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

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Variabile d'ambiente (Render) oppure configurazione (user secrets in locale)
        private string? Secret(string name) =>
            Environment.GetEnvironmentVariable(name) is { Length: > 0 } fromEnv
                ? fromEnv
                : _config[name];

        private string? ApiKey => Secret("SENDGRID_API_KEY") ?? _config["SendGrid:ApiKey"];

        /// <summary>
        /// Mittente. Deve essere un indirizzo VERIFICATO in SendGrid
        /// (Settings -> Sender Authentication), altrimenti SendGrid rifiuta con 403.
        /// </summary>
        private string FromEmail => Secret("MAIL_FROM") ?? "heroic853@gmail.com";

        /// <summary>Dove arrivano le notifiche di vendita e i report.</summary>
        private string? NotifyEmail => Secret("NOTIFY_EMAIL") ?? Secret("MAIL_FROM") ?? "heroic853@gmail.com";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

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
                _logger.LogWarning("SENDGRID_API_KEY non impostata: {Tipo} non inviata a {To}", tipo, to);
                return false;
            }

            try
            {
                var client = new SendGridClient(ApiKey);
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
                    // Il motivo del rifiuto sta nel corpo, non nello status:
                    // il caso tipico e' il mittente non verificato in SendGrid.
                    var body = await response.Body.ReadAsStringAsync();
                    _logger.LogError("SendGrid ha rifiutato la {Tipo}: {Status} — {Body}",
                        tipo, response.StatusCode, body);
                    return false;
                }

                _logger.LogInformation("Email inviata ({Tipo}) a {To} — {Status}", tipo, to, response.StatusCode);
                return true;
            }
            catch (Exception ex)
            {
                // Un errore di invio non deve mai far fallire la richiesta chiamante
                _logger.LogError(ex, "Errore nell'invio della {Tipo} a {To}", tipo, to);
                return false;
            }
        }

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
