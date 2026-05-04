// backend/GestionDocuments.API/Controller/NotificationsController.cs
// Correction 500 : le modèle Notification avait des colonnes manquantes.
// Ce contrôleur est maintenant aligné sur le modèle corrigé.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    public NotificationsController(ApplicationDbContext db) => _db = db;

    // GET /api/notifications?onglet=toutes|non-lues|rappels
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string onglet = "toutes")
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var query = _db.Notifications
            .Where(n => n.UtilisateurId == userId && !n.EstSupprimee)
            .AsQueryable();

        if (onglet == "non-lues") query = query.Where(n => !n.EstLue);
        if (onglet == "rappels")  query = query.Where(n => n.Type == "RAPPEL");

        var total   = await _db.Notifications.CountAsync(n => n.UtilisateurId == userId && !n.EstSupprimee);
        var nonLues = await _db.Notifications.CountAsync(n => n.UtilisateurId == userId && !n.EstLue && !n.EstSupprimee);
        var rappels = await _db.Notifications.CountAsync(n => n.UtilisateurId == userId && n.Type == "RAPPEL" && !n.EstSupprimee);

        var data = await query
            .OrderByDescending(n => n.DateCreation)
            .Select(n => new NotifDto
            {
                Id            = n.Id,
                Titre         = n.Titre,
                Description   = n.Message,
                Type          = n.Type,
                DossierId     = n.DossierId.HasValue ? n.DossierId.Value.ToString() : null,
                NumeroDossier = n.NumeroDossier,
                EstLue        = n.EstLue,
                DateCreation  = n.DateCreation
            })
            .ToListAsync();

        return Ok(new { total, nonLues, rappels, data });
    }

    // PUT /api/notifications/{id}/lue
    [HttpPut("{id:int}/lue")]
    public async Task<IActionResult> MarquerLue(int id)
    {
        var userId = GetUserId();
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UtilisateurId == userId);
        if (n == null) return NotFound();
        n.EstLue = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // PUT /api/notifications/tout-lire
    [HttpPut("tout-lire")]
    public async Task<IActionResult> ToutLire()
    {
        var userId = GetUserId();
        await _db.Notifications
            .Where(n => n.UtilisateurId == userId && !n.EstLue)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.EstLue, true));
        return Ok();
    }

    // DELETE /api/notifications/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Supprimer(int id)
    {
        var userId = GetUserId();
        var n = await _db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UtilisateurId == userId);
        if (n == null) return NotFound();
        n.EstSupprimee = true;
        await _db.SaveChangesAsync();
        return Ok();
    }

    // GET /api/notifications/emails
    [HttpGet("emails")]
    public async Task<IActionResult> GetEmails()
    {
        var userId = GetUserId();
        var emails = await _db.Rappels
            .Where(r => r.UtilisateurId == userId)
            .OrderByDescending(r => r.DateEnvoi)
            .Select(r => new EmailDto
            {
                Id           = r.Id,
                Destinataire = "",           // rempli côté frontend depuis le profil
                Objet        = r.Objet ?? r.Titre,
                Type         = r.Type ?? "RAPPEL",
                Statut       = r.Statut ?? "ENVOYE",
                DateEnvoi    = r.DateEnvoi,
                Tentatives   = r.Tentatives,
                Erreur       = r.Erreur
            })
            .ToListAsync();
        return Ok(emails);
    }

    // POST /api/notifications/emails/{id}/retry
    [HttpPost("emails/{id:int}/retry")]
    public async Task<IActionResult> Retry(int id)
    {
        var r = await _db.Rappels.FindAsync(id);
        if (r == null) return NotFound();
        r.Statut     = "EN_ATTENTE";
        r.Tentatives = (r.Tentatives ?? 0) + 1;
        await _db.SaveChangesAsync();
        return Ok(new { message = "Email remis en file d'envoi." });
    }

    private Guid? GetUserId()
    {
        var s = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(s, out var g) ? g : null;
    }
}

public class NotifDto
{
    public int      Id            { get; set; }
    public string   Titre         { get; set; } = "";
    public string   Description   { get; set; } = "";
    public string   Type          { get; set; } = "INFO";
    public string?  DossierId     { get; set; }
    public string?  NumeroDossier { get; set; }
    public bool     EstLue        { get; set; }
    public DateTime DateCreation  { get; set; }
}

public class EmailDto
{
    public int      Id           { get; set; }
    public string   Destinataire { get; set; } = "";
    public string   Objet        { get; set; } = "";
    public string   Type         { get; set; } = "";
    public string   Statut       { get; set; } = "ENVOYE";
    public DateTime DateEnvoi    { get; set; }
    public int?     Tentatives   { get; set; }
    public string?  Erreur       { get; set; }
}