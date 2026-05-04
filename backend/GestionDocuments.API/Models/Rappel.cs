// Models/Rappel.cs
public class Rappel
{
    public int              Id            { get; set; }
    public Guid?            DossierId     { get; set; }
    public Guid?            UtilisateurId { get; set; }
    public string           Titre         { get; set; } = "";
    public string?          Objet         { get; set; }       // Objet email
    public string?          Type          { get; set; } = "RAPPEL"; // RAPPEL|DEPOT|STATUT|TERMINE|REJETE
    public string?          Description   { get; set; }
    public string?          Statut        { get; set; } = "ENVOYE"; // ENVOYE|EN_ATTENTE|ECHEC
    public int?             Tentatives    { get; set; } = 0;
    public string?          Erreur        { get; set; }
    public bool             EstEffectue   { get; set; } = false;
    public DateTime         DateRappel    { get; set; } = DateTime.UtcNow;
    public DateTime         DateEnvoi     { get; set; } = DateTime.UtcNow;

    public Dossier?         Dossier       { get; set; }
    public Utilisateur?     Utilisateur   { get; set; }
}