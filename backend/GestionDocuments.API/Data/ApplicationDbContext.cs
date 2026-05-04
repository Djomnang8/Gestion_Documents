// Data/ApplicationDbContext.cs
// CORRECTIONS :
//   - Journal.DateHeure aligné avec la migration réelle (pas DateAction)
//   - Notification étendue (Titre, Type, DossierId, NumeroDossier, EstSupprimee)
//   - Rappel étendu (UtilisateurId, Objet, Type, Statut, Tentatives, Erreur, DateEnvoi)
using Microsoft.EntityFrameworkCore;
using GestionDocuments.API.Models;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    public DbSet<Utilisateur>     Utilisateurs     { get; set; }
    public DbSet<Role>            Roles            { get; set; }
    public DbSet<Permission>      Permissions      { get; set; }
    public DbSet<UtilisateurRole> UtilisateursRoles { get; set; }
    public DbSet<RolePermission>  RolesPermissions  { get; set; }
    public DbSet<Service>         Services          { get; set; }
    public DbSet<Dossier>         Dossiers          { get; set; }
    public DbSet<StatutDossier>   StatutsDossier    { get; set; }
    public DbSet<HistoriqueStatut> HistoriqueStatuts  { get; set; }
    public DbSet<VersionDocument> VersionsDocument  { get; set; }
    public DbSet<Journal>         Journaux          { get; set; }
    public DbSet<Notification>    Notifications     { get; set; }
    public DbSet<Rappel>          Rappels           { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ── Clés composites
        modelBuilder.Entity<UtilisateurRole>()
            .HasKey(ur => new { ur.UtilisateurId, ur.RoleId });

        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => new { rp.RoleId, rp.PermissionId });

        // ── Dossier
        modelBuilder.Entity<Dossier>()
            .HasOne(d => d.Statut)
            
            .WithMany(s => s.Dossiers) 
            .HasForeignKey(d => d.StatutId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Dossier>()
            .HasOne(d => d.Service)
            .WithMany(s => s.Dossiers) 
            .HasForeignKey(d => d.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Historique
        modelBuilder.Entity<HistoriqueStatut>()
            .HasOne(h => h.Dossier)
            .WithMany(d => d.HistoriqueStatuts)
            .HasForeignKey(h => h.DossierId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Version
        modelBuilder.Entity<VersionDocument>()
            .HasOne(v => v.Dossier)
            .WithMany(d => d.VersionsDocument)
            .HasForeignKey(v => v.DossierId)
            .OnDelete(DeleteBehavior.Cascade);

        // ── Journal : colonne DateHeure (NOM EXACT dans la migration)
        modelBuilder.Entity<Journal>(b =>
        {
            b.ToTable("Journaux");
            b.Property(j => j.DateAction)
             .HasColumnName("DateAction")      // ← aligné avec la migration
             .HasColumnType("datetime2")
             .HasDefaultValueSql("GETUTCDATE()");
            b.Property(j => j.EntiteId).IsRequired(false);
            b.Property(j => j.AdresseIp).IsRequired(false);
            b.Property(j => j.Details).IsRequired(false);
        });

        // ── Notification étendue
        modelBuilder.Entity<Notification>(b =>
        {
            b.ToTable("Notifications");
            b.Property(n => n.Titre).HasDefaultValue("").IsRequired();
            b.Property(n => n.Type).HasDefaultValue("INFO").IsRequired();
            b.Property(n => n.DossierId).IsRequired(false);
            b.Property(n => n.NumeroDossier).IsRequired(false);
            b.Property(n => n.EstSupprimee).HasDefaultValue(false);
        });

        // ── Rappel étendu
        modelBuilder.Entity<Rappel>(b =>
        {
            b.ToTable("Rappels");
            b.Property(r => r.UtilisateurId).IsRequired(false);
            b.Property(r => r.Objet).IsRequired(false);
            b.Property(r => r.Type).HasDefaultValue("RAPPEL").IsRequired(false);
            b.Property(r => r.Statut).HasDefaultValue("ENVOYE").IsRequired(false);
            b.Property(r => r.Tentatives).HasDefaultValue(0);
            b.Property(r => r.Erreur).IsRequired(false);
            b.Property(r => r.DateEnvoi)
             .HasColumnType("datetime2")
             .HasDefaultValueSql("GETUTCDATE()");
        });

        // ── Utilisateur : email unique (soft delete)
        modelBuilder.Entity<Utilisateur>()
            .HasIndex(u => u.Email)
            .IsUnique()
            .HasFilter("[EstSupprime] = 0");
    }
}