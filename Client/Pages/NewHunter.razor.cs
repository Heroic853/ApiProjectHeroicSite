using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
            messageText = "";

            // Validazione username
            if (string.IsNullOrWhiteSpace(User.Username))
            {
                messageText = "Username is required";
                return;
            }

            if (User.Username.Length > 20)
            {
                messageText = "Username must be max 20 characters";
                return;
            }

            // Validazione email (deve contenere @)
            if (string.IsNullOrWhiteSpace(User.Account) || !User.Account.Contains("@"))
            {
                messageText = "Account must be a valid email address";
                return;
            }

            // Validazione password
            if (string.IsNullOrWhiteSpace(User.Password))
            {
                messageText = "Password is required";
                return;
            }

            if (User.Password.Length < 8)
            {
                messageText = "Password must be at least 8 characters";
                return;
            }

            if (!Regex.IsMatch(User.Password, @"[A-Z]"))
            {
                messageText = "Password must contain at least one uppercase letter";
                return;
            }

            if (!Regex.IsMatch(User.Password, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            {
                messageText = "Password must contain at least one special character";
                return;
            }

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
                    var errorResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    messageText = errorResponse?.message ?? $"Error: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                messageText = $"Error: {ex.Message}";
            }
        }

        private class ErrorResponse
        {
            public string message { get; set; }
        }
    }
}