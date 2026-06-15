// Controller/DossiersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using GestionDocuments.API.Models;
using GestionDocuments.API.Services;

[ApiController]
[Route("api/dossiers")]
[Authorize]
public class DossiersController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<DossiersController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly IEmailService _emailService;
    private readonly IFileOrganizationService _fileOrgService;

    public DossiersController(ApplicationDbContext db,
                              ILogger<DossiersController> logger,
                              IWebHostEnvironment env,
                              IEmailService emailService,
                              IFileOrganizationService fileOrgService)
    {
        _db = db;
        _logger = logger;
        _env = env;
        _emailService = emailService;
        _fileOrgService = fileOrgService;
    }

    // ─── Helper ──────────────────────────────────────────────────────────────
    private async Task<int?> GetServiceIdAgentConnecte()
    {
        var role = User.FindFirstValue("role") ?? "";
        if (role == "Administrateur") return null;

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return null;

        var utilisateur = await _db.Utilisateurs.FindAsync(userId);
        return utilisateur?.ServiceId;
    }

    // ─── GET /api/dossiers ────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetDossiers(
        [FromQuery] string? statut,
        [FromQuery] string? recherche,
        [FromQuery] int page = 1,
        [FromQuery] int taille = 20,
        [FromQuery] int? serviceId = null,
        [FromQuery] DateTime? dateDebut = null,
        [FromQuery] DateTime? dateFin = null)
    {
        var query = _db.Dossiers
            .Include(d => d.Statut)
            .Where(d => d.Statut.Code != "ARCHIVE")
            .AsQueryable();

        var serviceAgent = await GetServiceIdAgentConnecte();
        if (serviceAgent.HasValue)
            query = query.Where(d => d.ServiceId == serviceAgent.Value);
        else if (serviceId.HasValue)
            query = query.Where(d => d.ServiceId == serviceId.Value);

        if (!string.IsNullOrEmpty(statut))
            query = query.Where(d => d.Statut.Code == statut);
        if (!string.IsNullOrEmpty(recherche))
            query = query.Where(d =>
                d.Numero.Contains(recherche) ||
                d.Titre.Contains(recherche) ||
                d.NomCitoyen.Contains(recherche));
        if (dateDebut.HasValue)
            query = query.Where(d => d.DateDepot >= dateDebut.Value);
        if (dateFin.HasValue)
            query = query.Where(d => d.DateDepot <= dateFin.Value);

        var total = await query.CountAsync();
        var dossiers = await query
            .OrderByDescending(d => d.DateDepot)
            .Skip((page - 1) * taille)
            .Take(taille)
            .Select(d => new DossierListeDto
            {
                Id = d.Id,
                Numero = d.Numero,
                Titre = d.Titre,
                NomCitoyen = d.NomCitoyen,
                EmailCitoyen = d.EmailCitoyen,
                TelephoneCitoyen = d.TelephoneCitoyen,
                StatutCode = d.Statut.Code,
                StatutLibelle = d.Statut.Libelle,
                DateDepot = d.DateDepot,
                DateMiseAJourStatut = d.DateMiseAJourStatut
            })
            .ToListAsync();

        return Ok(new PageDossiersDto
        {
            Total = total,
            Page = page,
            Taille = taille,
            Dossiers = dossiers
        });
    }

    // ─── GET /api/dossiers/stats ──────────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var aujourd = DateTime.UtcNow.Date;
        var debutSemaine = aujourd.AddDays(-(int)aujourd.DayOfWeek);
        var seuilRetard = DateTime.UtcNow.AddDays(-7);

        var serviceAgent = await GetServiceIdAgentConnecte();

        IQueryable<Dossier> BaseQuery()
        {
            var q = _db.Dossiers.Include(d => d.Statut).AsQueryable();
            if (serviceAgent.HasValue)
                q = q.Where(d => d.ServiceId == serviceAgent.Value);
            return q;
        }

        var total = await BaseQuery().CountAsync(d => d.Statut.Code != "ARCHIVE");
        var parStatut = await BaseQuery().GroupBy(d => d.Statut.Code).Select(g => new { Code = g.Key, Count = g.Count() }).ToListAsync();
        var recusAujourdhui = await BaseQuery().CountAsync(d => d.DateDepot.Date == aujourd && d.Statut.Code != "ARCHIVE");
        var traitesSemine = await BaseQuery().CountAsync(d => d.Statut.Code == "TERMINE" && d.DateMiseAJourStatut >= debutSemaine);
        var enRetard = await BaseQuery().CountAsync(d =>
            (d.Statut.Code == "RECU" || d.Statut.Code == "EN_COURS") &&
            d.DateDepot <= seuilRetard);

        return Ok(new
        {
            Total = total,
            RecusAujourdhui = recusAujourdhui,
            TraitesSemine = traitesSemine,
            EnRetard = enRetard,
            Recu = parStatut.FirstOrDefault(s => s.Code == "RECU")?.Count ?? 0,
            EnCours = parStatut.FirstOrDefault(s => s.Code == "EN_COURS")?.Count ?? 0,
            Transfere = parStatut.FirstOrDefault(s => s.Code == "TRANSFERE")?.Count ?? 0,
            Rejete = parStatut.FirstOrDefault(s => s.Code == "REJETE")?.Count ?? 0,
            Termine = parStatut.FirstOrDefault(s => s.Code == "TERMINE")?.Count ?? 0,
            Archive = parStatut.FirstOrDefault(s => s.Code == "ARCHIVE")?.Count ?? 0,
        });
    }

    // ─── GET /api/dossiers/en-retard ─────────────────────────────────────────
    [HttpGet("en-retard")]
    public async Task<IActionResult> GetEnRetard()
    {
        var seuil = DateTime.UtcNow.AddDays(-7);
        var serviceAgent = await GetServiceIdAgentConnecte();

        var query = _db.Dossiers
            .Include(d => d.Statut)
            .Where(d => (d.Statut.Code == "RECU" || d.Statut.Code == "EN_COURS") && d.DateDepot <= seuil);

        if (serviceAgent.HasValue)
            query = query.Where(d => d.ServiceId == serviceAgent.Value);

        var dossiers = await query
            .OrderBy(d => d.DateDepot)
            .Select(d => new
            {
                Id = d.Id,
                Numero = d.Numero,
                Titre = d.Titre,
                NomCitoyen = d.NomCitoyen,
                StatutCode = d.Statut.Code,
                StatutLibelle = d.Statut.Libelle,
                DateDepot = d.DateDepot,
                DateMiseAJourStatut = d.DateMiseAJourStatut,
                JoursEnRetard = (int)(DateTime.UtcNow - d.DateDepot).TotalDays
            })
            .ToListAsync();
        return Ok(dossiers);
    }

    // ─── GET /api/dossiers/export-csv ────────────────────────────────────────
    [HttpGet("export-csv")]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] string? statut,
        [FromQuery] string? recherche,
        [FromQuery] int? serviceId,
        [FromQuery] DateTime? dateDebut,
        [FromQuery] DateTime? dateFin)
    {
        var query = _db.Dossiers.Include(d => d.Statut).AsQueryable();

        var serviceAgent = await GetServiceIdAgentConnecte();
        if (serviceAgent.HasValue)
            query = query.Where(d => d.ServiceId == serviceAgent.Value);
        else if (serviceId.HasValue)
            query = query.Where(d => d.ServiceId == serviceId.Value);

        if (!string.IsNullOrEmpty(statut)) query = query.Where(d => d.Statut.Code == statut);
        if (!string.IsNullOrEmpty(recherche))
            query = query.Where(d => d.Numero.Contains(recherche) || d.NomCitoyen.Contains(recherche));
        if (dateDebut.HasValue) query = query.Where(d => d.DateDepot >= dateDebut.Value);
        if (dateFin.HasValue) query = query.Where(d => d.DateDepot <= dateFin.Value);

        var dossiers = await query.OrderByDescending(d => d.DateDepot).ToListAsync();

        var csv = new StringBuilder();
        csv.AppendLine("Numero;Titre;Citoyen;Email;Téléphone;Statut;Date Dépôt;Dernière MAJ");
        foreach (var d in dossiers)
            csv.AppendLine($"{d.Numero};{d.Titre};{d.NomCitoyen};{d.EmailCitoyen};{d.TelephoneCitoyen};" +
                           $"{d.Statut.Code};{d.DateDepot:yyyy-MM-dd};{d.DateMiseAJourStatut:yyyy-MM-dd}");

        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv",
            $"dossiers_{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    // ─── GET /api/dossiers/{id} ───────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDossier(Guid id)
    {
        try
        {
            var dossier = await _db.Dossiers
                .Include(d => d.Statut)
                .Include(d => d.Service)
                .Include(d => d.Agent)
                .Include(d => d.HistoriqueStatuts).ThenInclude(h => h.AncienStatut)
                .Include(d => d.HistoriqueStatuts).ThenInclude(h => h.NouveauStatut)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (dossier == null) return NotFound(new { error = "Dossier introuvable." });

            var documents = await _db.VersionsDocument
                .Where(v => v.DossierId == id)
                .Select(v => new DocumentDto
                {
                    Id = v.Id,
                    NomFichier = v.NomFichier,
                    CheminFichier = v.CheminFichier,
                    TypeFichier = v.TypeFichier ?? "",
                    TailleFichier = v.TailleFichier ?? 0,
                    NumeroVersion = v.NumeroVersion,
                    DateCreation = v.DateCreation
                }).ToListAsync();

            var dto = new DossierDetailDto
            {
                Id = dossier.Id,
                Numero = dossier.Numero,
                Titre = dossier.Titre,
                Description = dossier.Description,
                NomCitoyen = dossier.NomCitoyen,
                EmailCitoyen = dossier.EmailCitoyen,
                TelephoneCitoyen = dossier.TelephoneCitoyen,
                MotifRejet = dossier.MotifRejet,
                StatutCode = dossier.Statut.Code,
                StatutLibelle = dossier.Statut.Libelle,
                ServiceNom = dossier.Service?.Nom ?? "",
                DateDepot = dossier.DateDepot,
                DateMiseAJourStatut = dossier.DateMiseAJourStatut,
                Historique = dossier.HistoriqueStatuts
                    .OrderByDescending(h => h.DateChangement)
                    .Select(h => new HistoriqueDto
                    {
                        AncienStatut = h.AncienStatut?.Libelle ?? "—",
                        NouveauStatut = h.NouveauStatut.Libelle,
                        Commentaire = h.Commentaire,
                        DateChangement = h.DateChangement
                    }).ToList(),
                Documents = documents
            };

            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération du dossier {Id}", id);
            return StatusCode(500, new { error = "Erreur serveur." });
        }
    }

    // ─── POST /api/dossiers ───────────────────────────────────────────────────
    [HttpPost]
    public async Task<IActionResult> CreerDossier([FromBody] CreerDossierRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var statutRecu = await _db.StatutsDossier.FirstOrDefaultAsync(s => s.Code == "RECU");
        if (statutRecu == null) return BadRequest(new { error = "Statut 'RECU' non défini." });

        var agentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? agentId = Guid.TryParse(agentIdStr, out var g) ? g : null;

        var numero = $"DOS-{DateTime.UtcNow.Year}-{(await _db.Dossiers.CountAsync() + 1):D5}";

        var dossier = new Dossier
        {
            Id = Guid.NewGuid(),
            Numero = numero,
            Titre = req.Titre,
            Description = req.Description,
            NomCitoyen = req.NomCitoyen,
            EmailCitoyen = req.EmailCitoyen,
            TelephoneCitoyen = req.TelephoneCitoyen,
            StatutId = statutRecu.Id,
            ServiceId = req.ServiceId,
            AgentId = agentId,
            DateDepot = DateTime.UtcNow,
            DateMiseAJourStatut = DateTime.UtcNow
        };

        _db.Dossiers.Add(dossier);
        await _db.SaveChangesAsync();

        return Ok(new { id = dossier.Id, numero = dossier.Numero });
    }

    // ─── PATCH /api/dossiers/{id}/statut ─────────────────────────────────────
    [HttpPatch("{id:guid}/statut")]
    public async Task<IActionResult> ChangerStatut(Guid id, [FromBody] ChangerStatutRequest req)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var dossier = await _db.Dossiers
            .Include(d => d.Statut)
            .Include(d => d.Service)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (dossier == null) return NotFound(new { error = "Dossier introuvable." });

        var nouveauStatut = await _db.StatutsDossier.FirstOrDefaultAsync(s => s.Code == req.NouveauStatutCode);
        if (nouveauStatut == null)
            return BadRequest(new { error = $"Statut '{req.NouveauStatutCode}' inexistant." });

        if (req.NouveauStatutCode == "REJETE" && string.IsNullOrWhiteSpace(req.Commentaire))
            return BadRequest(new { error = "Un motif de rejet est obligatoire." });

        var ancienStatutId = dossier.StatutId;

        if (req.NouveauStatutCode == "REJETE")
        {
            dossier.MotifRejet = req.Commentaire;

            // Envoi email au citoyen
            if (!string.IsNullOrEmpty(dossier.EmailCitoyen))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string sujet = $"Votre dossier {dossier.Numero} a été rejeté";
                        string corps = $@"<html><body>
                            <h2>Bonjour {dossier.NomCitoyen},</h2>
                            <p>Nous regrettons de vous informer que votre dossier <strong>{dossier.Numero}</strong> a été rejeté.</p>
                            <p><strong>Motif :</strong> {req.Commentaire}</p>
                            <p>Vous pouvez soumettre un nouveau dossier en corrigeant les informations.</p>
                            <br/><p>Cordialement,<br/>Le service {dossier.Service?.Nom}</p>
                        </body></html>";
                        await _emailService.SendEmailAsync(dossier.EmailCitoyen, sujet, corps);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur envoi email rejet pour {Numero}", dossier.Numero);
                    }
                });
            }
        }

        dossier.StatutId = nouveauStatut.Id;
        dossier.DateMiseAJourStatut = DateTime.UtcNow;

        var agentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? agentId = Guid.TryParse(agentIdStr, out var g) ? g : null;

        _db.HistoriqueStatuts.Add(new HistoriqueStatut
        {
            DossierId = dossier.Id,
            AncienStatutId = ancienStatutId,
            NouveauStatutId = nouveauStatut.Id,
            Commentaire = req.Commentaire,
            DateChangement = DateTime.UtcNow,
            AgentId = agentId
        });

        await _db.SaveChangesAsync();

        return Ok(new { message = "Statut mis à jour.", statut = nouveauStatut.Libelle });
    }

    // ─── POST /api/dossiers/{id}/documents ───────────────────────────────────
    [HttpPost("{id:guid}/documents")]
    public async Task<IActionResult> UploadDocument(Guid id, IFormFile fichier)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.Service)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (dossier == null) return NotFound(new { error = "Dossier introuvable." });
        if (fichier == null || fichier.Length == 0)
            return BadRequest(new { error = "Aucun fichier fourni." });

        var uniqueId = Guid.NewGuid().ToString("N");
        var nomFichier = $"{uniqueId}_{fichier.FileName}";

        var dossierFinalPath = _fileOrgService.GetCitizenFolderPath(
            dossier.NomCitoyen,
            dossier.EmailCitoyen ?? "",
            dossier.ServiceId,
            dossier.Service.Nom);

        var cheminFinal = Path.Combine(dossierFinalPath, nomFichier);

        await using (var stream = new FileStream(cheminFinal, FileMode.Create))
            await fichier.CopyToAsync(stream);

        var derniereVersion = await _db.VersionsDocument
            .Where(v => v.DossierId == id).MaxAsync(v => (int?)v.NumeroVersion) ?? 0;

        var agentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? agentId = Guid.TryParse(agentIdStr, out var g) ? g : null;

        await _db.VersionsDocument.Where(v => v.DossierId == id)
            .ExecuteUpdateAsync(s => s.SetProperty(v => v.EstActive, false));

        _db.VersionsDocument.Add(new VersionDocument
        {
            Id = Guid.NewGuid(),
            DossierId = id,
            NumeroVersion = derniereVersion + 1,
            NomFichier = fichier.FileName,
            CheminFichier = cheminFinal,
            TypeFichier = fichier.ContentType,
            TailleFichier = fichier.Length,
            DateCreation = DateTime.UtcNow,
            EstActive = true,
            UtilisateurId = agentId
        });

        await _db.SaveChangesAsync();
        return Ok(new { id = derniereVersion + 1, nomFichier = fichier.FileName });
    }

    // ─── POST /api/dossiers/public/depot ─────────────────────────────────────
    [HttpPost("public/depot")]
    [AllowAnonymous]
    public async Task<IActionResult> DepotPublic([FromForm] DepotPublicRequest dto, [FromForm] List<IFormFile> fichiers)
    {
        try
        {
            var service = await _db.Services.FindAsync(dto.ServiceId);
            if (service == null) return BadRequest(new { message = "Service invalide." });

            if (fichiers == null || fichiers.Count == 0)
                return BadRequest(new { message = "Au moins un fichier est requis." });

            if (fichiers.Count > 4)
                return BadRequest(new { message = "Maximum 4 fichiers autorisés." });

            var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };

            var dossier = new Dossier
            {
                Id = Guid.NewGuid(),
                Numero = await GenererNumero(),
                Titre = dto.Titre,
                Description = dto.Description,
                NomCitoyen = dto.NomCitoyen,
                EmailCitoyen = dto.EmailCitoyen,
                TelephoneCitoyen = dto.TelephoneCitoyen,
                StatutId = 1,
                ServiceId = dto.ServiceId,
                DateDepot = DateTime.UtcNow,
                DateMiseAJourStatut = DateTime.UtcNow
            };

            _db.Dossiers.Add(dossier);
            await _db.SaveChangesAsync();

            var folderPath = _fileOrgService.GetCitizenFolderPath(
                dto.NomCitoyen,
                dto.EmailCitoyen ?? "",
                service.Id,
                service.Nom);

            int version = 1;
            foreach (var fichier in fichiers)
            {
                var ext = Path.GetExtension(fichier.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(ext)) continue;
                if (fichier.Length > 10 * 1024 * 1024) continue;

                var uniqueId = Guid.NewGuid().ToString("N");
                var nomFichier = $"{uniqueId}_{fichier.FileName}";
                var filePath = Path.Combine(folderPath, nomFichier);

                using (var stream = new FileStream(filePath, FileMode.Create))
                    await fichier.CopyToAsync(stream);

                _db.VersionsDocument.Add(new VersionDocument
                {
                    Id = Guid.NewGuid(),
                    DossierId = dossier.Id,
                    NumeroVersion = version++,
                    NomFichier = fichier.FileName,
                    CheminFichier = filePath,
                    TypeFichier = fichier.ContentType ?? "application/octet-stream",
                    TailleFichier = fichier.Length,
                    DateCreation = DateTime.UtcNow,
                    EstActive = true
                });
            }

            await _db.SaveChangesAsync();

            // Email de confirmation au citoyen
            if (!string.IsNullOrEmpty(dossier.EmailCitoyen))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        string sujet = $"Confirmation de dépôt — Dossier {dossier.Numero}";
                        string corps = $@"<html><body>
                            <h2>Bonjour {dossier.NomCitoyen},</h2>
                            <p>Votre dossier a été déposé avec succès auprès du service <strong>{service.Nom}</strong>.</p>
                            <p><strong>Numéro de suivi :</strong> {dossier.Numero}</p>
                            <p>Conservez ce numéro pour suivre l'avancement de votre demande.</p>
                            <br/><p>Cordialement,<br/>L'Administration</p>
                        </body></html>";
                        await _emailService.SendEmailAsync(dossier.EmailCitoyen, sujet, corps);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Erreur email citoyen {Email}", dossier.EmailCitoyen);
                    }
                });
            }

            return Ok(new { numeroDossier = dossier.Numero });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur DepotPublic");
            return StatusCode(500, new { message = "Une erreur technique est survenue." });
        }
    }

    // ─── GET /api/dossiers/suivi/{numero} ────────────────────────────────────
    [HttpGet("suivi/{numero}")]
    [AllowAnonymous]
    public async Task<IActionResult> SuiviCitoyen(string numero)
    {
        var dossier = await _db.Dossiers
            .Include(d => d.Statut)
            .Include(d => d.Service)
            .Include(d => d.HistoriqueStatuts).ThenInclude(h => h.AncienStatut)
            .Include(d => d.HistoriqueStatuts).ThenInclude(h => h.NouveauStatut)
            .FirstOrDefaultAsync(d => d.Numero == numero);

        if (dossier == null)
            return NotFound(new { message = $"Aucun dossier trouvé avec le numéro {numero}." });

        return Ok(new
        {
            id = dossier.Id,
            numero = dossier.Numero,
            titre = dossier.Titre,
            description = dossier.Description,
            nomCitoyen = dossier.NomCitoyen,
            service = dossier.Service?.Nom ?? "",
            statutCode = dossier.Statut.Code,
            statutLibelle = dossier.Statut.Libelle,
            dateDepot = dossier.DateDepot,
            dateMiseAJourStatut = dossier.DateMiseAJourStatut,
            motifRejet = dossier.MotifRejet,
            historique = dossier.HistoriqueStatuts
                .OrderByDescending(h => h.DateChangement)
                .Select(h => new
                {
                    ancienStatut = h.AncienStatut?.Libelle ?? "Création",
                    nouveauStatut = h.NouveauStatut.Libelle,
                    commentaire = h.Commentaire,
                    date = h.DateChangement
                })
        });
    }

    // ─── PATCH /api/dossiers/{id}/transferer ─────────────────────────────────
    [HttpPatch("{id:guid}/transferer")]
    public async Task<IActionResult> Transferer(Guid id, [FromBody] TransfererDto dto)
    {
        var dossier = await _db.Dossiers.Include(d => d.Statut).FirstOrDefaultAsync(d => d.Id == id);
        if (dossier == null) return NotFound(new { message = "Dossier introuvable." });

        var serviceExiste = await _db.Services.AnyAsync(s => s.Id == dto.ServiceId && s.EstActif);
        if (!serviceExiste) return BadRequest(new { message = "Service cible introuvable ou inactif." });

        var ancienStatutId = dossier.StatutId;
        dossier.ServiceId = dto.ServiceId;

        var statutTransf = await _db.StatutsDossier.FirstOrDefaultAsync(s => s.Code == "TRANSFERE");
        if (statutTransf != null) dossier.StatutId = statutTransf.Id;

        dossier.DateMiseAJourStatut = DateTime.UtcNow;

        var agentIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Guid? agentId = Guid.TryParse(agentIdStr, out var g) ? g : null;

        _db.HistoriqueStatuts.Add(new HistoriqueStatut
        {
            DossierId = dossier.Id,
            AncienStatutId = ancienStatutId,
            NouveauStatutId = dossier.StatutId,
            Commentaire = $"Transféré vers le service {dto.ServiceId}" +
                          (string.IsNullOrWhiteSpace(dto.Commentaire) ? "" : $" : {dto.Commentaire}"),
            DateChangement = DateTime.UtcNow,
            AgentId = agentId
        });

        await _db.SaveChangesAsync();

        return Ok(new { message = "Dossier transféré avec succès." });
    }

    // ─── GET /api/dossiers/archives ──────────────────────────────────────────
    [HttpGet("archives")]
    public async Task<IActionResult> GetArchives(
        [FromQuery] string? numero,
        [FromQuery] DateTime? dateDebut,
        [FromQuery] DateTime? dateFin,
        [FromQuery] int page = 1,
        [FromQuery] int size = 12)
    {
        var query = _db.Dossiers
            .Include(d => d.Statut)
            .Include(d => d.Service)
            .Where(d => d.Statut.Code == "ARCHIVE")
            .AsQueryable();

        if (!string.IsNullOrEmpty(numero))
            query = query.Where(d => d.Numero.Contains(numero) || d.NomCitoyen.Contains(numero));
        if (dateDebut.HasValue)
            query = query.Where(d => d.DateArchivage >= dateDebut.Value);
        if (dateFin.HasValue)
            query = query.Where(d => d.DateArchivage <= dateFin.Value);

        var total = await query.CountAsync();

        var dossiers = await query
            .OrderBy(d => d.Service.Nom)
            .ThenBy(d => d.EmailCitoyen)
            .ThenByDescending(d => d.DateArchivage)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(d => new
            {
                id = d.Id,
                numero = d.Numero,
                titre = d.Titre,
                citoyen = d.NomCitoyen,
                emailCitoyen = d.EmailCitoyen,
                service = d.Service != null ? d.Service.Nom : "",
                dateArchivage = d.DateArchivage ?? d.DateMiseAJourStatut,
                nbDocuments = _db.VersionsDocument.Count(v => v.DossierId == d.Id),
                miniature = ""
            })
            .ToListAsync();

        return Ok(new { total, page, size, data = dossiers });
    }

    private async Task<string> GenererNumero()
    {
        var annee = DateTime.Now.Year;
        var count = await _db.Dossiers.CountAsync() + 1;
        return $"DOS-{annee}-{count:D5}";
    }
}
