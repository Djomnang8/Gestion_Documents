// backend/GestionDocuments.API/Controller/UtilisateursController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GestionDocuments.API.Models;

[ApiController]
[Route("api/utilisateurs")]
[Authorize]
public class UtilisateursController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public UtilisateursController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? statut = null)
    {
        var query = _db.Utilisateurs
            .Include(u => u.UtilisateursRoles).ThenInclude(ur => ur.Role)
            .Where(u => !u.EstSupprime)
            .AsQueryable();
        query = statut switch
        {
            "actif"      => query.Where(u => u.EstActif && !u.EstListeNoire),
            "inactif"    => query.Where(u => !u.EstActif && !u.EstListeNoire),
            "listenoire" => query.Where(u => u.EstListeNoire),
            _            => query
        };
        var result = await query.Select(u => new UtilisateurListeDto
        {
            Id = u.Id, Nom = u.Nom, Prenom = u.Prenom, Email = u.Email,
            Telephone = u.Telephone,
            Role = u.UtilisateursRoles.Select(ur => ur.Role.Nom).FirstOrDefault() ?? "",
            ServiceId = u.ServiceId, EstActif = u.EstActif,
            EstListeNoire = u.EstListeNoire, MotifListeNoire = u.MotifListeNoire,
            DerniereConnexion = u.DerniereConnexion, TypeUtilisateur = u.TypeUtilisateur
        }).ToListAsync();
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var u = await _db.Utilisateurs
            .Include(u => u.UtilisateursRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);
        if (u == null) return NotFound();
        return Ok(new UtilisateurListeDto
        {
            Id = u.Id, Nom = u.Nom, Prenom = u.Prenom, Email = u.Email,
            Telephone = u.Telephone,
            Role = u.UtilisateursRoles.Select(ur => ur.Role.Nom).FirstOrDefault() ?? "",
            ServiceId = u.ServiceId, EstActif = u.EstActif,
            EstListeNoire = u.EstListeNoire, MotifListeNoire = u.MotifListeNoire,
            DerniereConnexion = u.DerniereConnexion, TypeUtilisateur = u.TypeUtilisateur
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUtilisateurDto dto)
    {
        if (await _db.Utilisateurs.AnyAsync(u => u.Email == dto.Email && !u.EstSupprime))
            return BadRequest(new { message = "Cet email est déjà utilisé." });
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Nom == dto.Role);
        if (role == null) return BadRequest(new { message = "Rôle introuvable." });
        var utilisateur = new Utilisateur
        {
            Id = Guid.NewGuid(), Nom = dto.Nom, Prenom = dto.Prenom,
            Email = dto.Email, Telephone = dto.Telephone,
            MotDePasseHash = BCrypt.Net.BCrypt.HashPassword(dto.MotDePasse),
            EstActif = true, EstListeNoire = false,
            ServiceId = dto.ServiceId, TypeUtilisateur = dto.Role, EstSupprime = false
        };
        _db.Utilisateurs.Add(utilisateur);
        await _db.SaveChangesAsync();
        _db.UtilisateursRoles.Add(new UtilisateurRole { UtilisateurId = utilisateur.Id, RoleId = role.Id });
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.creer", $"Création utilisateur {utilisateur.Email}");
        return CreatedAtAction(nameof(GetById), new { id = utilisateur.Id },
            new { message = "Utilisateur créé.", id = utilisateur.Id });
    }

    // ── NOUVEAU : Modifier un utilisateur ──────────────────────────
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Modifier(Guid id, [FromBody] ModifierUtilisateurDto dto)
    {
        var u = await _db.Utilisateurs
            .Include(u => u.UtilisateursRoles)
            .FirstOrDefaultAsync(u => u.Id == id && !u.EstSupprime);
        if (u == null) return NotFound(new { message = "Utilisateur introuvable." });

        // Vérifier unicité email si changé
        if (!string.Equals(u.Email, dto.Email, StringComparison.OrdinalIgnoreCase)
            && await _db.Utilisateurs.AnyAsync(x => x.Email == dto.Email && !x.EstSupprime && x.Id != id))
            return BadRequest(new { message = "Cet email est déjà utilisé." });

        u.Nom       = dto.Nom;
        u.Prenom    = dto.Prenom;
        u.Email     = dto.Email;
        u.Telephone = dto.Telephone;
        if (dto.ServiceId.HasValue) u.ServiceId = dto.ServiceId;

        // Changer le rôle si fourni
        if (!string.IsNullOrWhiteSpace(dto.Role))
        {
            var role = await _db.Roles.FirstOrDefaultAsync(r => r.Nom == dto.Role);
            if (role == null) return BadRequest(new { message = "Rôle introuvable." });
            _db.UtilisateursRoles.RemoveRange(u.UtilisateursRoles);
            _db.UtilisateursRoles.Add(new UtilisateurRole { UtilisateurId = id, RoleId = role.Id });
            u.TypeUtilisateur = dto.Role;
        }
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.modifier", $"Modification {u.Email}");
        return Ok(new { message = "Utilisateur modifié avec succès." });
    }

    [HttpPut("{id:guid}/activer")]
    public async Task<IActionResult> Activer(Guid id)
    {
        var u = await _db.Utilisateurs.FindAsync(id);
        if (u == null) return NotFound();
        u.EstActif = true; u.EstListeNoire = false; u.MotifListeNoire = null;
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.activer", $"Activation {u.Email}");
        return Ok(new { message = "Compte activé." });
    }

    [HttpPut("{id:guid}/desactiver")]
    public async Task<IActionResult> Desactiver(Guid id)
    {
        var u = await _db.Utilisateurs.FindAsync(id);
        if (u == null) return NotFound();
        u.EstActif = false;
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.desactiver", $"Désactivation {u.Email}");
        return Ok(new { message = "Compte désactivé." });
    }

    [HttpPut("{id:guid}/listenoire")]
    public async Task<IActionResult> ListeNoire(Guid id, [FromBody] ListeNoireDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Motif))
            return BadRequest(new { message = "Le motif est obligatoire." });
        var u = await _db.Utilisateurs.FindAsync(id);
        if (u == null) return NotFound();
        u.EstListeNoire = true; u.EstActif = false; u.MotifListeNoire = dto.Motif;
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.blacklist", $"Liste noire {u.Email}");
        return Ok(new { message = "Utilisateur mis en liste noire." });
    }

    [HttpPut("{id:guid}/mot-de-passe")]
    public async Task<IActionResult> ChangerMotDePasse(Guid id, [FromBody] ChangerMotDePasseDto dto)
    {
        var u = await _db.Utilisateurs.FindAsync(id);
        if (u == null) return NotFound(new { message = "Utilisateur introuvable." });

        // Vérifier que l'appelant est bien le propriétaire du compte
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null || !Guid.TryParse(userId, out var callerGuid) || callerGuid != id)
            return Forbid();

        // Comparaison en clair (cohérent avec AuthController.Login)
        if (u.MotDePasseHash != dto.AncienMotDePasse)
            return BadRequest(new { message = "Ancien mot de passe incorrect." });

        if (string.IsNullOrWhiteSpace(dto.NouveauMotDePasse) || dto.NouveauMotDePasse.Length < 6)
            return BadRequest(new { message = "Le nouveau mot de passe doit contenir au moins 6 caractères." });

        u.MotDePasseHash = dto.NouveauMotDePasse;
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.motdepasse", $"Changement de mot de passe {u.Email}");
        return Ok(new { message = "Mot de passe mis à jour avec succès." });
    }

    [HttpPut("{id:guid}/role")]
    public async Task<IActionResult> ChangerRole(Guid id, [FromBody] ChangerRoleDto dto)
    {
        var u = await _db.Utilisateurs.Include(u => u.UtilisateursRoles).FirstOrDefaultAsync(u => u.Id == id);
        if (u == null) return NotFound();
        var role = await _db.Roles.FirstOrDefaultAsync(r => r.Nom == dto.Role);
        if (role == null) return BadRequest(new { message = "Rôle introuvable." });
        _db.UtilisateursRoles.RemoveRange(u.UtilisateursRoles);
        _db.UtilisateursRoles.Add(new UtilisateurRole { UtilisateurId = id, RoleId = role.Id });
        u.TypeUtilisateur = dto.Role;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Rôle mis à jour." });
    }

    [HttpPut("{id:guid}/service")]
    public async Task<IActionResult> ChangerService(Guid id, [FromBody] ChangerServiceDto dto)
    {
        var u = await _db.Utilisateurs.Include(u => u.UtilisateursRoles)
                                       .ThenInclude(ur => ur.Role)
                                       .FirstOrDefaultAsync(u => u.Id == id);
        if (u == null) return NotFound(new { message = "Utilisateur introuvable." });

        // Vérifier que l'utilisateur est bien un Agent
        var role = u.UtilisateursRoles.FirstOrDefault()?.Role?.Nom ?? u.TypeUtilisateur;
        if (!string.Equals(role, "Agent", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Seuls les agents peuvent être affectés à un service via cette action." });

        if (dto.ServiceId.HasValue)
        {
            var serviceExiste = await _db.Services.AnyAsync(s => s.Id == dto.ServiceId.Value);
            if (!serviceExiste) return BadRequest(new { message = "Service introuvable." });
        }

        u.ServiceId = dto.ServiceId;
        await _db.SaveChangesAsync();
        await LogAction("utilisateurs.service", $"Service de {u.Email} modifié → {dto.ServiceId?.ToString() ?? "aucun"}");
        return Ok(new { message = "Service mis à jour." });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoles()
    {
        var roles = await _db.Roles
            .Include(r => r.RolesPermissions).ThenInclude(rp => rp.Permission)
            .Select(r => new RoleDto
            {
                Id = r.Id, Nom = r.Nom, Description = r.Description,
                Permissions = r.RolesPermissions.Select(rp => rp.Permission.Nom).ToList()
            }).ToListAsync();
        return Ok(roles);
    }

    [HttpGet("services")]
    public async Task<IActionResult> GetServices()
        => Ok(await _db.Services.Select(s => new { s.Id, s.Nom, s.Description }).ToListAsync());

    private async Task LogAction(string action, string details)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _db.Journaux.Add(new Journal
        {
            UtilisateurId = userId != null ? Guid.Parse(userId) : null,
            Module = "Utilisateurs", Action = action, Details = details,
            NiveauId = 1, DateAction = DateTime.UtcNow,
            AdresseIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();
    }
}

// ── DTOs ────────────────────────────────────────────────────────
public class UtilisateurListeDto
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telephone { get; set; }
    public string Role { get; set; } = "";
    public int? ServiceId { get; set; }
    public bool EstActif { get; set; }
    public bool EstListeNoire { get; set; }
    public string? MotifListeNoire { get; set; }
    public DateTime? DerniereConnexion { get; set; }
    public string TypeUtilisateur { get; set; } = "";
}
public class CreateUtilisateurDto
{
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telephone { get; set; }
    public string MotDePasse { get; set; } = "";
    public string Role { get; set; } = "";
    public int? ServiceId { get; set; }
}
public class ModifierUtilisateurDto
{
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telephone { get; set; }
    public string? Role { get; set; }
    public int? ServiceId { get; set; }
}
public record ListeNoireDto(string Motif);
public record ChangerRoleDto(string Role);
public record ChangerServiceDto(int? ServiceId);
public class ChangerMotDePasseDto
{
    public string AncienMotDePasse { get; set; } = "";
    public string NouveauMotDePasse { get; set; } = "";
}
public class RoleDto
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Description { get; set; }
    public List<string> Permissions { get; set; } = [];
}