using SendGrid;
using SendGrid.Helpers.Mail;

public class EmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    public async Task SendPaymentConfirmation(
    string toEmail, string toName, string planName,
    decimal amount, string transactionId, DateTime date)
    {
        try
        {
            var apiKey = _config["SendGrid:ApiKey"];
            Console.WriteLine($"[SendGrid] ApiKey presente: {!string.IsNullOrEmpty(apiKey)}");
            Console.WriteLine($"[SendGrid] Invio a: {toEmail}");

            var client = new SendGridClient(apiKey);
            var msg = new SendGridMessage
            {
                From = new EmailAddress("heroic853@gmail.com", "Heroic853 Mods"),
                Subject = "✅ Payment Confirmed — Heroic853 Mods",
                HtmlContent = BuildEmailHtml(toName, planName, amount, transactionId, date)
            };

            msg.AddTo(new EmailAddress(toEmail, toName));
            var response = await client.SendEmailAsync(msg);

            Console.WriteLine($"[SendGrid] Status: {response.StatusCode}");

            if ((int)response.StatusCode >= 300)
            {
                var body = await response.Body.ReadAsStringAsync();
                Console.WriteLine($"[SendGrid] Errore: {body}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SendGrid] Eccezione: {ex.Message}");
        }
    }

    private string BuildEmailHtml(
        string name,
        string planName,
        decimal amount,
        string transactionId,
        DateTime date)
    {
      return $"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8"/>
        </head>
        <body>
          <div class="wrapper">
            <div class="card">

              <div class="logo">HEROIC853</div>
              <div class="logo-sub">Monster Hunter Mods</div>

              <div class="icon-wrap">
                <div class="icon">✓</div>
              </div>

              <div class="title">Transaction Completed</div>
              <div class="subtitle">Your payment has been processed successfully.</div>
              <div style="text-align:center; margin-bottom:24px;">
                <span class="badge">● CONFIRMED</span>
              </div>

              <div class="amount-box">
                <div class="amount-label">Total amount charged</div>
                <div class="amount">€{amount:F2}</div>
              </div>

              <div class="row">
                <span class="row-key">Customer</span>
                <span class="row-val">{name}</span>
              </div>
              <div class="row">
                <span class="row-key">Product</span>
                <span class="row-val">{planName}</span>
              </div>
              <div class="row">
                <span class="row-key">Date & Time</span>
                <span class="row-val">{date:dd/MM/yyyy HH:mm:ss}</span>
              </div>
              <div class="row">
                <span class="row-key">Transaction ID</span>
                <span class="row-val code">{transactionId}</span>
              </div>
              <div class="row">
                <span class="row-key">Payment Method</span>
                <span class="row-val">Credit Card</span>
              </div>

              <hr class="divider"/>

              <div class="footer">
                Thank you for your purchase, {name}!<br/>
                Keep this email as your payment receipt.<br/>
                For support, reply to this email with your Transaction ID.<br/><br/>
                © 2025 Heroic853 Mods — All rights reserved
              </div>

            </div>
          </div>
        </body>
        </html>
        """;
    }
}