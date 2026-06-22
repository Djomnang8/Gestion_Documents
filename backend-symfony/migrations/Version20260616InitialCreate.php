<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

final class Version20260616InitialCreate extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Création initiale du schéma pour Gestion_Documents (MySQL XAMPP)';
    }

    public function isTransactional(): bool
    {
        return false;
    }

    public function up(Schema $schema): void
    {
        $this->addSql('SET FOREIGN_KEY_CHECKS = 0');

        $this->addSql('CREATE TABLE IF NOT EXISTS Roles (
            id INT AUTO_INCREMENT PRIMARY KEY,
            nom VARCHAR(100) NOT NULL,
            description VARCHAR(255) NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS Permissions (
            id INT AUTO_INCREMENT PRIMARY KEY,
            nom VARCHAR(100) NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS RolesPermissions (
            RoleId INT NOT NULL,
            PermissionId INT NOT NULL,
            PRIMARY KEY (RoleId, PermissionId),
            FOREIGN KEY (RoleId) REFERENCES Roles(id) ON DELETE CASCADE,
            FOREIGN KEY (PermissionId) REFERENCES Permissions(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS Services (
            id INT AUTO_INCREMENT PRIMARY KEY,
            nom VARCHAR(100) NOT NULL,
            description VARCHAR(255) NULL,
            EstActif TINYINT(1) NOT NULL DEFAULT 1
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS StatutsDossier (
            id INT AUTO_INCREMENT PRIMARY KEY,
            code VARCHAR(50) NOT NULL,
            libelle VARCHAR(100) NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS Utilisateurs (
            id INT AUTO_INCREMENT PRIMARY KEY,
            nom VARCHAR(100) NOT NULL,
            prenom VARCHAR(100) NOT NULL,
            email VARCHAR(255) NOT NULL UNIQUE,
            telephone VARCHAR(20) NULL,
            MotDePasseHash VARCHAR(255) NOT NULL,
            EstActif TINYINT(1) NOT NULL DEFAULT 1,
            EstListeNoire TINYINT(1) NOT NULL DEFAULT 0,
            MotifListeNoire VARCHAR(500) NULL,
            DerniereConnexion DATETIME NULL,
            JetonRafraichissement VARCHAR(500) NULL,
            ExpirationJeton DATETIME NULL,
            ServiceId INT NULL,
            TypeUtilisateur VARCHAR(50) NOT NULL DEFAULT \'\',
            EstSupprime TINYINT(1) NOT NULL DEFAULT 0,
            FOREIGN KEY (ServiceId) REFERENCES Services(id) ON DELETE SET NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS UtilisateursRoles (
            UtilisateurId INT NOT NULL,
            RoleId INT NOT NULL,
            PRIMARY KEY (UtilisateurId, RoleId),
            FOREIGN KEY (UtilisateurId) REFERENCES Utilisateurs(id) ON DELETE CASCADE,
            FOREIGN KEY (RoleId) REFERENCES Roles(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS Dossiers (
            id CHAR(36) NOT NULL PRIMARY KEY,
            numero VARCHAR(50) NOT NULL,
            titre VARCHAR(255) NOT NULL,
            description TEXT NULL,
            NomCitoyen VARCHAR(200) NOT NULL,
            EmailCitoyen VARCHAR(255) NULL,
            TelephoneCitoyen VARCHAR(20) NULL,
            MotifRejet TEXT NULL,
            DateDepot DATETIME NOT NULL,
            DateMiseAJourStatut DATETIME NOT NULL,
            DateArchivage DATETIME NULL,
            AgentId INT NULL,
            GroupeArchiveId CHAR(36) NULL,
            NumeroVersionArchive INT NOT NULL DEFAULT 0,
            EstVersionActive TINYINT(1) NOT NULL DEFAULT 0,
            ServiceId INT NOT NULL,
            StatutId INT NOT NULL,
            FOREIGN KEY (ServiceId) REFERENCES Services(id) ON DELETE CASCADE,
            FOREIGN KEY (StatutId) REFERENCES StatutsDossier(id) ON DELETE RESTRICT
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS HistoriqueStatuts (
            id BIGINT AUTO_INCREMENT PRIMARY KEY,
            DossierId CHAR(36) NOT NULL,
            AncienStatutId INT NULL,
            NouveauStatutId INT NOT NULL,
            commentaire TEXT NULL,
            DateChangement DATETIME NOT NULL,
            AgentId INT NULL,
            FOREIGN KEY (DossierId) REFERENCES Dossiers(id) ON DELETE CASCADE,
            FOREIGN KEY (AncienStatutId) REFERENCES StatutsDossier(id) ON DELETE SET NULL,
            FOREIGN KEY (NouveauStatutId) REFERENCES StatutsDossier(id) ON DELETE RESTRICT
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS VersionsDocument (
            id CHAR(36) NOT NULL PRIMARY KEY,
            DossierId CHAR(36) NOT NULL,
            NumeroVersion INT NOT NULL DEFAULT 1,
            NomFichier VARCHAR(255) NOT NULL,
            CheminFichier VARCHAR(500) NOT NULL,
            TypeFichier VARCHAR(100) NULL,
            TailleFichier BIGINT NULL,
            EmpreinteHash VARCHAR(255) NULL,
            DateCreation DATETIME NOT NULL,
            EstActive TINYINT(1) NOT NULL DEFAULT 1,
            UtilisateurId INT NULL,
            Commentaire TEXT NULL,
            FOREIGN KEY (DossierId) REFERENCES Dossiers(id) ON DELETE CASCADE
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('CREATE TABLE IF NOT EXISTS Journaux (
            id INT AUTO_INCREMENT PRIMARY KEY,
            UtilisateurId INT NULL,
            Module VARCHAR(100) NOT NULL,
            Action VARCHAR(100) NOT NULL,
            Details TEXT NULL,
            NiveauId INT NOT NULL DEFAULT 1,
            DateAction DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
            EntiteId VARCHAR(100) NULL,
            AdresseIp VARCHAR(50) NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci');

        $this->addSql('SET FOREIGN_KEY_CHECKS = 1');

        // Données initiales — IDs correspondent à la base de production
        $this->addSql("INSERT IGNORE INTO StatutsDossier (id, code, libelle) VALUES
            (21, 'RECU',      'Reçu'),
            (22, 'EN_COURS',  'En cours de traitement'),
            (23, 'TRANSFERE', 'Transféré'),
            (24, 'REJETE',    'Rejeté'),
            (25, 'TERMINE',   'Terminé'),
            (26, 'ARCHIVE',   'Archivé')");

        $this->addSql("INSERT IGNORE INTO Roles (id, nom, description) VALUES
            (7, 'Administrateur', 'Accès complet'),
            (8, 'Agent', 'Agent de traitement'),
            (9, 'Archiviste', 'Archiviste')");

        $this->addSql("INSERT IGNORE INTO Permissions (id, nom) VALUES
            (24, 'dossiers.voir'),
            (25, 'dossiers.creer'),
            (26, 'dossiers.modifier'),
            (27, 'dossiers.supprimer'),
            (28, 'dossiers.valider'),
            (29, 'dossiers.rejeter'),
            (30, 'archivage.archiver'),
            (31, 'archivage.restaurer'),
            (32, 'utilisateurs.voir'),
            (33, 'utilisateurs.gerer'),
            (34, 'statistiques.voir'),
            (35, 'journaux.voir'),
            (36, 'notifications.voir')");

        $this->addSql("INSERT IGNORE INTO Services (id, nom, description, EstActif) VALUES
            (11, 'Droit des Affaires', NULL, 1),
            (12, 'Droit de la Famille', NULL, 1),
            (13, 'Droit Pénal', NULL, 1),
            (14, 'Droit Immobilier', NULL, 1)");

        $this->addSql("INSERT IGNORE INTO RolesPermissions (RoleId, PermissionId) VALUES
            (7,24),(7,25),(7,26),(7,27),(7,28),(7,29),(7,30),(7,31),(7,32),(7,33),(7,34),(7,35),(7,36),
            (8,24),(8,25),(8,26),(8,28),(8,29),(8,34),(8,36),
            (9,24),(9,30),(9,31),(9,34),(9,36)");

        $this->addSql("INSERT IGNORE INTO Utilisateurs (id, nom, prenom, email, MotDePasseHash, EstActif, EstSupprime, TypeUtilisateur, ServiceId) VALUES
            (8,  'Admin',    'Système', 'mbogo@gmail.com',        '\$2y\$13\$dqo6qC5R1sKOZVmMc2yL8u222/KS3IbmHlNF0rFWsQ4X/VYoJ0mDK', 1, 0, 'Administrateur', NULL),
            (9,  'Dupont',   'Jean',    'jean.dupont@ged.local',  '\$2y\$13\$o/ZmSpkMNwk1J1oiXIiD7.qL/7gNpK2R9ZUXIrFpLsfM2zo5H.lmi',  1, 0, 'Agent',          11),
            (10, 'Atangana', 'pierre',  'atangana@gmail.com',     '\$2y\$13\$To3GspJoYf9r8O2jx3mJpeoX0IYfIUcOzjpYwthDG4TOBmeT9k2mC',  1, 0, 'Agent',          13),
            (11, 'Martin',   'Sophie',  'martin@gmail.com',       '\$2y\$13\$JNg6AwmWGag1FcsvoHMi.OFIyN44lJ1WsaCDrRzQwQ.rAucMDUx3S',  1, 0, 'Archiviste',     NULL)");

        $this->addSql("INSERT IGNORE INTO UtilisateursRoles (UtilisateurId, RoleId) VALUES
            (8, 7), (9, 8), (10, 8), (11, 9)");
    }

    public function down(Schema $schema): void
    {
        $this->addSql('SET FOREIGN_KEY_CHECKS = 0');
        $this->addSql('DROP TABLE IF EXISTS Journaux');
        $this->addSql('DROP TABLE IF EXISTS VersionsDocument');
        $this->addSql('DROP TABLE IF EXISTS HistoriqueStatuts');
        $this->addSql('DROP TABLE IF EXISTS Dossiers');
        $this->addSql('DROP TABLE IF EXISTS UtilisateursRoles');
        $this->addSql('DROP TABLE IF EXISTS Utilisateurs');
        $this->addSql('DROP TABLE IF EXISTS RolesPermissions');
        $this->addSql('DROP TABLE IF EXISTS Services');
        $this->addSql('DROP TABLE IF EXISTS StatutsDossier');
        $this->addSql('DROP TABLE IF EXISTS Permissions');
        $this->addSql('DROP TABLE IF EXISTS Roles');
        $this->addSql('SET FOREIGN_KEY_CHECKS = 1');
    }
}
