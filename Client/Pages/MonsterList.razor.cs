using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.JSInterop;
using SharedLibrary.Dto;
using System.ComponentModel.DataAnnotations;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Client.Service;

namespace Client.Pages
{
    public partial class MonsterList
    {
        [Parameter(CaptureUnmatchedValues = true)]
        public Dictionary<string, object> InputAttributes { get; set; } = new Dictionary<string, object>();

        [Inject]
        public ApplicationManager ApplicationManager { get; set; }

        public Dragon Dragon { get; set; } = new();
        public Clasification Clasification { get; set; } = new();

        private string messageTextMonsterChoise = "";
        private string messageTextFeedbacks = "";
        
        [Inject]
        public HttpClient HttpClient { get; set; }

        private async Task Saves()
        {
            try
            {
                var response = await HttpClient.PostAsJsonAsync("api/dragon", Dragon);

                if (response.IsSuccessStatusCode)
                {
                    messageTextMonsterChoise = "Success! The dragon was created";
                    Dragon = new(); // Reset form
                }
                else
                {
                    messageTextMonsterChoise = "Error: Nothing was created";
                }
            }
            catch (Exception ex)
            {
                messageTextMonsterChoise = $"Error: {ex.Message}";
            }
        }

        private bool invioInCorso;
        private bool messaggioEUnErrore;

        private List<ReviewDto> recensioni = new();
        private bool recensioniInCaricamento = true;

        [Inject] private IHttpClientFactory HttpClientFactory { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

        protected override async Task OnInitializedAsync()
        {
            await CaricaRecensioni();
        }

        /// <summary>
        /// Carica le recensioni con il client "Anonymous": l'endpoint /reviews
        /// e' pubblico, e usando il client autenticato un visitatore non
        /// loggato non le vedrebbe (e' lo stesso errore che teneva il grafico
        /// vuoto per mesi).
        /// </summary>
        private async Task CaricaRecensioni()
        {
            recensioniInCaricamento = true;
            try
            {
                var anon = HttpClientFactory.CreateClient("Anonymous");
                recensioni = await anon.GetFromJsonAsync<List<ReviewDto>>("api/dragon/reviews?limit=50")
                             ?? new List<ReviewDto>();
            }
            catch (Exception ex)
            {
                recensioni = new List<ReviewDto>();
                Console.WriteLine($"[reviews] {ex.Message}");
            }
            finally
            {
                recensioniInCaricamento = false;
            }
        }

        private async Task CancellaRecensione(int id)
        {
            var conferma = await JSRuntime.InvokeAsync<bool>("confirm",
                "Delete this review permanently?");
            if (!conferma) return;

            try
            {
                var response = await HttpClient.DeleteAsync($"api/dragon/reviews/{id}");
                if (response.IsSuccessStatusCode)
                {
                    // Toglila subito dalla lista, senza rileggere tutto
                    recensioni.RemoveAll(r => r.Id == id);
                }
                else
                {
                    messageTextFeedbacks = $"Could not delete it (error {(int)response.StatusCode}).";
                    messaggioEUnErrore = true;
                }
            }
            catch (Exception ex)
            {
                messageTextFeedbacks = "Could not delete it.";
                messaggioEUnErrore = true;
                Console.WriteLine($"[reviews delete] {ex.Message}");
            }
        }

        private async Task Saved()
        {
            messaggioEUnErrore = false;

            // Controlli lato client: evitano una chiamata inutile e danno
            // un messaggio chiaro invece del generico "Nothing was sent"
            if (string.IsNullOrWhiteSpace(Clasification.Monster))
            {
                messageTextFeedbacks = "Choose which mod you are rating.";
                messaggioEUnErrore = true;
                return;
            }

            if (string.IsNullOrWhiteSpace(Clasification.Feedback))
            {
                messageTextFeedbacks = "Choose a rating.";
                messaggioEUnErrore = true;
                return;
            }

            invioInCorso = true;
            try
            {
                var response = await HttpClient.PostAsJsonAsync("api/dragon/Clasification", Clasification);

                if (response.IsSuccessStatusCode)
                {
                    var eraUnaRecensione = !string.IsNullOrWhiteSpace(Clasification.Review);
                    messageTextFeedbacks = eraUnaRecensione
                        ? "Review sent. Thank you!"
                        : "Rating sent. Thank you!";
                    Clasification = new(); // Reset form

                    // Se ha scritto qualcosa, ricarica l'elenco cosi' la vede
                    // comparire subito sotto
                    if (eraUnaRecensione)
                        await CaricaRecensioni();

                    return;
                }

                // Prima qualsiasi errore diventava "Nothing was sent", che non
                // diceva niente. Il caso piu' comune e' l'utente non loggato:
                // l'endpoint ora richiede un token.
                messageTextFeedbacks = response.StatusCode switch
                {
                    System.Net.HttpStatusCode.Unauthorized => "You need to sign in to leave a rating.",
                    System.Net.HttpStatusCode.BadRequest => await LeggiMessaggio(response) ?? "Check the fields and try again.",
                    _ => $"Could not send it (error {(int)response.StatusCode}). Try again later."
                };
                messaggioEUnErrore = true;
            }
            catch (AccessTokenNotAvailableException)
            {
                // La lancia l'handler di Auth0 quando non c'e' un token:
                // succede se non sei loggato, e senza questo catch la pagina
                // mostrava un errore incomprensibile.
                messageTextFeedbacks = "You need to sign in to leave a rating.";
                messaggioEUnErrore = true;
            }
            catch (Exception ex)
            {
                messageTextFeedbacks = "Network error. Check your connection and try again.";
                messaggioEUnErrore = true;
                Console.WriteLine($"[Clasification] {ex.Message}");
            }
            finally
            {
                invioInCorso = false;
            }
        }

        /// <summary>Legge il campo "message" dalla risposta di errore dell'API.</summary>
        private static async Task<string?> LeggiMessaggio(HttpResponseMessage response)
        {
            try
            {
                var json = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
                return json.TryGetProperty("message", out var m) ? m.GetString() : null;
            }
            catch
            {
                return null;
            }
        }
    }
}