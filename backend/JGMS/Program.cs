using BLL.Services;
using BLL.Services.Interface;
using DAL.Data;
using DAL.Models;using DAL.Repositories;
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

        // CORS — allow frontend apps to call the API
        builder.Services.AddCors(options =>
        {
            options.AddPolicy("AllowFrontend", policy =>
            {
                var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();
                if (allowedOrigins != null && allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
                else
                {
                    // Default: allow common dev ports + any origin for staging
                    policy.SetIsOriginAllowed(_ => true)
                          .AllowAnyHeader()
                          .AllowAnyMethod()
                          .AllowCredentials();
                }
            });
        });

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
				Version = "v1",
				Description = "Jira & GitHub Management System — API for frontend integration.\n\n" +
					"**Auth flow:** POST `/api/auth/login` → get `accessToken` → " +
					"click 'Authorize' button above → paste token."
			});

			// Include XML comments for endpoint descriptions
			var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
			var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
			if (File.Exists(xmlPath))
				c.IncludeXmlComments(xmlPath);

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
		builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
		builder.Services.AddScoped<IJiraIntegrationRepository, JiraIntegrationRepository>();
		builder.Services.AddScoped<IJiraIssueRepository, JiraIssueRepository>();
		builder.Services.AddScoped<IRequirementRepository, RequirementRepository>();

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
		// Identifier resolver — converts group codes, emails, etc. to internal IDs
		builder.Services.AddScoped<BLL.Helpers.IdentifierResolver>();

        var app = builder.Build();

		using (var scope = app.Services.CreateScope())
		{
			var context = scope.ServiceProvider.GetRequiredService<JgmsContext>();
			DbInitializer.SeedAdmin(context);
		}

		// Run SQL migrations on every startup — only unapplied files are executed
		using (var scope = app.Services.CreateScope())
		{
			var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
			var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;

			// Resolve the migrations folder relative to the app's content root
			var migrationsFolder = Path.Combine(app.Environment.ContentRootPath, "migrations");
			MigrationRunner.Run(connectionString, migrationsFolder, logger);
		}

		// Swagger — available in all environments for team access
		app.UseSwagger();
		app.UseSwaggerUI(c =>
		{
			c.SwaggerEndpoint("/swagger/v1/swagger.json", "SWP391 JGMS API v1");
			c.RoutePrefix = "swagger";
		});

		if (!app.Environment.IsDevelopment())
		{
			app.UseHttpsRedirection();
		}
		app.UseCors("AllowFrontend");
		app.UseAuthentication();
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
