using Microsoft.EntityFrameworkCore;

namespace SWP391_JGMS.DAL;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    // public DbSet<YourModel> YourModels { get; set; }
}