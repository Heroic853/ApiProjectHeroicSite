using Client.Service;
using Microsoft.AspNetCore.Components;
using SharedLibrary;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Pages
{
    public partial class Commision
    {
        [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
        [Inject] private NavigationManager NavManager { get; set; }

        private string? checkoutError;
        private string? busyPlanId;

        /// <summary>
        /// Avvia il checkout Stripe. Manda solo l'ID del piano: il prezzo lo
        /// decide il server leggendolo da CommissionPlans, così non è
        /// manipolabile dal browser.
        /// </summary>
        async Task BuyPlan(CommissionPlan plan)
        {
            checkoutError = null;
            busyPlanId = plan.Id;

            try
            {
                var anonClient = HttpClientFactory.CreateClient("Anonymous");
                var response = await anonClient.PostAsJsonAsync(
                    "api/dragon/create-checkout",
                    new { PlanName = plan.Id });

                if (!response.IsSuccessStatusCode)
                {
                    // Prima l'errore non veniva gestito: la pagina esplodeva su
                    // GetProperty("url") con un messaggio incomprensibile.
                    checkoutError = response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                        ? "Payments are temporarily unavailable. Please try again later."
                        : "Could not start the checkout. Please try again.";
                    Console.WriteLine($"[create-checkout] HTTP {(int)response.StatusCode}");
                    return;
                }

                var data = await response.Content.ReadFromJsonAsync<JsonElement>();

                if (!data.TryGetProperty("url", out var urlProp) || urlProp.GetString() is not { Length: > 0 } url)
                {
                    checkoutError = "Stripe did not return a checkout link. Please try again.";
                    return;
                }

                NavManager.NavigateTo(url, forceLoad: true);
            }
            catch (Exception ex)
            {
                checkoutError = "Network error while contacting the payment service.";
                Console.WriteLine($"[create-checkout] {ex.Message}");
            }
            finally
            {
                busyPlanId = null;
            }
        }

    }
}
