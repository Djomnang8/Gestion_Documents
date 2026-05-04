using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

public class Dossier
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string Titre { get; set; } = "";
    public string? Description { get; set; }
    public string NomCitoyen { get; set; } = "";
    public string? EmailCitoyen { get; set; }
    public string? TelephoneCitoyen { get; set; }
    public string? MotifRejet { get; set; }
    public DateTime DateDepot { get; set; } = DateTime.UtcNow;
    public DateTime DateMiseAJourStatut { get; set; } = DateTime.UtcNow;
    public DateTime? DateArchivage { get; set; }
    public Guid? AgentId { get; set; }
   
    
    public Utilisateur? Agent { get; set; }
    public ICollection<HistoriqueStatut> HistoriqueStatuts { get; set; } = [];
    public ICollection<VersionDocument> VersionsDocument { get; set; } = [];

   

    public int ServiceId { get; set; }
    [ForeignKey(nameof(ServiceId))]
    public Service Service { get; set; }

    public int StatutId { get; set; }
    [ForeignKey(nameof(StatutId))]
    public StatutDossier Statut { get; set; }
}

public class StatutDossier
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Libelle { get; set; } = "";
    public ICollection<Dossier> Dossiers { get; set; } = [];
}

public class HistoriqueStatut
{
    public long Id { get; set; }
    public Guid DossierId { get; set; }
    public int? AncienStatutId { get; set; }
    public int NouveauStatutId { get; set; }
    public string? Commentaire { get; set; }
    public DateTime DateChangement { get; set; } = DateTime.UtcNow;
    public Guid? AgentId { get; set; }
    public Dossier Dossier { get; set; } = null!;
    public StatutDossier? AncienStatut { get; set; }
    public StatutDossier NouveauStatut { get; set; } = null!;
}