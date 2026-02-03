using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SharedLibrary.Dto;
using WebApi.Data;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/dragon")]
    [EnableCors("AllowAll")]
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
        public async Task Register([FromBody] User user)
        {
            await _dragonListDbContext.User.AddAsync(user);
            await _dragonListDbContext.SaveChangesAsync();
        }
       
        [HttpGet("register")]
        public async Task<IEnumerable<User>> GetUsers()
        {
            return await _dragonListDbContext.User.ToListAsync();
        }

        [HttpGet("get-user")]
        public async Task<IActionResult> GetUser([FromQuery] string username)
        {
            var user = await _dragonListDbContext.User
                .Where(u => u.Username.Equals(username))
                .FirstOrDefaultAsync();

            if (user == null)
                return NotFound(new { message = "User not found" }); // ⬅️ Ritorna 404

            return Ok(user); // ⬅️ Ritorna 200 con l'utente
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