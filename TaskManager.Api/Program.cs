using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskManager.Api.Data;
using TaskManager.Api.Hubs; // ("/taskhub"); // <-- 2. MAP THE HUB ROUTE HERE
using Microsoft.OpenApi;
using Scalar.AspNetCore;
using TaskManager.Api.Services;
using TaskManager.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Register Services 
builder.Services.AddScoped<ITaskRepository, TaskRepository>();
builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<ITaskService, TaskService>();

// 1. Add ASP.NET Core Identity using the built-in IdentityUser
builder.Services.AddIdentityCore<IdentityUser>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// 2. Add Authentication with JWT Bearer configurations
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
    };
});

builder.Services.AddControllers();
builder.Services.AddSignalR();
// builder.Services.AddMemoryCache();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("RedisConnection");
    options.InstanceName = "TaskManager_"; // Prefixes all keys in Redis for organization
});

builder.Services.AddOpenApi(options => {
    options.AddDocumentTransformer((document, context, cancellationToken) => {
        // Safe, clean tree construction using interfaces expected by .NET 10
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Security ??= new List<OpenApiSecurityRequirement>();

        var scheme = new OpenApiSecurityScheme {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token directly."
        };

        // Enforce safe assignment via the interface dictionary
        document.Components.SecuritySchemes["Bearer"] = scheme;

        // Apply globally using type safety
        var requirement = new OpenApiSecurityRequirement {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
        };

        document.Security.Add(requirement);
        return Task.CompletedTask;
    });
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>(); 

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}

// 4. Modern .NET 10 API Documentation Routing Middleware
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // Generates native JSON metadata via /openapi/v1.json
    app.MapScalarApiReference(options => {
        options.WithTitle("Real-Time Task Manager API")
               .WithTheme(ScalarTheme.Moon); // Premium built-in dark theme
    });
}

// ⚠️ FIXED REDIRECTS: Only redirect to HTTPS if running on a configured secure dev port
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// 3. Essential Middleware Order: Authentication MUST come before Authorization
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();
app.MapHub<TaskHub>("/taskhub");

app.Run();