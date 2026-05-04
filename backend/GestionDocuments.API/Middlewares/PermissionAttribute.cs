// backend/GestionDocuments.API/Middlewares/PermissionAttribute.cs

using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class PermissionRequiredAttribute : Attribute {
    public string Permission { get; }
    public PermissionRequiredAttribute(string permission) => Permission = permission;
}
 
// Middlewares/PermissionMiddleware.cs
public class PermissionMiddleware {
    private readonly RequestDelegate _next;
    public PermissionMiddleware(RequestDelegate next) => _next = next;
 
    public async Task InvokeAsync(HttpContext ctx, ApplicationDbContext db) {
        var endpoint = ctx.GetEndpoint();
        var attr = endpoint?.Metadata.GetMetadata<PermissionRequiredAttribute>();
        if (attr != null) {
            var userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasPermission = await db.UtilisateursRoles
                .Where(ur => ur.UtilisateurId.ToString() == userId)
                .SelectMany(ur => ur.Role.RolesPermissions)
                .AnyAsync(rp => rp.Permission.Nom == attr.Permission);
            if (!hasPermission) {
                ctx.Response.StatusCode = 403;
                await ctx.Response.WriteAsJsonAsync(new { error = "Accès refusé" });
                return;
            }
        }
        await _next(ctx);
    }
}