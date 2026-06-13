using System.ComponentModel.DataAnnotations;

namespace SharedLibrary.Dto
{
    public class User
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Username is required")]
        [StringLength(20, ErrorMessage = "Username must be max 20 characters")]
        public string? Username { get; set; }  // Nome visualizzato

        [Required(ErrorMessage = "Account is required")]
        [EmailAddress(ErrorMessage = "Account must be a valid email address")]
        public string? Account { get; set; }  // Email

        [Required(ErrorMessage = "Password is required")]
        [MinLength(8, ErrorMessage = "Password must be at least 8 characters")]
        [RegularExpression(@"^(?=.*[A-Z])(?=.*[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]).{8,}$",
            ErrorMessage = "Password must contain at least one uppercase letter and one special character")]
        public string? Password { get; set; }
        public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    }
}