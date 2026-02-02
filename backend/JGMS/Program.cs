using BLL.Services;
using BLL.Services.Interface;
using DAL.Data;
using DAL.Models;
using DAL.Repositories;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;


namespace SWP391_JGMS;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

		NpgsqlConnection.GlobalTypeMapper.MapEnum<UserRole>("user_role");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<UserStatus>("user_status");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<DAL.Models.TaskStatus>("task_status");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<PriorityLevel>("priority_level");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<RequirementType>("requirement_type");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<JiraPriority>("jira_priority");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<DocumentStatus>("document_status");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<ProjectStatus>("project_status");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<SyncStatus>("sync_status");
		NpgsqlConnection.GlobalTypeMapper.MapEnum<ReportType>("report_type");

		// Configure Npgsql to handle DateTime correctly
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

		builder.Services.AddDbContext<JgmsContext>(options =>
	        options.UseNpgsql(
		    builder.Configuration.GetConnectionString("DefaultConnection")
	    ));

		// Register repositories
		builder.Services.AddScoped<IUserRepository, UserRepository>();
		builder.Services.AddScoped<IStudentGroupRepository, StudentGroupRepository>();
		builder.Services.AddScoped<IGroupMemberRepository, GroupMemberRepository>();
		builder.Services.AddScoped<ITaskRepository, TaskRepository>();
		builder.Services.AddScoped<ICommitRepository, CommitRepository>();
		builder.Services.AddScoped<IPersonalTaskStatisticRepository, PersonalTaskStatisticRepository>();
		builder.Services.AddScoped<ISrsDocumentRepository, SrsDocumentRepository>();

		// Register services
		builder.Services.AddScoped<IUserService, UserService>();
		builder.Services.AddScoped<IAdminService, AdminService>();
		builder.Services.AddScoped<IStudentService, StudentService>();

        var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<JgmsContext>();
			DbInitializer.SeedAdmin(context);
		}

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
