// backend/GestionDocuments.API/Program.cs
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using GestionDocuments.API.Models;
using GestionDocuments.API.Services;

var builder = WebApplication.CreateBuilder(args);

// Entity Framework
builder.Services.AddDbContext<ApplicationDbContext>(o =>
    o.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions => sqlOptions.MigrationsHistoryTable("EFMigrationsHistory")));

builder.Services.AddScoped<IFileOrganizationService, FileOrganizationService>();

// Authentification JWT
var jwt = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => {
        o.TokenValidationParameters = new TokenValidationParameters {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt["Issuer"],
            ValidAudience = jwt["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwt["SecretKey"]!))
        };

        // CORRECTIF TÉLÉCHARGEMENT / EXPORT CSV :
        // Le navigateur ne peut pas envoyer un header Authorization sur un lien
        // <a href="..."> ou window.open(). Pour les endpoints de téléchargement
        // de fichiers et d'export CSV, on accepte aussi le token JWT passé en
        // query parameter (?token=...).
        // SÉCURITÉ : cela expose le token dans l'URL (visible dans les logs serveur
        // et l'historique navigateur). Acceptable pour un usage interne/scolaire.
        // En production, préférer un endpoint qui retourne une URL signée à durée
        // limitée (signed URL / pre-signed URL).
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // Lire le token depuis ?token= si le header Authorization est absent
                var tokenFromQuery = context.Request.Query["token"].FirstOrDefault();
                if (!string.IsNullOrEmpty(tokenFromQuery))
                {
                    context.Token = tokenFromQuery;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// CORS pour Angular (web + mobile Capacitor)
builder.Services.AddCors(o => o.AddPolicy("Angular", p =>
    p.AllowAnyOrigin()
     .AllowAnyHeader()
     .AllowAnyMethod()));

builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<FormOptions>(o => {
    o.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 Mo
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddScoped<IFileOrganizationService, FileOrganizationService>();
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Angular");
app.UseAuthentication();   // ← doit être avant UseAuthorization
app.UseAuthorization();
app.MapControllers();

app.Run();