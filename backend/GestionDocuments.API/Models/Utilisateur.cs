// Models/Utilisateur.cs
public class Utilisateur
{
    public Guid Id { get; set; }
    public string Nom { get; set; } = "";
    public string Prenom { get; set; } = "";
    public string Email { get; set; } = "";
    public string? Telephone { get; set; }
    public string MotDePasseHash { get; set; } = "";
    public bool EstActif { get; set; }
    public bool EstListeNoire { get; set; }
    public string? MotifListeNoire { get; set; }
    public DateTime? DerniereConnexion { get; set; }
    public string? JetonRafraichissement { get; set; }
    public DateTime? ExpirationJeton { get; set; }
    public int? ServiceId { get; set; }             
    public string TypeUtilisateur { get; set; } = "";
    public bool EstSupprime { get; set; } = false;

    // Navigation
    public ICollection<UtilisateurRole> UtilisateursRoles { get; set; } = [];
}




