//C:\TP_PROJET_B1_a_B3\projet_Angular\gestion_documents\backend\GestionDocuments.API\Controller\ServicesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GestionDocuments.API.Models; 

[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ServicesController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET /api/services
    // AllowAnonymous car le citoyen doit pouvoir choisir un service sans être connecté
    [HttpGet]
    [AllowAnonymous] 
    public async Task<IActionResult> GetServices()
    {
        var services = await _db.Services
            .Where(s => s.EstActif)
            .OrderBy(s => s.Nom)
            .Select(s => new { 
                id = s.Id, 
                nom = s.Nom 
            })
            .ToListAsync();

        return Ok(services);
    }
}