<?php

declare(strict_types=1);

namespace DoctrineMigrations;

use Doctrine\DBAL\Schema\Schema;
use Doctrine\Migrations\AbstractMigration;

final class Version20260617FixData extends AbstractMigration
{
    public function getDescription(): string
    {
        return 'Nettoie les données incorrectes insérées par la migration initiale et insère les données correctes';
    }

    public function isTransactional(): bool
    {
        return false;
    }

    public function up(Schema $schema): void
    {
        $this->addSql('SET FOREIGN_KEY_CHECKS = 0');

        // Supprimer l'utilisateur admin fantôme (id=0, issu de la conversion UUID→INT)
        $this->addSql('DELETE FROM UtilisateursRoles WHERE UtilisateurId = 0');
        $this->addSql('DELETE FROM Utilisateurs WHERE id = 0');

        // Supprimer les anciens rôles (1,2,3) et leurs liaisons
        $this->addSql('DELETE FROM UtilisateursRoles WHERE RoleId IN (1, 2, 3)');
        $this->addSql('DELETE FROM RolesPermissions WHERE RoleId IN (1, 2, 3)');
        $this->addSql('DELETE FROM Roles WHERE id IN (1, 2, 3)');

        // Supprimer les anciens statuts (1-6)
        $this->addSql('DELETE FROM StatutsDossier WHERE id IN (1, 2, 3, 4, 5, 6)');

        // Supprimer les anciennes permissions (1-8)
        $this->addSql('DELETE FROM RolesPermissions WHERE PermissionId IN (1, 2, 3, 4, 5, 6, 7, 8)');
        $this->addSql('DELETE FROM Permissions WHERE id IN (1, 2, 3, 4, 5, 6, 7, 8)');

        // Supprimer les anciens services (1-3)
        $this->addSql('DELETE FROM Services WHERE id IN (1, 2, 3)');

        // Insérer les données correctes (si pas déjà présentes)
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

        $this->addSql("INSERT IGNORE INTO StatutsDossier (id, code, libelle) VALUES
            (21, 'RECU',      'Reçu'),
            (22, 'EN_COURS',  'En cours de traitement'),
            (23, 'TRANSFERE', 'Transféré'),
            (24, 'REJETE',    'Rejeté'),
            (25, 'TERMINE',   'Terminé'),
            (26, 'ARCHIVE',   'Archivé')");

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

        $this->addSql('SET FOREIGN_KEY_CHECKS = 1');
    }

    public function down(Schema $schema): void
    {
        // Irréversible — ne pas exécuter down() sur une base de production
    }
}
