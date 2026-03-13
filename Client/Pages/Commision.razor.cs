using Client.Service;
using Microsoft.AspNetCore.Components;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace Client.Pages
{
    public partial class Commision
    {
        [Inject] private IHttpClientFactory HttpClientFactory { get; set; }
        [Inject] private NavigationManager NavManager { get; set; }

        async Task BuyPlan(string planName, long cents)
        {
            var anonClient = HttpClientFactory.CreateClient("Anonymous");
            var response = await anonClient.PostAsJsonAsync(
                "api/dragon/create-checkout",
                new { PlanName = planName, AmountCents = cents });

            var data = await response.Content.ReadFromJsonAsync<JsonElement>();
            var url = data.GetProperty("url").GetString();

            NavManager.NavigateTo(url, forceLoad: true);
        }

    }
}
