// Controller/FichiersController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;

namespace GestionDocuments.API.Controller;

[ApiController]
[Route("api/fichiers")]
[Authorize]
public class FichiersController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FichiersController> _logger;

    public FichiersController(IWebHostEnvironment env, ILogger<FichiersController> logger)
    {
        _env = env;
        _logger = logger;
    }

    // GET /api/fichiers/download?chemin=...
    [HttpGet("download")]
    public IActionResult Download([FromQuery] string chemin)
    {
        if (string.IsNullOrWhiteSpace(chemin))
            return BadRequest("Chemin manquant.");

        // Normaliser le chemin reçu (supporte chemins absolus Windows et Unix)
        string cheminNormalise;
        try
        {
            cheminNormalise = Path.GetFullPath(chemin);
        }
        catch
        {
            return BadRequest("Chemin invalide.");
        }

        // Racine autorisée : dossier uploads/ à la racine du projet
        var racine = Path.GetFullPath(Path.Combine(_env.ContentRootPath, "uploads"));

        // Sécurité : interdire les traversées de répertoire (path traversal)
        // Comparaison insensible à la casse et normalisée
        if (!cheminNormalise.StartsWith(racine, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Tentative d'accès non autorisé au chemin : {Chemin} (racine attendue: {Racine})",
                cheminNormalise, racine);
            return Forbid();
        }

        if (!System.IO.File.Exists(cheminNormalise))
        {
            _logger.LogWarning("Fichier introuvable : {Chemin}", cheminNormalise);
            return NotFound("Fichier introuvable.");
        }

        // Déterminer le Content-Type
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(cheminNormalise, out var contentType))
            contentType = "application/octet-stream";

        var nomFichier = Path.GetFileName(cheminNormalise);

        // Pour les PDF et images : afficher dans le navigateur (inline)
        // Pour les autres : forcer le téléchargement (attachment)
        var extensionsInline = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(cheminNormalise).ToLowerInvariant();
        var disposition = extensionsInline.Contains(ext) ? "inline" : "attachment";

        Response.Headers.Append("Content-Disposition",
            $"{disposition}; filename=\"{Uri.EscapeDataString(nomFichier)}\"");

        // Autoriser CORS pour les requêtes cross-origin (Angular en dev)
        Response.Headers.Append("Access-Control-Allow-Origin", "*");

        var stream = System.IO.File.OpenRead(cheminNormalise);
        return File(stream, contentType, enableRangeProcessing: true);
    }

    // GET /api/fichiers/preview/{dossierId}
    [HttpGet("preview/{dossierId:guid}")]
    public async Task<IActionResult> Preview(Guid dossierId, [FromServices] ApplicationDbContext context)
    {
        var version = await context.VersionsDocument
            .Where(v => v.DossierId == dossierId && v.EstActive)
            .OrderByDescending(v => v.NumeroVersion)
            .FirstOrDefaultAsync();

        if (version == null)
            return NotFound("Aucun document pour ce dossier.");

        return Download(version.CheminFichier);
    }
}