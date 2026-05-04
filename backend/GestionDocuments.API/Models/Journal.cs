// Models/Journal.cs
// CORRECTION CRITIQUE : la migration crée la colonne "DateHeure" (pas "DateAction")
// Le modèle doit utiliser DateHeure pour correspondre au schéma réel.
// EntiteId est nullable (NOT IsRequired) car absent de certains appels.
namespace GestionDocuments.API.Models
{
    public class Journal
    {
        public int      Id            { get; set; }
        public Guid?    UtilisateurId { get; set; }
        public string   Module        { get; set; } = "";
        public string   Action        { get; set; } = "";
        public string?  Details       { get; set; }
        public int      NiveauId      { get; set; } = 1;
        public DateTime DateAction     { get; set; } = DateTime.UtcNow;  // ← NOM EXACT DE LA COLONNE EN BASE
        public string?  EntiteId      { get; set; }
        public string?  AdresseIp     { get; set; }

        public Utilisateur? Utilisateur { get; set; }
    }
}