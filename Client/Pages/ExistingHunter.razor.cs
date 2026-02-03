using Client.Service;
using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class ExistingHunter
    {
        [Inject] private NavigationManager NavManager { get; set; }
        [Inject] private ApplicationManager ApplicationManager { get; set; }
        [Inject] public HttpClient HttpClient { get; set; }

        public User User { get; set; } = new();
        private string messageText = "";

        public async Task Login()
        {
            try
            {
                var dbUser = await HttpClient.GetFromJsonAsync<User>(
                    $"api/dragon/get-user?username={User.Username}"
                );

                if (dbUser == null)
                {
                    messageText = "Account doesn't exist";
                    return;
                }

                if (User.Password.Equals(dbUser.Password))
                {
                    ApplicationManager.Username = dbUser.Username;
                    NavManager.NavigateTo("/");
                }
                else
                {
                    messageText = "Wrong Password";
                }
            }
            catch (HttpRequestException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    messageText = "Account doesn't exist";
                }
                else
                {
                    messageText = "Connection error. Please try again.";
                }
                Console.WriteLine($"Login error: {ex.Message}");
            }
            catch (Exception ex)
            {
                messageText = "Error during login";
                Console.WriteLine($"Login error: {ex.Message}");
            }
        }
    }
}