using BLL.Services;
using BLL.Services.Interface;
using DAL.Data;
using DAL.Models;
using DAL.Repositories;
using DAL.Repositories.Interface;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using Npgsql.NameTranslation;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Security.Claims;

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
			options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
			});
        builder.Services.AddEndpointsApiExplorer();

		builder.Services.AddSwaggerGen(c =>
		{
			c.SwaggerDoc("v1", new OpenApiInfo
			{
				Title = "SWP391 JGMS API",
				Version = "v1"
			});

			c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
			{
				Name = "Authorization",
				Type = SecuritySchemeType.Http,
				Scheme = "Bearer",
				BearerFormat = "JWT",
				In = ParameterLocation.Header,
				Description = "Enter: Bearer {your JWT token}"
			});

			c.AddSecurityRequirement(new OpenApiSecurityRequirement
			{
				{
					new OpenApiSecurityScheme
					{
						Reference = new OpenApiReference
						{
							Type = ReferenceType.SecurityScheme,
							Id = "Bearer"
						}
					},
					Array.Empty<string>()
				}
			});

			c.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
		});

		var jwtSettings = builder.Configuration.GetSection("Jwt");
		var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

		builder.Services.AddAuthentication(options =>
		{
			options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
			options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
		}).AddJwtBearer(options =>
		{
			options.RequireHttpsMetadata = false;
			options.SaveToken = true;
			options.TokenValidationParameters = new TokenValidationParameters
			{
				ValidateIssuer = true,
				ValidateAudience = true,
				ValidateLifetime = true,
				ValidateIssuerSigningKey = true,

				ValidIssuer = jwtSettings["Issuer"],
				ValidAudience = jwtSettings["Audience"],
				IssuerSigningKey = new SymmetricSecurityKey(key),

				RoleClaimType = ClaimTypes.Role
			};
		});

		builder.Services.AddAuthorization();

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
		builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
		builder.Services.AddScoped<IJiraIntegrationRepository, JiraIntegrationRepository>();
		builder.Services.AddScoped<IJiraIssueRepository, JiraIssueRepository>();

		// Register HttpClient for Jira API
		builder.Services.AddHttpClient();

		// Register Data Protection for encrypting API tokens
		builder.Services.AddDataProtection();

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
		// Jira Integration services
		builder.Services.AddScoped<IJiraApiService, JiraApiService>();
		builder.Services.AddScoped<IJiraIntegrationService, JiraIntegrationService>();

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
		app.UseAuthentication();
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
