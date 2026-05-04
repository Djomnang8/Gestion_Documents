using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;

    public AuthController(ApplicationDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        // 1. Trouver l'utilisateur par email
        var utilisateur = await _db.Utilisateurs
            .Include(u => u.UtilisateursRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolesPermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Email == req.Email);

        if (utilisateur == null)
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });

        if (!utilisateur.EstActif)
            return Unauthorized(new { message = "Compte désactivé. Contactez l'administrateur." });

        if (utilisateur.EstListeNoire)
            return Unauthorized(new { message = "Accès bloqué. Contactez l'administrateur." });

        // 2. Vérifier le mot de passe en clair
        if (utilisateur.MotDePasseHash != req.MotDePasse)
            return Unauthorized(new { message = "Email ou mot de passe incorrect." });

        // 3. Récupérer le rôle et les permissions
        var role = utilisateur.UtilisateursRoles
            .FirstOrDefault()?.Role;

        var permissions = utilisateur.UtilisateursRoles
            .SelectMany(ur => ur.Role.RolesPermissions)
            .Select(rp => rp.Permission.Nom)
            .Distinct()
            .ToList();

        // 4. Générer le token JWT
        var token = GenererToken(utilisateur, role?.Nom ?? "", permissions);

        // 5. Mettre à jour la dernière connexion
        utilisateur.DerniereConnexion = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(new LoginResponse
        {
            Token = token,
            User = new UserDto
            {
                Id = utilisateur.Id,
                Nom = utilisateur.Nom,
                Prenom = utilisateur.Prenom,
                Email = utilisateur.Email,
                Role = role?.Nom ?? "",
                Permissions = permissions,
                ServiceId = utilisateur.ServiceId
            }
        });
    }

    private string GenererToken(Utilisateur user, string role, List<string> permissions)
    {
        var jwt = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwt["SecretKey"]!));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new("nom", user.Nom),
            new("prenom", user.Prenom),
            new("role", role),
            new("serviceId", user.ServiceId?.ToString() ?? "")
        };

        // Ajouter chaque permission comme claim
        foreach (var perm in permissions)
            claims.Add(new Claim("permission", perm));

        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: jwt["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                int.Parse(jwt["ExpirationMinutes"] ?? "60")),
            signingCredentials: new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256)
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// DTOs
public record LoginRequest(string Email, string MotDePasse);

public class LoginResponse
{
    public string Token { get; set; } = "";
    public UserDto User { get; set; } = null!;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
    public List<string> Permissions { get; set; } = [];
    public int? ServiceId { get; set; }
}