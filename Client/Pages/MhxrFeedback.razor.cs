using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Pages
{
    public partial class MhxrFeedback
    {
        [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
        [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
        [Inject] private HttpClient Http { get; set; } = default!;

        private string categoria = "";
        private string messaggio = "";

        private bool invioInCorso;
        private string errore = "";

        private bool ticketInviato;
        private int numeroTicket;
        private bool inviatoComeAnonimo;

        private int LunghezzaMessaggio => messaggio?.Length ?? 0;

        private async Task InviaTicket()
        {
            errore = "";
            if (string.IsNullOrWhiteSpace(categoria))
            {
                errore = "Choose what kind of problem it is.";
                return;
            }

            if (string.IsNullOrWhiteSpace(messaggio))
            {
                errore = "Write a short description before sending.";
                return;
            }

            invioInCorso = true;
            try
            {
                var client = await ScegliClientAsync();

                var response = await client.PostAsJsonAsync(
                    "api/mhxr/ticket",
                    new MhxrTicketRequest { Categoria = categoria, Messaggio = messaggio });

                if (response.IsSuccessStatusCode)
                {
                    var dati = await response.Content.ReadFromJsonAsync<JsonElement>();

                    numeroTicket = dati.TryGetProperty("id", out var id) ? id.GetInt32() : 0;
                    inviatoComeAnonimo = !dati.TryGetProperty("anonimo", out var anon) || anon.GetBoolean();

                    ticketInviato = true;
                    return;
                }

                errore = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests =>
                        "You already sent several tickets. Please wait a bit before sending another one.",
                    System.Net.HttpStatusCode.BadRequest =>
                        await LeggiMessaggio(response) ?? "Check the fields and try again.",
                    _ =>
                        $"Could not send the ticket (error {(int)response.StatusCode}). Please try again later."
                };
            }
            catch (AccessTokenNotAvailableException)
            {
                // Non dovrebbe capitare: ScegliClientAsync usa il client anonimo
                // per chi non e' loggato. Resta come rete di sicurezza.
                errore = "Your session expired. Reload the page and try again.";
            }
            catch (Exception ex)
            {
                errore = "Network error. Check your connection and try again.";
                Console.WriteLine($"[mhxr-ticket] {ex.Message}");
            }
            finally
            {
                invioInCorso = false;
            }
        }

        /// <summary>
        /// Sceglie con quale client mandare la richiesta.
        ///
        /// L'endpoint accetta tutti, ma il server deve poter capire CHI scrive
        /// quando c'e' un account. Il client "Anonymous" non manda mai il token,
        /// mentre quello autenticato lancia un'eccezione se il token non c'e':
        /// quindi si guarda prima lo stato di autenticazione e si sceglie.
        /// </summary>
        private async Task<HttpClient> ScegliClientAsync()
        {
            var stato = await AuthStateProvider.GetAuthenticationStateAsync();

            return stato.User.Identity?.IsAuthenticated == true
                ? Http
                : HttpClientFactory.CreateClient("Anonymous");
        }

        /// <summary>Legge il campo "message" dalla risposta di errore dell'API.</summary>
        private static async Task<string?> LeggiMessaggio(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                return json.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch
            {
                return null;
            }
        }

        private void NuovoTicket()
        {
            ticketInviato = false;
            categoria = "";
            messaggio = "";
            errore = "";
            numeroTicket = 0;
        }
    }
}
