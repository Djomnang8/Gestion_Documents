using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GestionDocuments.API.Migrations
{
    /// <inheritdoc />
    public partial class AddArchiveVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupeArchiveId",
                table: "Dossiers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NumeroVersionArchive",
                table: "Dossiers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "EstVersionActive",
                table: "Dossiers",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "GroupeArchiveId", table: "Dossiers");
            migrationBuilder.DropColumn(name: "NumeroVersionArchive", table: "Dossiers");
            migrationBuilder.DropColumn(name: "EstVersionActive", table: "Dossiers");
        }
    }
}
