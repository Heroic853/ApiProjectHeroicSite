using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Dto;
using WebApi.Data;
using System.Text.RegularExpressions;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/dragon")]
    //[EnableCors("AllowAll")]
    public class DragonController : ControllerBase
    {

        private readonly DragonListDbContext _dragonListDbContext;
        private readonly ILogger<DragonController> _logger;

        public DragonController(ILogger<DragonController> logger, DragonListDbContext dragonListDbContext)
        {
            _logger = logger;
            _dragonListDbContext = dragonListDbContext;
        }

        [HttpGet] // leggere
        public async Task<IEnumerable<Dragon>> Get()
        {
            /*return Enumerable.Range(1, 43).Select(index => new Dragon
            {
                Name = Names[Random.Shared.Next(Names.Length)],
                Element = Elements[Random.Shared.Next(Elements.Length)],
                Class = Class[Random.Shared.Next(Class.Length)],
                Defence = Random.Shared.Next(-0, 1000),
                Map = Map[Random.Shared.Next(Map.Length)]
            })
           .ToArray();
            e il random dei dati
           */
            return await _dragonListDbContext.DragonSet.ToListAsync();
        }

        [HttpPost] // scrivere
        public async Task Post([FromBody] Dragon dragon)
        {
            await _dragonListDbContext.DragonSet.AddAsync(dragon);
            await _dragonListDbContext.SaveChangesAsync();
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] User user)
        {
            try
            {
                // Verifica se l'account (email) esiste già
                var existingUser = await _dragonListDbContext.User
                    .FirstOrDefaultAsync(u => u.Account == user.Account);

                if (existingUser != null)
                {
                    return BadRequest(new { message = "Account (email) already exists" });
                }

                // Verifica se lo username esiste già
                var existingUsername = await _dragonListDbContext.User
                    .FirstOrDefaultAsync(u => u.Username == user.Username);

                if (existingUsername != null)
                {
                    return BadRequest(new { message = "Username already exists" });
                }

                await _dragonListDbContext.User.AddAsync(user);
                await _dragonListDbContext.SaveChangesAsync();

                return Ok(new { message = "Registration successful" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Registration error: {ex.Message}");
                return StatusCode(500, new { message = "Registration failed" });
            }
        }


        [HttpGet("users")]
        public async Task<IActionResult> GetUsers() 
        {
            try
            {
                var users = await _dragonListDbContext.User
                    .Select(u => new {
                        u.Id,
                        u.Username,
                        u.Account,
                        u.Password
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching users: {ex.Message}");
                return StatusCode(500, new { message = "Failed to load users" });
            }
        }

        [HttpGet("get-user")]
        public async Task<IActionResult> GetUser([FromQuery] string account)
        {
            var user = await _dragonListDbContext.User
                .Where(u => u.Account.Equals(account))
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "User not found" });

            return Ok(user);
        }

        [HttpGet("user-profile")]
        public async Task<IActionResult> GetUserProfile([FromQuery] string username)
        {
            try
            {
                var user = await _dragonListDbContext.User
                    .Where(u => u.Username == username)
                    .Select(u => new {
                        u.Username,
                        u.Account,
                        u.RegistrationDate
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                    return NotFound(new { message = "User not found" });

                return Ok(user);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching user profile: {ex.Message}");
                return StatusCode(500, new { message = "Failed to load user profile" });
            }
        }

        // API per cambiare email
        [HttpPost("change-email")]
        public async Task<IActionResult> ChangeEmail([FromBody] ChangeEmailRequest request)
        {
            try
            {
                var user = await _dragonListDbContext.User
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Verifica se la nuova email esiste già
                var emailExists = await _dragonListDbContext.User
                    .AnyAsync(u => u.Account == request.NewEmail && u.Username != request.Username);

                if (emailExists)
                    return BadRequest(new { message = "Email already in use" });

                user.Account = request.NewEmail;
                await _dragonListDbContext.SaveChangesAsync();

                return Ok(new { message = "Email updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error changing email: {ex.Message}");
                return StatusCode(500, new { message = "Failed to update email" });
            }
        }

        // API per cambiare password con validazione
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            try
            {
                // Trova l'utente
                var user = await _dragonListDbContext.User
                    .FirstOrDefaultAsync(u => u.Username == request.Username);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                // Verifica password attuale
                if (user.Password != request.CurrentPassword)
                    return BadRequest(new { message = "Current password is incorrect" });

                // VALIDAZIONE NUOVA PASSWORD
                if (string.IsNullOrWhiteSpace(request.NewPassword))
                    return BadRequest(new { message = "Password is required" });

                if (request.NewPassword.Length < 8)
                    return BadRequest(new { message = "Password must be at least 8 characters" });

                if (!Regex.IsMatch(request.NewPassword, @"[A-Z]"))
                    return BadRequest(new { message = "Password must contain at least one uppercase letter" });

                if (!Regex.IsMatch(request.NewPassword, @"[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]"))
                    return BadRequest(new { message = "Password must contain at least one special character" });

                // Aggiorna password
                user.Password = request.NewPassword;
                await _dragonListDbContext.SaveChangesAsync();

                return Ok(new { message = "Password updated successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error changing password: {ex.Message}");
                return StatusCode(500, new { message = "Failed to update password" });
            }
        }

        // API per eliminare account
        [HttpDelete("delete-account")]
        public async Task<IActionResult> DeleteAccount([FromQuery] string username)
        {
            try
            {
                var user = await _dragonListDbContext.User
                    .FirstOrDefaultAsync(u => u.Username == username);

                if (user == null)
                    return NotFound(new { message = "User not found" });

                _dragonListDbContext.User.Remove(user);
                await _dragonListDbContext.SaveChangesAsync();

                return Ok(new { message = "Account deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error deleting account: {ex.Message}");
                return StatusCode(500, new { message = "Failed to delete account" });
            }
        }


        [HttpPost("Clasification")]
        public async Task Clasification([FromBody] Clasification clasification)
        {
            await _dragonListDbContext.Clasification.AddAsync(clasification);
            await _dragonListDbContext.SaveChangesAsync();
        }

        [HttpGet("Clasification")]
        public async Task<IEnumerable<Clasification>> GetAniversary()
        {
            return await _dragonListDbContext.Clasification.ToListAsync();
        }
    }
}