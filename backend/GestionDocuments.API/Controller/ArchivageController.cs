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

    // ─── Helper : notification aux archivistes ──────────────────────────────
    private async Task NotifierArchivistes(string titre, string message, string type, Guid? dossierId = null, string? numeroDossier = null)
    {
        var archivistes = await _db.Utilisateurs
            .Where(u => u.TypeUtilisateur == "Archiviste" && u.EstActif && !u.EstListeNoire && !u.EstSupprime)
            .Select(u => u.Id)
            .ToListAsync();

        foreach (var archId in archivistes)
        {
            _db.Notifications.Add(new Notification
            {
                UtilisateurId = archId,
                Titre = titre,
                Message = message,
                Type = type,
                DossierId = dossierId,
                NumeroDossier = numeroDossier,
                DateCreation = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }

    // ─── GET /api/archivage/kpi ─────────────────────────────────────────────
    [HttpGet("kpi")]
    public async Task<IActionResult> GetKpi()
    {
        var aujourd = DateTime.UtcNow.Date;
        var debutMois = new DateTime(aujourd.Year, aujourd.Month, 1);

        var aArchiver = await _db.Dossiers
            .CountAsync(d => d.Statut.Code == "TERMINE");

        var archivesCeMois = await _db.Dossiers
            .CountAsync(d => d.Statut.Code == "ARCHIVE" && d.DateArchivage != null && d.DateArchivage.Value >= debutMois);

        var totalArchives = await _db.Dossiers
            .CountAsync(d => d.Statut.Code == "ARCHIVE");

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

    // ─── POST /api/archivage/{dossierId} (avec notification archiviste) ────
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

        // ── Fusion si même email ───────────────────────────────────────────
        Dossier? dossierCible = null;
        if (!string.IsNullOrEmpty(dossier.EmailCitoyen))
        {
            dossierCible = await _db.Dossiers
                .Include(d => d.Statut)
                .Where(d => d.EmailCitoyen == dossier.EmailCitoyen
                         && d.Statut.Code == "ARCHIVE"
                         && d.Id != dossierId)
                .OrderByDescending(d => d.DateArchivage)
                .FirstOrDefaultAsync();
        }

        if (dossierCible != null)
        {
            // Fusion : rattacher les versions au dossier archivé existant
            foreach (var version in dossier.VersionsDocument)
            {
                version.DossierId = dossierCible.Id;
            }

            var statutTermine = await _db.StatutsDossier.FirstOrDefaultAsync(s => s.Code == "TERMINE");
            if (statutTermine != null)
                dossier.StatutId = statutTermine.Id;

            await _db.SaveChangesAsync();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null && Guid.TryParse(userId, out var gId))
            {
                _db.Journaux.Add(new Journal
                {
                    UtilisateurId = gId,
                    Module = "Archivage",
                    Action = "ARCHIVAGE_FUSION",
                    Details = $"Dossier {dossier.Numero} fusionné dans {dossierCible.Numero} (même email : {dossier.EmailCitoyen})",
                    DateAction = DateTime.UtcNow,
                    NiveauId = 1
                });
                await _db.SaveChangesAsync();
            }

            // Notification aux archivistes (fusion)
            await NotifierArchivistes(
                "Fusion d'archives",
                $"Le dossier {dossier.Numero} a été fusionné avec l'archive existante {dossierCible.Numero} (même citoyen).",
                "ARCHIVAGE",
                dossierCible.Id,
                dossierCible.Numero
            );

            return Ok(new
            {
                message = $"Dossier fusionné dans l'archive existante {dossierCible.Numero}.",
                fusionne = true,
                dossierId = dossierCible.Id
            });
        }

        // ── Archivage normal (premier dossier de cet email) ─────────────────
        var ancienStatutId = dossier.StatutId;

        dossier.StatutId = statutArchive.Id;
        dossier.DateArchivage = DateTime.UtcNow;
        dossier.DateMiseAJourStatut = DateTime.UtcNow;

        _db.HistoriqueStatuts.Add(new HistoriqueStatut
        {
            DossierId = dossier.Id,
            AncienStatutId = ancienStatutId,
            NouveauStatutId = statutArchive.Id,
            Commentaire = "Archivage définitif",
            DateChangement = DateTime.UtcNow,
            AgentId = User.FindFirstValue(ClaimTypes.NameIdentifier) != null ? Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) : null
        });

        await _db.SaveChangesAsync();

        var userIdNormal = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdNormal != null && Guid.TryParse(userIdNormal, out var gIdNormal))
        {
            _db.Journaux.Add(new Journal
            {
                UtilisateurId = gIdNormal,
                Module = "Archivage",
                Action = "ARCHIVAGE_NOUVEAU",
                Details = $"Dossier {dossier.Numero} archivé (premier dossier pour {dossier.EmailCitoyen})",
                DateAction = DateTime.UtcNow,
                NiveauId = 1
            });
            await _db.SaveChangesAsync();
        }

        // Notification aux archivistes
        await NotifierArchivistes(
            "Nouvelle archive",
            $"Le dossier {dossier.Numero} a été archivé.",
            "ARCHIVAGE",
            dossier.Id,
            dossier.Numero
        );

        return Ok(new { message = $"Dossier {dossier.Numero} archivé avec succès.", fusionne = false });
    }

    // ─── GET /api/archivage/archives ────────────────────────────────────────
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
            query = query.Where(d => d.Numero.Contains(numero));
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
}

// DTOs
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