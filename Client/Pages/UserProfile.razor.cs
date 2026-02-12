using Microsoft.JSInterop;
using SharedLibrary.Dto;
using System.Net.Http.Json;
using System.Text.RegularExpressions;

namespace Client.Pages
{
    public partial class UserProfile
    {
        // Dati dell'utente
        private string currentEmail = "";
        private string registrationDate = "";

        // Form inputs
        private string newEmail = "";
        private string currentPassword = "";
        private string newPassword = "";
        private string confirmPassword = "";

        // UI State
        private bool showSuccessMessage = false;
        private string successMessage = "";
        private string errorMessage = "";
        private bool isLoading = true;
        private bool isProcessing = false;

        protected override async Task OnInitializedAsync()
        {
            if (!string.IsNullOrEmpty(ApplicationManager.Username))
            {
                await LoadUserProfile();
            }
            else
            {
                isLoading = false;
            }
        }

        private async Task LoadUserProfile()
        {
            try
            {
                isLoading = true;
                var response = await Http.GetFromJsonAsync<UserProfileResponse>(
                    $"api/dragon/user-profile?username={ApplicationManager.Username}");

                if (response != null)
                {
                    currentEmail = response.Account ?? "Not set";
                    registrationDate = response.RegistrationDate.ToString("MMMM dd, yyyy");
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to load user profile";
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                isLoading = false;
            }
        }

        private async Task ChangeEmail()
        {
            errorMessage = "";
            showSuccessMessage = false;

            if (string.IsNullOrWhiteSpace(newEmail))
            {
                errorMessage = "Please enter a new email address";
                return;
            }

            if (!IsValidEmail(newEmail))
            {
                errorMessage = "Please enter a valid email address";
                return;
            }

            try
            {
                isProcessing = true;
                var request = new ChangeEmailRequest
                {
                    Username = ApplicationManager.Username,
                    NewEmail = newEmail
                };

                var response = await Http.PostAsJsonAsync("api/dragon/change-email", request);

                if (response.IsSuccessStatusCode)
                {
                    currentEmail = newEmail;
                    newEmail = "";
                    successMessage = "Email updated successfully!";
                    showSuccessMessage = true;
                    await AutoHideMessage();
                }
                else
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    errorMessage = error?.Message ?? "Failed to update email";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while updating email";
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }

        private async Task ChangePassword()
        {
            errorMessage = "";
            showSuccessMessage = false;

            // Validazione client-side
            if (string.IsNullOrWhiteSpace(currentPassword))
            {
                errorMessage = "Current password is required";
                return;
            }

            if (string.IsNullOrWhiteSpace(newPassword))
            {
                errorMessage = "New password is required";
                return;
            }

            if (newPassword.Length < 8)
            {
                errorMessage = "Password must be at least 8 characters";
                return;
            }

            if (!Regex.IsMatch(newPassword, @"[A-Z]"))
            {
                errorMessage = "Password must contain at least one uppercase letter";
                return;
            }

            if (!Regex.IsMatch(newPassword, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
            {
                errorMessage = "Password must contain at least one special character";
                return;
            }

            if (newPassword != confirmPassword)
            {
                errorMessage = "New passwords do not match";
                return;
            }

            try
            {
                isProcessing = true;
                var request = new ChangePasswordRequest
                {
                    Username = ApplicationManager.Username,
                    CurrentPassword = currentPassword,
                    NewPassword = newPassword
                };

                var response = await Http.PostAsJsonAsync("api/dragon/change-password", request);

                if (response.IsSuccessStatusCode)
                {
                    currentPassword = "";
                    newPassword = "";
                    confirmPassword = "";
                    successMessage = "Password updated successfully!";
                    showSuccessMessage = true;
                    await AutoHideMessage();
                }
                else
                {
                    var error = await response.Content.ReadFromJsonAsync<ErrorResponse>();
                    errorMessage = error?.Message ?? "Failed to update password";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while updating password";
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }

        private async Task DeleteAccount()
        {
            bool confirmed = await JSRuntime.InvokeAsync<bool>("confirm",
                "⚠️ Are you sure you want to delete your account?\n\nThis action CANNOT be undone!\nAll your data will be permanently lost.");

            if (!confirmed) return;

            bool doubleConfirm = await JSRuntime.InvokeAsync<bool>("confirm",
                "⚠️ FINAL WARNING!\n\nAre you ABSOLUTELY sure?");

            if (!doubleConfirm) return;

            try
            {
                isProcessing = true;
                var response = await Http.DeleteAsync($"api/dragon/delete-account?username={ApplicationManager.Username}");

                if (response.IsSuccessStatusCode)
                {
                    ApplicationManager.Username = null;
                    NavManager.NavigateTo("", forceLoad: true);
                }
                else
                {
                    errorMessage = "Failed to delete account";
                }
            }
            catch (Exception ex)
            {
                errorMessage = "An error occurred while deleting account";
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                isProcessing = false;
            }
        }

        private async Task AutoHideMessage()
        {
            await Task.Delay(3000);
            showSuccessMessage = false;
            StateHasChanged();
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }
}