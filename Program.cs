using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using HabitTrackerBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// Register Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Register PostgreSQL Database
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// Configure Identity Services
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = ""; // Disable redirect to /Account/Login
    options.AccessDeniedPath = ""; // Prevent redirection on unauthorized access
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});


// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtIssuer,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),

            // Ensures "nameid" claim is used as User ID in claims
            NameClaimType = ClaimTypes.NameIdentifier
        };
    });

// Add Google Authentication (Optional)
builder.Services.AddAuthentication()
   .AddGoogle(options =>
   {
       options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
       options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
   });

builder.Services.AddAuthorization();

// Configure Swagger to accept a Bearer Token
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Habit Tracker API", Version = "v1" });

    // Add Authorization header to Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Enter JWT Token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            new string[] { }
        }
    });
});

// Configure Google Play Billing
builder.Services.Configure<GooglePlayBillingOptions>(
    builder.Configuration.GetSection("GooglePlayBilling"));

// Register Services
builder.Services.AddScoped<HabitService>();
builder.Services.AddHttpClient<AiHabitSuggestionService>();
builder.Services.AddScoped<SubscriptionService>();
builder.Services.AddScoped<IGooglePlayBillingService, GooglePlayBillingService>();

// Build the Application
var app = builder.Build();

// Enable Swagger UI
app.UseSwagger();
app.UseSwaggerUI();

// Enable CORS
app.UseCors(policy =>
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader());

// Ensure Authentication is processed before Authorization
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Debug Log to Check If Token is Being Processed
app.Use(async (context, next) =>
{
    var user = context.User;
    if (user.Identity?.IsAuthenticated == true)
    {
        Console.WriteLine("User Authenticated: " + user.Identity.Name);
        Console.WriteLine("Claims:");
        foreach (var claim in user.Claims)
        {
            Console.WriteLine($"{claim.Type}: {claim.Value}");
        }
    }
    else
    {
        Console.WriteLine("No user authenticated.");
    }

    await next();
});

// Map Controllers
app.MapControllers();

// Run the App
app.Run();
