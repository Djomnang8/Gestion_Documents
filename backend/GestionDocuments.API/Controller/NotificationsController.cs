// Module Notifications supprimé — endpoints retournent 404
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    [HttpGet] public IActionResult Get() => NotFound(new { message = "Module notifications supprimé." });
    [HttpPut("{id}/lue")] public IActionResult MarquerLue(int id) => NotFound();
    [HttpPut("tout-lire")] public IActionResult ToutLire() => NotFound();
    [HttpDelete("{id}")] public IActionResult Supprimer(int id) => NotFound();
    [HttpGet("emails")] public IActionResult GetEmails() => NotFound();
    [HttpPost("emails/{id}/retry")] public IActionResult Retry(int id) => NotFound();
}