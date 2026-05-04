// Models/UtilisateurRole.cs
public class UtilisateurRole
{
    public Guid UtilisateurId { get; set; }
    public int RoleId { get; set; }
    public Utilisateur Utilisateur { get; set; } = null!;
    public Role Role { get; set; } = null!;
}