// Models/VersionDocument.cs
// CORRECTIF : La propriété Commentaire était déjà déclarée dans le modèle
// mais la colonne n'existait pas en base de données.
// SOLUTION : exécuter dans le terminal backend :
//   dotnet ef migrations add AjoutCommentaireVersionDocument
//   dotnet ef database update

public class VersionDocument
{
    public Guid Id { get; set; }
    public Guid DossierId { get; set; }
    public int NumeroVersion { get; set; } = 1;
    public string NomFichier { get; set; } = "";
    public string CheminFichier { get; set; } = "";
    public string? TypeFichier { get; set; }
    public long? TailleFichier { get; set; }
    public string? EmpreinteHash { get; set; }
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
    public bool EstActive { get; set; } = true;
    public Guid? UtilisateurId { get; set; }

    // CORRECTIF : colonne Commentaire — doit être ajoutée via migration EF Core
    // Si vous ne voulez PAS faire de migration, supprimez cette propriété
    // et adaptez VersionsController.cs pour ne pas la sélectionner.
    public string? Commentaire { get; set; }

    // Navigation
    public Dossier Dossier { get; set; } = null!;
    public Utilisateur? Utilisateur { get; set; }
}