using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionDocuments.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Services",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstActif = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Services", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "StatutsDossier",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Code = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Libelle = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StatutsDossier", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Utilisateurs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Prenom = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Telephone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MotDePasseHash = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstActif = table.Column<bool>(type: "bit", nullable: false),
                    EstListeNoire = table.Column<bool>(type: "bit", nullable: false),
                    MotifListeNoire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DerniereConnexion = table.Column<DateTime>(type: "datetime2", nullable: true),
                    JetonRafraichissement = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExpirationJeton = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ServiceId = table.Column<int>(type: "int", nullable: true),
                    TypeUtilisateur = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EstSupprime = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Utilisateurs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RolesPermissions",
                columns: table => new
                {
                    RoleId = table.Column<int>(type: "int", nullable: false),
                    PermissionId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolesPermissions", x => new { x.RoleId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_RolesPermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolesPermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Dossiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomCitoyen = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmailCitoyen = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TelephoneCitoyen = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MotifRejet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateDepot = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateMiseAJourStatut = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateArchivage = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ServiceId = table.Column<int>(type: "int", nullable: false),
                    StatutId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dossiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dossiers_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Dossiers_StatutsDossier_StatutId",
                        column: x => x.StatutId,
                        principalTable: "StatutsDossier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dossiers_Utilisateurs_AgentId",
                        column: x => x.AgentId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Journaux",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Module = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NiveauId = table.Column<int>(type: "int", nullable: false),
                    DateAction = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    EntiteId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdresseIp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Journaux", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Journaux_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: ""),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false, defaultValue: "INFO"),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NumeroDossier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstLue = table.Column<bool>(type: "bit", nullable: false),
                    EstSupprimee = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "UtilisateursRoles",
                columns: table => new
                {
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoleId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UtilisateursRoles", x => new { x.UtilisateurId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_UtilisateursRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UtilisateursRoles_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HistoriqueStatuts",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AncienStatutId = table.Column<int>(type: "int", nullable: true),
                    NouveauStatutId = table.Column<int>(type: "int", nullable: false),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateChangement = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HistoriqueStatuts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HistoriqueStatuts_Dossiers_DossierId",
                        column: x => x.DossierId,
                        principalTable: "Dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_HistoriqueStatuts_StatutsDossier_AncienStatutId",
                        column: x => x.AncienStatutId,
                        principalTable: "StatutsDossier",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HistoriqueStatuts_StatutsDossier_NouveauStatutId",
                        column: x => x.NouveauStatutId,
                        principalTable: "StatutsDossier",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rappels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Titre = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Objet = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: true, defaultValue: "RAPPEL"),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Statut = table.Column<string>(type: "nvarchar(max)", nullable: true, defaultValue: "ENVOYE"),
                    Tentatives = table.Column<int>(type: "int", nullable: true, defaultValue: 0),
                    Erreur = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EstEffectue = table.Column<bool>(type: "bit", nullable: false),
                    DateRappel = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateEnvoi = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rappels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rappels_Dossiers_DossierId",
                        column: x => x.DossierId,
                        principalTable: "Dossiers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Rappels_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "VersionsDocument",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DossierId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroVersion = table.Column<int>(type: "int", nullable: false),
                    NomFichier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CheminFichier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TypeFichier = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TailleFichier = table.Column<long>(type: "bigint", nullable: true),
                    EmpreinteHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateCreation = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EstActive = table.Column<bool>(type: "bit", nullable: false),
                    UtilisateurId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Commentaire = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VersionsDocument", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VersionsDocument_Dossiers_DossierId",
                        column: x => x.DossierId,
                        principalTable: "Dossiers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_VersionsDocument_Utilisateurs_UtilisateurId",
                        column: x => x.UtilisateurId,
                        principalTable: "Utilisateurs",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Dossiers_AgentId",
                table: "Dossiers",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Dossiers_ServiceId",
                table: "Dossiers",
                column: "ServiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Dossiers_StatutId",
                table: "Dossiers",
                column: "StatutId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueStatuts_AncienStatutId",
                table: "HistoriqueStatuts",
                column: "AncienStatutId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueStatuts_DossierId",
                table: "HistoriqueStatuts",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_HistoriqueStatuts_NouveauStatutId",
                table: "HistoriqueStatuts",
                column: "NouveauStatutId");

            migrationBuilder.CreateIndex(
                name: "IX_Journaux_UtilisateurId",
                table: "Journaux",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UtilisateurId",
                table: "Notifications",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_Rappels_DossierId",
                table: "Rappels",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_Rappels_UtilisateurId",
                table: "Rappels",
                column: "UtilisateurId");

            migrationBuilder.CreateIndex(
                name: "IX_RolesPermissions_PermissionId",
                table: "RolesPermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_Utilisateurs_Email",
                table: "Utilisateurs",
                column: "Email",
                unique: true,
                filter: "[EstSupprime] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_UtilisateursRoles_RoleId",
                table: "UtilisateursRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_VersionsDocument_DossierId",
                table: "VersionsDocument",
                column: "DossierId");

            migrationBuilder.CreateIndex(
                name: "IX_VersionsDocument_UtilisateurId",
                table: "VersionsDocument",
                column: "UtilisateurId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HistoriqueStatuts");

            migrationBuilder.DropTable(
                name: "Journaux");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Rappels");

            migrationBuilder.DropTable(
                name: "RolesPermissions");

            migrationBuilder.DropTable(
                name: "UtilisateursRoles");

            migrationBuilder.DropTable(
                name: "VersionsDocument");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "Dossiers");

            migrationBuilder.DropTable(
                name: "Services");

            migrationBuilder.DropTable(
                name: "StatutsDossier");

            migrationBuilder.DropTable(
                name: "Utilisateurs");
        }
    }
}
