using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

public record TransfererDto(int ServiceId, string? Commentaire);
public class DossierListeDto
{
    public Guid Id { get; set; }
    public string Numero { get; set; } = "";
    public string Titre { get; set; } = "";
    public string NomCitoyen { get; set; } = "";
    public string? EmailCitoyen { get; set; }
    public string? TelephoneCitoyen { get; set; }
    public string StatutCode { get; set; } = "";
    public string StatutLibelle { get; set; } = "";
    public DateTime DateDepot { get; set; }
    public DateTime DateMiseAJourStatut { get; set; }

    //public string? CheminFichier { get; set; }
    //public string CheminFichier { get; set; } = ""; 
}

public class DossierDetailDto : DossierListeDto
{
    public string? Description { get; set; }
    public string? MotifRejet { get; set; }
    public string ServiceNom { get; set; } = "";
    public List<HistoriqueDto> Historique { get; set; } = [];
    public List<DocumentDto> Documents { get; set; } = [];
}

public class HistoriqueDto
{
    public string AncienStatut { get; set; } = "";
    public string NouveauStatut { get; set; } = "";
    public string? Commentaire { get; set; }
    public DateTime DateChangement { get; set; }
}

public class DocumentDto
{
    public Guid Id { get; set; }
    public string NomFichier { get; set; } = "";
    public string CheminFichier { get; set; } = ""; 
    public string TypeFichier { get; set; } = "";
    public long TailleFichier { get; set; }
    public int NumeroVersion { get; set; }
    public DateTime DateCreation { get; set; }
}

public class CreerDossierRequest
{
    [Required, MaxLength(200)] public string Titre { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    [Required, MaxLength(160)] public string NomCitoyen { get; set; } = "";
    [EmailAddress, MaxLength(150)] public string? EmailCitoyen { get; set; }
    [MaxLength(20)] public string? TelephoneCitoyen { get; set; }
    [Required] public int ServiceId { get; set; }
}

public class ChangerStatutRequest
{
    [Required] public string NouveauStatutCode { get; set; } = "";
    [MaxLength(1000)] public string? Commentaire { get; set; }
}

public class StatsDossiersDto
{
    public int Total { get; set; }
    public int Recu { get; set; }
    public int EnCours { get; set; }
    public int Transfere { get; set; }
    public int Rejete { get; set; }
    public int Termine { get; set; }
    public int Archive { get; set; }
}

public class PageDossiersDto
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int Taille { get; set; }
    public List<DossierListeDto> Dossiers { get; set; } = [];
}

/*public class DepotPublicRequest
{
    [Required, MaxLength(160)] public string NomCitoyen { get; set; } = "";
    [EmailAddress, MaxLength(150)] public string? EmailCitoyen { get; set; }
    [MaxLength(20)] public string? TelephoneCitoyen { get; set; }
    [Required, MaxLength(200)] public string Titre { get; set; } = "";
    [MaxLength(2000)] public string? Description { get; set; }
    public IFormFile? Fichier { get; set; }
}*/

public class UploadDocumentRequest
{
    [Required] public IFormFile? Fichier { get; set; }
}



public class DepotPublicRequest
{
    [Required, MaxLength(160)] 
    public string NomCitoyen { get; set; } = "";

    [EmailAddress, MaxLength(150)] 
    public string? EmailCitoyen { get; set; }

    [MaxLength(20)] 
    public string? TelephoneCitoyen { get; set; }

    [Required, MaxLength(200)] 
    public string Titre { get; set; } = "";

    [MaxLength(2000)] 
    public string? Description { get; set; }

    [Required]
    public int ServiceId { get; set; }
}

// ... (Gardez le reste du fichier)


