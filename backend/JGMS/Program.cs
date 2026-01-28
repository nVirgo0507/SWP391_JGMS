using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using SWP391_JGMS.DAL;
using SWP391_JGMS.DAL.Models;
using SWP391_JGMS.DAL.Repositories;
using SWP391_JGMS.BLL.Services;

namespace SWP391_JGMS;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Configure Npgsql data source with enum mapping
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(
            builder.Configuration.GetConnectionString("DefaultConnection") ?? 
            "Host=localhost;Port=5433;Database=swp391_db;Username=admin;Password=123456");
        
        // Map enums with custom lowercase translator
        dataSourceBuilder.MapEnum<UserRole>("user_role", new LowercaseNameTranslator());
        dataSourceBuilder.MapEnum<UserStatus>("user_status", new LowercaseNameTranslator());
        
        var dataSource = dataSourceBuilder.Build();

        // Register Database Context with configured data source
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(dataSource));

        // Register Repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();

        // Register Services
        builder.Services.AddScoped<IUserService, UserService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        app.Run();
    }
}

// Custom name translator to convert C# enum names to lowercase for PostgreSQL
public class LowercaseNameTranslator : INpgsqlNameTranslator
{
    public string TranslateTypeName(string clrName) => clrName.ToLowerInvariant();
    public string TranslateMemberName(string clrName) => clrName.ToLowerInvariant();
}
