using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using GestionDocuments.API.Models;

[ApiController]
[Route("api/archivage")]
[Authorize]
public class ArchivageController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ArchivageController> _logger;

    public ArchivageController(ApplicationDbContext db, ILogger<ArchivageController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ─── GET /api/archivage/kpi ─────────────────────────────────────────────
    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi()
    {
        var aujourd = DateTime.UtcNow.Date;
        var debutMois = new DateTime(aujourd.Year, aujourd.Month, 1);

        var aArchiver = await _db.Dossiers.CountAsync(d => d.Statut.Code == "TERMINE");
        var archivesCeMois = await _db.Dossiers
            .CountAsync(d => d.Statut.Code == "ARCHIVE" && d.DateArchivage != null && d.DateArchivage.Value >= debutMois);
        var totalArchives = await _db.Dossiers.CountAsync(d => d.Statut.Code == "ARCHIVE");

        return Ok(new { aArchiver, archivesCeMois, totalArchives });
    }

    // ─── GET /api/archivage/a-archiver ──────────────────────────────────────
    [HttpGet("a-archiver")]
    public async Task<IActionResult> GetDossiersAAArchiver()
    {
        var dossiers = await _db.Dossiers
            .Include(d => d.Service)
            .Where(d => d.Statut.Code == "TERMINE")
            .OrderByDescending(d => d.DateMiseAJourStatut)
            .Select(d => new DossierAArchiverDto
            {
                Id = d.Id,
                Numero = d.Numero,
                Titre = d.Titre,
                Citoyen = d.NomCitoyen,
                Email = d.EmailCitoyen ?? "",
                DateFin = d.DateMiseAJourStatut,
                Service = d.Service != null ? d.Service.Nom : "",
                NbDocuments = d.VersionsDocument.Count
            })
            .ToListAsync();

        return Ok(dossiers);
    }

    // ─── POST /api/archivage/{dossierId} ────────────────────────────────────
    // Identifie le même citoyen par NomCitoyen + EmailCitoyen + ServiceId
    // Crée une nouvelle version d'archive si le citoyen a déjà des archives pour ce service
    [HttpPost("{dossierId:guid}")]
    public async Task<IActionResult> ArchiverDossier(Guid dossierId)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.Statut)
            .Include(d => d.VersionsDocument)
            .FirstOrDefaultAsync(d => d.Id == dossierId);

        if (dossier == null)
            return NotFound(new { error = "Dossier introuvable." });

        if (dossier.Statut.Code != "TERMINE")
            return BadRequest(new { error = "Seuls les dossiers terminés peuvent être archivés." });

        var statutArchive = await _db.StatutsDossier.FirstOrDefaultAsync(s => s.Code == "ARCHIVE");
        if (statutArchive == null)
            return BadRequest(new { error = "Statut 'ARCHIVE' non défini." });

        // Recherche des archives existantes du même citoyen (nom+email) pour le même service
        var archivesExistantes = await _db.Dossiers
            .Where(d => d.NomCitoyen == dossier.NomCitoyen
                     && d.EmailCitoyen == dossier.EmailCitoyen
                     && d.ServiceId == dossier.ServiceId
                     && d.Statut.Code == "ARCHIVE"
                     && d.Id != dossierId)
            .OrderBy(d => d.NumeroVersionArchive)
            .ToListAsync();

        Guid groupeId;
        int nouvelleVersion;

        if (archivesExistantes.Any())
        {
            // Même citoyen + même service : nouvelle version d'archive
            // Récupérer ou créer le GroupeArchiveId
            groupeId = archivesExistantes.FirstOrDefault(d => d.GroupeArchiveId != null)?.GroupeArchiveId
                       ?? Guid.NewGuid();

            // Attribuer le groupeId aux archives sans groupe et corriger les numéros de version
            int versionBase = 1;
            foreach (var arch in archivesExistantes.OrderBy(d => d.DateArchivage ?? d.DateMiseAJourStatut))
            {
                arch.GroupeArchiveId = groupeId;
                if (arch.NumeroVersionArchive == 0)
                    arch.NumeroVersionArchive = versionBase++;
                arch.EstVersionActive = false;  // La nouvelle version sera active
            }

            var maxVersion = archivesExistantes.Max(d => d.NumeroVersionArchive);
            nouvelleVersion = maxVersion + 1;
        }
        else
        {
            // Premier archivage pour ce citoyen + service
            groupeId = Guid.NewGuid();
            nouvelleVersion = 1;
        }

        var ancienStatutId = dossier.StatutId;
        dossier.StatutId = statutArchive.Id;
        dossier.DateArchivage = DateTime.UtcNow;
        dossier.DateMiseAJourStatut = DateTime.UtcNow;
        dossier.GroupeArchiveId = groupeId;
        dossier.NumeroVersionArchive = nouvelleVersion;
        dossier.EstVersionActive = true;

        _db.HistoriqueStatuts.Add(new HistoriqueStatut
        {
            DossierId = dossier.Id,
            AncienStatutId = ancienStatutId,
            NouveauStatutId = statutArchive.Id,
            Commentaire = $"Archivé — Version {nouvelleVersion}",
            DateChangement = DateTime.UtcNow,
            AgentId = ParseUserId(User)
        });

        await _db.SaveChangesAsync();

        var userId = ParseUserId(User);
        if (userId.HasValue)
        {
            _db.Journaux.Add(new Journal
            {
                UtilisateurId = userId.Value,
                Module = "Archivage",
                Action = "ARCHIVAGE",
                Details = $"Dossier {dossier.Numero} archivé — Version {nouvelleVersion} (citoyen: {dossier.NomCitoyen}, email: {dossier.EmailCitoyen})",
                DateAction = DateTime.UtcNow,
                NiveauId = 1
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new
        {
            message = $"Dossier {dossier.Numero} archivé en Version {nouvelleVersion}.",
            numeroVersion = nouvelleVersion,
            groupeArchiveId = groupeId
        });
    }

    // ─── POST /api/archivage/{dossierId}/restaurer-version ──────────────────
    // Rend une version archivée active → ses fichiers apparaissent dans Archives Consultables
    [HttpPost("{dossierId:guid}/restaurer-version")]
    public async Task<IActionResult> RestaurerVersion(Guid dossierId)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.Statut)
            .FirstOrDefaultAsync(d => d.Id == dossierId);

        if (dossier == null)
            return NotFound(new { error = "Dossier introuvable." });

        if (dossier.Statut.Code != "ARCHIVE")
            return BadRequest(new { error = "Ce dossier n'est pas archivé." });

        if (dossier.EstVersionActive)
            return BadRequest(new { error = "Cette version est déjà la version active." });

        if (dossier.GroupeArchiveId == null)
            return BadRequest(new { error = "Ce dossier n'appartient à aucun groupe d'archive." });

        // Désactiver toutes les versions du groupe
        var groupeId = dossier.GroupeArchiveId.Value;
        var tousLesDossiers = await _db.Dossiers
            .Where(d => d.GroupeArchiveId == groupeId)
            .ToListAsync();

        foreach (var d in tousLesDossiers)
            d.EstVersionActive = false;

        // Activer la version choisie
        dossier.EstVersionActive = true;
        await _db.SaveChangesAsync();

        var userId = ParseUserId(User);
        if (userId.HasValue)
        {
            _db.Journaux.Add(new Journal
            {
                UtilisateurId = userId.Value,
                Module = "Archivage",
                Action = "RESTAURATION_VERSION_ARCHIVE",
                Details = $"Version {dossier.NumeroVersionArchive} ({dossier.Numero}) restaurée comme version active du groupe {groupeId}",
                DateAction = DateTime.UtcNow,
                NiveauId = 1
            });
            await _db.SaveChangesAsync();
        }

        return Ok(new { message = $"Version {dossier.NumeroVersionArchive} restaurée avec succès. Ses fichiers sont maintenant visibles dans Archives Consultables." });
    }

    // ─── GET /api/archivage/historique-versions ─────────────────────────────
    // Retourne tous les dossiers archivés groupés : service → citoyen → versions
    // Chaque version contient ses fichiers (VersionDocument)
    [HttpGet("historique-versions")]
    public async Task<IActionResult> GetHistoriqueVersions()
    {
        var dossiers = await _db.Dossiers
            .Include(d => d.Service)
            .Include(d => d.VersionsDocument)
            .Where(d => d.Statut.Code == "ARCHIVE")
            .OrderBy(d => d.Service.Nom)
            .ThenBy(d => d.NomCitoyen)
            .ThenBy(d => d.NumeroVersionArchive)
            .ToListAsync();

        var groupes = dossiers
            .GroupBy(d => d.Service?.Nom ?? "Sans service")
            .OrderBy(g => g.Key)
            .Select(gService => new
            {
                nomService = gService.Key,
                citoyens = gService
                    .GroupBy(d => new { Nom = d.NomCitoyen, Email = d.EmailCitoyen ?? "" })
                    .OrderBy(g => g.Key.Nom)
                    .Select(gCitoyen => new
                    {
                        nomCitoyen = gCitoyen.Key.Nom,
                        emailCitoyen = gCitoyen.Key.Email,
                        groupeArchiveId = gCitoyen.FirstOrDefault(d => d.GroupeArchiveId != null)?.GroupeArchiveId,
                        versions = gCitoyen
                            .OrderBy(d => d.NumeroVersionArchive)
                            .Select(d => new
                            {
                                numero = d.NumeroVersionArchive > 0 ? d.NumeroVersionArchive : 1,
                                dossierId = d.Id,
                                dossierNumero = d.Numero,
                                dossierTitre = d.Titre,
                                dateArchivage = d.DateArchivage ?? d.DateMiseAJourStatut,
                                estActive = d.EstVersionActive,
                                fichiers = d.VersionsDocument
                                    .Select(v => new
                                    {
                                        id = v.Id,
                                        nomFichier = v.NomFichier,
                                        cheminFichier = v.CheminFichier,
                                        typeFichier = v.TypeFichier ?? "",
                                        tailleFichier = v.TailleFichier ?? 0L
                                    }).ToList()
                            }).ToList()
                    }).ToList()
            }).ToList();

        return Ok(groupes);
    }

    // ─── GET /api/archivage/archives-consultables ───────────────────────────
    // Retourne seulement les versions actives, groupées : service → citoyen
    // Avec les fichiers de la version active pour consultation
    [HttpGet("archives-consultables")]
    public async Task<IActionResult> GetArchivesConsultables()
    {
        // Dossiers archivés avec version active
        var dossiersActifs = await _db.Dossiers
            .Include(d => d.Service)
            .Include(d => d.VersionsDocument)
            .Where(d => d.Statut.Code == "ARCHIVE" && d.EstVersionActive)
            .OrderBy(d => d.Service.Nom)
            .ThenBy(d => d.NomCitoyen)
            .ToListAsync();

        // Dossiers archivés sans GroupeArchiveId (archives legacy)
        var dossiersLegacy = await _db.Dossiers
            .Include(d => d.Service)
            .Include(d => d.VersionsDocument)
            .Where(d => d.Statut.Code == "ARCHIVE" && d.GroupeArchiveId == null)
            .OrderBy(d => d.Service.Nom)
            .ThenBy(d => d.NomCitoyen)
            .ToListAsync();

        var tousLesDossiers = dossiersActifs
            .Concat(dossiersLegacy.Where(leg => !dossiersActifs.Any(a => a.Id == leg.Id)))
            .OrderBy(d => d.Service?.Nom ?? "")
            .ThenBy(d => d.NomCitoyen)
            .ToList();

        var groupes = tousLesDossiers
            .GroupBy(d => d.Service?.Nom ?? "Sans service")
            .OrderBy(g => g.Key)
            .Select(gService => new
            {
                nomService = gService.Key,
                citoyens = gService
                    .GroupBy(d => new { Nom = d.NomCitoyen, Email = d.EmailCitoyen ?? "" })
                    .OrderBy(g => g.Key.Nom)
                    .Select(gCitoyen =>
                    {
                        var dossier = gCitoyen.OrderByDescending(d => d.NumeroVersionArchive).First();
                        return new
                        {
                            nomCitoyen = gCitoyen.Key.Nom,
                            emailCitoyen = gCitoyen.Key.Email,
                            groupeArchiveId = dossier.GroupeArchiveId,
                            dossierId = dossier.Id,
                            dossierNumero = dossier.Numero,
                            dossierTitre = dossier.Titre,
                            dateArchivage = dossier.DateArchivage ?? dossier.DateMiseAJourStatut,
                            numeroVersionActive = dossier.NumeroVersionArchive > 0 ? dossier.NumeroVersionArchive : 1,
                            fichiers = dossier.VersionsDocument.Select(v => new
                            {
                                id = v.Id,
                                nomFichier = v.NomFichier,
                                cheminFichier = v.CheminFichier,
                                typeFichier = v.TypeFichier ?? "",
                                tailleFichier = v.TailleFichier ?? 0L
                            }).ToList()
                        };
                    }).ToList()
            }).ToList();

        return Ok(groupes);
    }

    // ─── GET /api/archivage/archives (compatibilité) ─────────────────────────
    [HttpGet("archives")]
    public async Task<IActionResult> RechercherArchives(
        [FromQuery] string? numero,
        [FromQuery] int? serviceId,
        [FromQuery] DateTime? dateDebut,
        [FromQuery] DateTime? dateFin,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20)
    {
        var query = _db.Dossiers
            .Include(d => d.Service)
            .Where(d => d.Statut.Code == "ARCHIVE")
            .AsQueryable();

        if (!string.IsNullOrEmpty(numero))
            query = query.Where(d => d.Numero.Contains(numero) || d.NomCitoyen.Contains(numero));
        if (serviceId.HasValue)
            query = query.Where(d => d.ServiceId == serviceId.Value);
        if (dateDebut.HasValue)
            query = query.Where(d => d.DateArchivage >= dateDebut.Value);
        if (dateFin.HasValue)
            query = query.Where(d => d.DateArchivage <= dateFin.Value);

        var total = await query.CountAsync();
        var archives = await query
            .OrderByDescending(d => d.DateArchivage)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(d => new DossierArchiveDto
            {
                Id = d.Id,
                Numero = d.Numero,
                Titre = d.Titre,
                Citoyen = d.NomCitoyen,
                DateArchivage = d.DateArchivage ?? d.DateMiseAJourStatut,
                Service = d.Service != null ? d.Service.Nom : "",
                NbDocuments = d.VersionsDocument.Count,
                Description = d.Description ?? ""
            })
            .ToListAsync();

        return Ok(new { data = archives, total });
    }

    private static Guid? ParseUserId(System.Security.Principal.IPrincipal user)
    {
        var str = (user as System.Security.Claims.ClaimsPrincipal)
            ?.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
        return Guid.TryParse(str, out var g) ? g : null;
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────────
public class DossierAArchiverDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Citoyen { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime DateFin { get; set; }
    public string Service { get; set; } = "";
    public int NbDocuments { get; set; }
}

public class DossierArchiveDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string Titre { get; set; } = "";
    public string Citoyen { get; set; } = "";
    public DateTime DateArchivage { get; set; }
    public string Service { get; set; } = "";
    public int NbDocuments { get; set; }
    public string Description { get; set; } = "";
}
