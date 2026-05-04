// Models/Notification.cs
// Correction 500 : ajout des colonnes Titre, Type, DossierId, NumeroDossier, EstSupprimee
// qui étaient absentes du modèle mais utilisées par NotificationsController
public class Notification
{
    public int              Id            { get; set; }
    public Guid?            UtilisateurId { get; set; }
    public string           Titre         { get; set; } = "";
    public string           Message       { get; set; } = "";
    public string           Type          { get; set; } = "INFO";   // INFO|STATUT|REJETE|RAPPEL|TERMINE
    public Guid?            DossierId     { get; set; }
    public string?          NumeroDossier { get; set; }
    public bool             EstLue        { get; set; } = false;
    public bool             EstSupprimee  { get; set; } = false;
    public DateTime         DateCreation  { get; set; } = DateTime.UtcNow;

    public Utilisateur?     Utilisateur   { get; set; }
}