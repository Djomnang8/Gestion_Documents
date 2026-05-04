// Models/Service.cs
public class Service
{
    public int Id { get; set; }
    public string Nom { get; set; } = "";
    public string? Description { get; set; }
    public bool EstActif { get; set; } = true;
    public ICollection<Dossier> Dossiers { get; set; } = [];
}