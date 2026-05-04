// Models/Permission.cs
public class Permission
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public ICollection<RolePermission> RolesPermissions { get; set; } = [];
}
