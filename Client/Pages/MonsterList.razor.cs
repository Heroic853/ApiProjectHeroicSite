using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
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
        private HttpClient httpClient;

        private async Task Saves()
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/dragon", Dragon);

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

        private async Task Saved()
        {
            try
            {
                var response = await httpClient.PostAsJsonAsync("api/dragon/Clasification", Clasification);

                if (response.IsSuccessStatusCode)
                {
                    messageTextFeedbacks = "Success! Your choice was sent";
                    Clasification = new(); // Reset form
                }
                else
                {
                    messageTextFeedbacks = "Error: Nothing was sent";
                }
            }
            catch (Exception ex)
            {
                messageTextFeedbacks = $"Error: {ex.Message}";
            }
        }
    }
}