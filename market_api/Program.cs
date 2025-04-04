using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Security.Cryptography;
using System.Linq;
using System;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using market_api.Data;
using market_api.Models;
using market_api.Repositories;
using market_api.Services;
using market_api.Controllers;
using CloudinaryDotNet;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

// Register DatabaseContext in DI
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger for OpenAPI 3.0
builder.Services.AddSwaggerGen(options =>
{
    // OpenAPI v3 definition
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Market API",
        Version = "v1",
        Description = "A simple API for managing market operations",
    });

    // Add security definition for JWT authentication
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please insert JWT with Bearer into field",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    // Define the security requirement
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] { }
        }
    });
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();

// Add CORS policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

// Add JWT Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("thisisaverysecureandlongenoughkey123456"))
    };
});

var cloudinarySettings = builder.Configuration.GetSection("Cloudinary").Get<CloudinarySettings>();
var cloudinary = new Cloudinary(new Account(
    cloudinarySettings.CloudName,
    cloudinarySettings.ApiKey,
    cloudinarySettings.ApiSecret
));

builder.Services.AddSingleton(cloudinary);

// Add Authorization
builder.Services.AddAuthorization();

var app = builder.Build();

// Add default CORS policy before authentication middleware
app.UseCors("AllowAll");

// Add admin user on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    context.Database.EnsureCreated();

    if (!context.Users.Any(u => u.Role == "admin"))
    {
        var admin = new User
        {
            Username = "leokey",
            Email = "leokey@gmail.com",
            PasswordHash = HashPassword("aye_sadochok228"),
            Role = "admin",
            CreatedAt = DateTime.UtcNow,
        };

        context.Users.Add(admin);
        context.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    // Configure custom Swagger JSON with OpenAPI version 3.0
    app.UseSwagger(c =>
    {
        c.SerializeAsV2 = false;

        // Inject the version directly into the generated JSON
        c.PreSerializeFilters.Add((swaggerDoc, httpReq) =>
        {
            // Add the OpenAPI version field to the root document
            var dict = new Dictionary<string, object>
            {
                ["openapi"] = "3.0.0"
            };

            // Add each existing property
            foreach (var prop in swaggerDoc.GetType().GetProperties())
            {
                var value = prop.GetValue(swaggerDoc);
                if (value != null)
                {
                    dict[prop.Name.ToLowerInvariant()] = value;
                }
            }

            // Replace the document with our modified version
            // This is a hacky workaround since we can't directly set "openapi" property
            foreach (var prop in swaggerDoc.GetType().GetProperties())
            {
                if (prop.CanWrite && dict.ContainsKey(prop.Name.ToLowerInvariant()))
                {
                    prop.SetValue(swaggerDoc, dict[prop.Name.ToLowerInvariant()]);
                }
            }
        });
    });

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Market API v1");
        c.RoutePrefix = string.Empty;  // This will make Swagger UI available at the root of the application.
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapFallbackToFile("/index.html");
app.Run();

// Password hashing method
static string HashPassword(string password)
{
    using (var sha256 = SHA256.Create())
    {
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return BitConverter.ToString(hashedBytes).Replace("-", "").ToLower();
    }
}