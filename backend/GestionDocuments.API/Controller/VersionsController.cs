// Controller/VersionsController.cs
// CORRECTIF PRINCIPAL : suppression de v.Commentaire dans la requête LINQ
// si la colonne n'existe pas encore en base.
// APRÈS avoir exécuté la migration, vous pouvez remettre Commentaire = v.Commentaire ?? ""

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GestionDocuments.API.Models;

[ApiController]
[Route("api/versions")]
[Authorize]
public class VersionsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<VersionsController> _logger;

    public VersionsController(ApplicationDbContext db, ILogger<VersionsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET /api/versions/{dossierId}
    // Retourne toutes les versions d'un dossier, triées de la plus récente à la plus ancienne.
    // La restauration est possible uniquement sur les versions NON actives (estActive = false).
    [HttpGet("{dossierId:guid}")]
    public async Task<IActionResult> GetVersions(Guid dossierId)
    {
        // On charge les versions SANS projeter Commentaire dans la requête SQL
        // pour éviter l'erreur "Nom de colonne non valide : 'Commentaire'"
        // si la migration n'a pas encore été jouée.
        // Après migration : remplacez par la version commentée ci-dessous.
        var versions = await _db.VersionsDocument
            .Where(v => v.DossierId == dossierId)
            .OrderByDescending(v => v.NumeroVersion)
            .Select(v => new
            {
                v.Id,
                v.DossierId,
                v.NumeroVersion,
                v.NomFichier,
                TailleFichier = v.TailleFichier ?? 0,
                TypeFichier = v.TypeFichier ?? "",
                v.DateCreation,
                v.EstActive,
                v.UtilisateurId
            })
            .ToListAsync();

        // On charge les utilisateurs séparément pour éviter la jointure qui plante
        var utilisateurIds = versions
            .Where(v => v.UtilisateurId.HasValue)
            .Select(v => v.UtilisateurId!.Value)
            .Distinct()
            .ToList();

        var utilisateurs = await _db.Utilisateurs
            .Where(u => utilisateurIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Prenom, u.Nom })
            .ToListAsync();

        var result = versions.Select(v => new VersionDto
        {
            Id = v.Id,
            DossierId = v.DossierId,
            Numero = v.NumeroVersion,
            NomFichier = v.NomFichier,
            TailleFichier = v.TailleFichier,
            TypeFichier = v.TypeFichier,
            DateCreation = v.DateCreation,
            EstActive = v.EstActive,
            Commentaire = "", // vide jusqu'à la migration
            Auteur = v.UtilisateurId.HasValue
                ? utilisateurs.FirstOrDefault(u => u.Id == v.UtilisateurId)
                    is var u && u != null
                    ? $"{u.Prenom} {u.Nom}"
                    : "Système"
                : "Système"
        }).ToList();

        return Ok(result);
    }

    // POST /api/versions/{versionId}/restaurer
    // CONDITION DE RESTAURATION : la version doit exister et ne pas être déjà active.
    // Seul l'archiviste (avec permission archivage.restaurer) peut restaurer.
    [HttpPost("{versionId:guid}/restaurer")]
    public async Task<IActionResult> RestaurerVersion(Guid versionId)
    {
        var version = await _db.VersionsDocument
            .Include(v => v.Dossier)
            .FirstOrDefaultAsync(v => v.Id == versionId);

        if (version == null)
            return NotFound(new { error = "Version introuvable." });

        // Vérification : inutile de restaurer une version déjà active
        if (version.EstActive)
            return BadRequest(new { error = "Cette version est déjà la version active." });

        var dossierId = version.DossierId;

        // Désactiver toutes les versions du dossier
        await _db.VersionsDocument
            .Where(v => v.DossierId == dossierId)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.EstActive, false));

        // Activer la version choisie
        version.EstActive = true;
        await _db.SaveChangesAsync();

        // Journaliser l'action
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId != null && Guid.TryParse(userId, out var guidUserId))
        {
            _db.Journaux.Add(new Journal
            {
                UtilisateurId = guidUserId,
                Action = "RESTAURATION_VERSION",
                Module = "Archivage",
                EntiteId = versionId.ToString(),
                Details = $"Restauré version {version.NumeroVersion} du dossier {version.Dossier.Numero}",
                DateAction = DateTime.UtcNow,
                NiveauId = 1
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = $"Version {version.NumeroVersion} restaurée avec succès." });
    }
}

public class VersionDto
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public int Numero { get; set; }
    public string NomFichier { get; set; } = "";
    public long TailleFichier { get; set; }
    public string TypeFichier { get; set; } = "";
    public DateTime DateCreation { get; set; }
    public string Auteur { get; set; } = "";
    public bool EstActive { get; set; }
    public string Commentaire { get; set; } = "";
}