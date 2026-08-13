using Microsoft.AspNetCore.Components;
using SharedLibrary.Dto;
using System.Net.Http;
using System.Net.Http.Json;

namespace Client.Pages
{
    public partial class UsersTable
    {
        private User[]? UsersList;
        private string? loadError;

        protected override async Task OnInitializedAsync()
        {
            try
            {
                UsersList = await Http.GetFromJsonAsync<User[]>("api/dragon/users");
            }
            catch (Exception ex)
            {
                // Prima l'eccezione risaliva fino al boundary di Blazor e la
                // pagina restava bloccata su "Loading please..." per sempre.
                loadError = "Could not load the user list.";
                Console.WriteLine($"[users] {ex.Message}");
            }
        }
    }
}
