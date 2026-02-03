using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Client.Pages
{
    public partial class NewHunter
    {
        public User User { get; set; } = new();
        private string messageText = "";

        [Inject]
        public HttpClient HttpClient { get; set; }

        private async Task Registred()
        {
            try
            {
                var json = JsonSerializer.Serialize(User);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await HttpClient.PostAsync("api/dragon/register", content);

                if (response.IsSuccessStatusCode)
                {
                    messageText = "Success! Registration complete";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    messageText = $"Error: {response.StatusCode} - {error}";
                }
            }
            catch (Exception ex)
            {
                messageText = $"Error: {ex.Message}";
            }
        }
    }
}