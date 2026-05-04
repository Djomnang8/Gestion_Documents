// Controller/JournauxController.cs
// Utilise j.DateAction (colonne réelle en base)
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;

[ApiController]
[Route("api/journaux")]
[Authorize]
public class JournauxController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public JournauxController(ApplicationDbContext db) => _db = db;

    // ── GET /api/journaux ─────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid?     utilisateurId = null,
        [FromQuery] string?   module        = null,
        [FromQuery] string?   action        = null,
        [FromQuery] int?      niveauId      = null,
        [FromQuery] DateTime? dateDebut     = null,
        [FromQuery] DateTime? dateFin       = null,
        [FromQuery] int       page          = 1,
        [FromQuery] int       pageSize      = 30)
    {
        var query = _db.Journaux.Include(j => j.Utilisateur).AsQueryable();

        if (utilisateurId.HasValue) query = query.Where(j => j.UtilisateurId == utilisateurId);
        if (!string.IsNullOrEmpty(module)) query = query.Where(j => j.Module.Contains(module));
        if (!string.IsNullOrEmpty(action)) query = query.Where(j => j.Action.Contains(action));
        if (niveauId.HasValue)  query = query.Where(j => j.NiveauId == niveauId);
        if (dateDebut.HasValue) query = query.Where(j => j.DateAction >= dateDebut);   // ← DateAction
        if (dateFin.HasValue)   query = query.Where(j => j.DateAction <= dateFin.Value.AddDays(1));

        var total    = await query.CountAsync();
        var journaux = await query
            .OrderByDescending(j => j.DateAction)                                       // ← DateAction
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(j => new JournalDto
            {
                Id          = j.Id,
                Utilisateur = j.Utilisateur != null
                                ? $"{j.Utilisateur.Prenom} {j.Utilisateur.Nom}"
                                : "Système",
                Module      = j.Module,
                Action      = j.Action,
                Details     = j.Details ?? "",
                NiveauId    = j.NiveauId,
                DateAction  = j.DateAction,                                             // ← DateAction
                AdresseIp   = j.AdresseIp ?? ""
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data = journaux });
    }

    // ── GET /api/journaux/mes-activites ───────────────────────
    [HttpGet("mes-activites")]
    public async Task<IActionResult> MesActivites(
        [FromQuery] Guid?     utilisateurId = null,
        [FromQuery] DateTime? dateDebut     = null,
        [FromQuery] DateTime? dateFin       = null,
        [FromQuery] int       page          = 1,
        [FromQuery] int       pageSize      = 20)
    {
        if (!utilisateurId.HasValue)
        {
            var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (Guid.TryParse(s, out var gId)) utilisateurId = gId;
        }

        var query = _db.Journaux
            .Where(j => j.UtilisateurId == utilisateurId)
            .AsQueryable();

        if (dateDebut.HasValue) query = query.Where(j => j.DateAction >= dateDebut);
        if (dateFin.HasValue)   query = query.Where(j => j.DateAction <= dateFin.Value.AddDays(1));

        var total = await query.CountAsync();
        var data  = await query
            .OrderByDescending(j => j.DateAction)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(j => new {
                id         = j.Id,
                module     = j.Module,
                action     = j.Action,
                details    = j.Details ?? "",
                niveauId   = j.NiveauId,
                dateAction = j.DateAction,                                              // ← DateAction
                adresseIp  = j.AdresseIp ?? ""
            })
            .ToListAsync();

        return Ok(new { total, page, pageSize, data });
    }

    // ── GET /api/journaux/modules ─────────────────────────────
    [HttpGet("modules")]
    public async Task<IActionResult> GetModules()
    {
        var modules = await _db.Journaux
            .Select(j => j.Module).Distinct().OrderBy(m => m)
            .ToListAsync();
        return Ok(modules);
    }

    // ── GET /api/journaux/export ──────────────────────────────
    [HttpGet("export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid?     utilisateurId = null,
        [FromQuery] string?   module        = null,
        [FromQuery] int?      niveauId      = null,
        [FromQuery] DateTime? dateDebut     = null,
        [FromQuery] DateTime? dateFin       = null)
    {
        var query = _db.Journaux.Include(j => j.Utilisateur).AsQueryable();
        if (utilisateurId.HasValue) query = query.Where(j => j.UtilisateurId == utilisateurId);
        if (!string.IsNullOrEmpty(module)) query = query.Where(j => j.Module.Contains(module));
        if (niveauId.HasValue)  query = query.Where(j => j.NiveauId == niveauId);
        if (dateDebut.HasValue) query = query.Where(j => j.DateAction >= dateDebut);
        if (dateFin.HasValue)   query = query.Where(j => j.DateAction <= dateFin.Value.AddDays(1));

        var data = await query
            .OrderByDescending(j => j.DateAction)
            .Select(j => new {
                Date        = j.DateAction.ToString("dd/MM/yyyy HH:mm"),                // ← DateAction
                Utilisateur = j.Utilisateur != null
                                ? $"{j.Utilisateur.Prenom} {j.Utilisateur.Nom}"
                                : "Système",
                j.Module, j.Action,
                Details  = j.Details ?? "",
                Niveau   = j.NiveauId,
                IP       = j.AdresseIp ?? ""
            }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("Date,Utilisateur,Module,Action,Détails,Niveau,IP");
        foreach (var r in data)
            sb.AppendLine($"\"{r.Date}\",\"{r.Utilisateur}\",\"{r.Module}\",\"{r.Action}\",\"{r.Details}\",{r.Niveau},\"{r.IP}\"");

        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv",
            $"journaux_{DateTime.Now:yyyyMMdd}.csv");
    }
}

// ── DTO ──────────────────────────────────────────────────────
public class JournalDto
{
    public int      Id          { get; set; }
    public string   Utilisateur { get; set; } = "";
    public string   Module      { get; set; } = "";
    public string   Action      { get; set; } = "";
    public string   Details     { get; set; } = "";
    public int      NiveauId    { get; set; }
    public DateTime DateAction  { get; set; }   // ← DateAction (sérialisé en camelCase)
    public string   AdresseIp   { get; set; } = "";
}