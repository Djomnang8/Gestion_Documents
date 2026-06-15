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
            // Colonnes pour le versionnement des archives (idempotent avec IF NOT EXISTS)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Dossiers') AND name = 'GroupeArchiveId')
                    ALTER TABLE [Dossiers] ADD [GroupeArchiveId] uniqueidentifier NULL;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Dossiers') AND name = 'NumeroVersionArchive')
                    ALTER TABLE [Dossiers] ADD [NumeroVersionArchive] int NOT NULL DEFAULT 0;
            ");
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Dossiers') AND name = 'EstVersionActive')
                    ALTER TABLE [Dossiers] ADD [EstVersionActive] bit NOT NULL DEFAULT 0;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Dossiers') AND name = 'GroupeArchiveId')
                    ALTER TABLE [Dossiers] DROP COLUMN [GroupeArchiveId];
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Dossiers') AND name = 'NumeroVersionArchive')
                    ALTER TABLE [Dossiers] DROP COLUMN [NumeroVersionArchive];
                IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Dossiers') AND name = 'EstVersionActive')
                    ALTER TABLE [Dossiers] DROP COLUMN [EstVersionActive];
            ");
        }
    }
}
