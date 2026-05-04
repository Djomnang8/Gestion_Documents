// Models/Role.cs
public class Role
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Description { get; set; }
    public ICollection<UtilisateurRole> UtilisateursRoles { get; set; } = [];
    public ICollection<RolePermission> RolesPermissions { get; set; } = [];
}