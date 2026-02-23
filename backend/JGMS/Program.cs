using BLL.Services;
using BLL.Services.Interface;
using DAL.Repositories;
using DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using DAL.Models;


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
		builder.Services.AddSwaggerGen(options =>
		{
			// Use full type name for schema ids to avoid collisions between DTOs with same class name
			options.CustomSchemaIds(type => type.FullName);
		});

		// Configure Npgsql to handle DateTime correctly
		AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

		builder.Services.AddDbContext<JgmsContext>(options =>
	        options.UseNpgsql(
		    builder.Configuration.GetConnectionString("DefaultConnection"),
		    npgsqlOptions => npgsqlOptions
		        .MapEnum<UserRole>("user_role")
		        .MapEnum<UserStatus>("user_status")
		        .MapEnum<DAL.Models.TaskStatus>("task_status")
		        .MapEnum<PriorityLevel>("priority_level")
		        .MapEnum<RequirementType>("requirement_type")
		        .MapEnum<JiraPriority>("jira_priority")
		        .MapEnum<DocumentStatus>("document_status")
		        .MapEnum<ProjectStatus>("project_status")
		        .MapEnum<SyncStatus>("sync_status")
		        .MapEnum<ReportType>("report_type")
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
		// BR-054: Lecturer Group-Scoped Access service
		builder.Services.AddScoped<ILecturerService, LecturerService>();
		// BR-055: Team Leader Group-Scoped Access service
		builder.Services.AddScoped<ITeamLeaderService, TeamLeaderService>();
		// BR-056: Team Member Self-Scoped Access service
		builder.Services.AddScoped<ITeamMemberService, TeamMemberService>();
		// BR-058: Admin Integration Configuration service
		builder.Services.AddScoped<IIntegrationService, IntegrationService>();
		builder.Services.AddScoped<IStudentService, StudentService>();

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

        // Initialize database
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<JgmsContext>();
            dbContext.Database.EnsureCreated();
        }

        app.Run();
    }
}

// Custom name translator to convert C# enum names to lowercase for PostgreSQL
public class LowercaseNameTranslator : INpgsqlNameTranslator
{
    public string TranslateTypeName(string clrName) => clrName.ToLowerInvariant();
    public string TranslateMemberName(string clrName) => clrName.ToLowerInvariant();
}
