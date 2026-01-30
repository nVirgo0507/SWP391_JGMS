using BLL.Services;
using BLL.Services.Interface;
using DAL.Repositories;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using DAL.Models;
using Microsoft.EntityFrameworkCore;


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

		NpgsqlConnection.GlobalTypeMapper.MapEnum<UserRole>("user_role");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<UserStatus>("user_status");

		builder.Services.AddDbContext<JgmsContext>(options =>
	        options.UseNpgsql(
		    builder.Configuration.GetConnectionString("DefaultConnection")
	    ));


		builder.Services.AddScoped<IUserRepository, UserRepository>();
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
