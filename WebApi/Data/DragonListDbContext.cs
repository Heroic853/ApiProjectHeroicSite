using Microsoft.EntityFrameworkCore;
using SharedLibrary.Dto;

namespace WebApi.Data
{
    public class DragonListDbContext : DbContext
    {
        public DbSet<Dragon> DragonSet { get; set; } // prima tabella
        public DbSet<User> User { get; set; } // seconda tabella
        public DbSet<Clasification> Clasification { get; set; } 

        public DragonListDbContext(DbContextOptions options):base(options)
        {
        }

        //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        //{
        //    optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS01;Database=DragonList;Trusted_Connection=True;TrustServerCertificate=True;"); //Databese istanza che e DragonList
        //}
    }
}
