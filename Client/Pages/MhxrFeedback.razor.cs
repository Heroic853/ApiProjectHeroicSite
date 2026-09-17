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

        /// <summary>
        /// L'ultimo ticket di chi e' loggato. Se non e' "Chiuso" il modulo di
        /// invio resta nascosto: un utente puo' avere un solo ticket aperto
        /// alla volta, deve prima ricevere risposta o chiuderlo lui.
        /// Per chi NON e' loggato resta sempre null: senza account non c'e'
        /// modo di ritrovare un ticket dopo aver lasciato la pagina, quindi
        /// per gli anonimi il limite "uno alla volta" non si puo' applicare.
        /// </summary>
        private MhxrTicketDto? ticketAttivo;
        private bool caricamentoIniziale = true;
        private bool chiusuraInCorso;

        private bool MostraModulo => ticketAttivo is null;

        private int LunghezzaMessaggio => messaggio?.Length ?? 0;

        protected override async Task OnInitializedAsync()
        {
            var stato = await AuthStateProvider.GetAuthenticationStateAsync();
            if (stato.User.Identity?.IsAuthenticated == true)
                await CaricaTicketAttivoAsync();

            caricamentoIniziale = false;
        }

        private async Task CaricaTicketAttivoAsync()
        {
            try
            {
                var ultimo = await Http.GetFromJsonAsync<MhxrTicketDto?>("api/mhxr/mio-ticket");
                ticketAttivo = ultimo is { Stato: not "Chiuso" } ? ultimo : null;
            }
            catch (Exception ex)
            {
                // Se questa chiamata fallisce si mostra comunque il modulo:
                // meglio lasciar scrivere un ticket che bloccare la pagina.
                Console.WriteLine($"[mhxr-ticket] impossibile leggere il ticket attivo: {ex.Message}");
            }
        }

        private async Task ChiudiTicket()
        {
            if (ticketAttivo is null || chiusuraInCorso)
                return;

            chiusuraInCorso = true;
            try
            {
                var risposta = await Http.PostAsync($"api/mhxr/ticket/{ticketAttivo.Id}/chiudi", null);
                if (risposta.IsSuccessStatusCode)
                {
                    ticketAttivo = null;
                    NuovoTicket();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[mhxr-ticket] chiusura fallita: {ex.Message}");
            }
            finally
            {
                chiusuraInCorso = false;
            }
        }

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

                    if (inviatoComeAnonimo)
                    {
                        // Senza account non c'e' modo di ritrovare il ticket dopo:
                        // resta la vecchia schermata "grazie, puoi mandarne un altro".
                        ticketInviato = true;
                    }
                    else
                    {
                        // Chi e' loggato passa direttamente alla schermata di stato:
                        // e' anche il modo in cui la pagina applica "un ticket alla
                        // volta", perche' MostraModulo torna false finche' non e'
                        // chiuso.
                        await CaricaTicketAttivoAsync();
                    }

                    return;
                }

                errore = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.TooManyRequests =>
                        "You already sent several tickets. Please wait a bit before sending another one.",
                    System.Net.HttpStatusCode.Conflict =>
                        await LeggiMessaggio(response) ?? "You already have an open ticket.",
                    System.Net.HttpStatusCode.BadRequest =>
                        await LeggiMessaggio(response) ?? "Check the fields and try again.",
                    _ =>
                        $"Could not send the ticket (error {(int)response.StatusCode}). Please try again later."
                };

                // Se il server ha rifiutato perche' c'e' gia' un ticket aperto,
                // si allinea la pagina con quello vero invece di lasciarla
                // a mostrare un modulo che tanto verra' rifiutato di nuovo.
                if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                    await CaricaTicketAttivoAsync();
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

        /// <summary>Etichetta in inglese per il badge di stato del ticket.</summary>
        private static string EtichettaStato(string stato) => stato switch
        {
            "Risposto" => "Answered",
            "Chiuso" => "Closed",
            "Preso in carico" => "In progress",
            _ => "Pending"
        };

        /// <summary>Classe CSS del badge, coerente col colore dell'etichetta sopra.</summary>
        private static string ClasseStato(string stato) => stato switch
        {
            "Risposto" => "mhxr-badge-answered",
            "Chiuso" => "mhxr-badge-closed",
            "Preso in carico" => "mhxr-badge-progress",
            _ => "mhxr-badge-pending"
        };
    }
}
