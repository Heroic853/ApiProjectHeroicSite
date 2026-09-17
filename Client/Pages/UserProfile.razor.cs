using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Client.Pages
{
    public partial class UserProfile
    {
        [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; }

        private string currentEmail = "";
        private string newEmail = "";
        private string currentPassword = "";
        private string newPassword = "";
        private string confirmPassword = "";
        private bool showSuccessMessage = false;
        private string successMessage = "";
        private string errorMessage = "";
        private bool isLoading = true;
        private string currentUsername = "";

        protected override async Task OnInitializedAsync()
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                currentUsername = user.Identity.Name ?? "";
                // Email viene dai claims di Auth0
                currentEmail = user.FindFirst("email")?.Value ?? "";
                isLoading = false;
            }
            else
            {
                isLoading = false;
            }
        }

        private async Task AutoHideMessage()
        {
            await Task.Delay(3000);
            showSuccessMessage = false;
            StateHasChanged();
        }
    }
}