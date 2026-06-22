-- ============================================================
-- Script SQL pour XAMPP MariaDB (port 3306)
-- Base de données : gestion_documents
-- Compatible avec le backend Symfony 7.1 + Doctrine ORM
-- NOTE : Pour un fresh install, utiliser les migrations Doctrine :
--        php bin/console doctrine:migrations:migrate
-- ============================================================

CREATE DATABASE IF NOT EXISTS `gestion_documents`
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE `gestion_documents`;

SET FOREIGN_KEY_CHECKS = 0;

-- ── Tables ────────────────────────────────────────────────────
-- Les noms de colonnes respectent la casse utilisée par les entités Doctrine

CREATE TABLE IF NOT EXISTS `Roles` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `nom` varchar(100) NOT NULL,
  `description` varchar(255) DEFAULT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Permissions` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `nom` varchar(100) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `RolesPermissions` (
  `RoleId` int(11) NOT NULL,
  `PermissionId` int(11) NOT NULL,
  PRIMARY KEY (`RoleId`, `PermissionId`),
  FOREIGN KEY (`RoleId`) REFERENCES `Roles`(`id`) ON DELETE CASCADE,
  FOREIGN KEY (`PermissionId`) REFERENCES `Permissions`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Services` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `nom` varchar(100) NOT NULL,
  `description` varchar(255) DEFAULT NULL,
  `EstActif` tinyint(1) NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `StatutsDossier` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `code` varchar(50) NOT NULL,
  `libelle` varchar(100) NOT NULL,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Utilisateurs` (
  `id` int(11) NOT NULL AUTO_INCREMENT,
  `nom` varchar(100) NOT NULL,
  `prenom` varchar(100) NOT NULL,
  `email` varchar(255) NOT NULL,
  `telephone` varchar(20) DEFAULT NULL,
  `MotDePasseHash` varchar(255) NOT NULL,
  `EstActif` tinyint(1) NOT NULL DEFAULT 1,
  `EstListeNoire` tinyint(1) NOT NULL DEFAULT 0,
  `MotifListeNoire` varchar(500) DEFAULT NULL,
  `DerniereConnexion` datetime DEFAULT NULL,
  `JetonRafraichissement` varchar(500) DEFAULT NULL,
  `ExpirationJeton` datetime DEFAULT NULL,
  `ServiceId` int(11) DEFAULT NULL,
  `TypeUtilisateur` varchar(50) NOT NULL DEFAULT '',
  `EstSupprime` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_email` (`email`),
  FOREIGN KEY (`ServiceId`) REFERENCES `Services`(`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `UtilisateursRoles` (
  `UtilisateurId` int(11) NOT NULL,
  `RoleId` int(11) NOT NULL,
  PRIMARY KEY (`UtilisateurId`, `RoleId`),
  FOREIGN KEY (`UtilisateurId`) REFERENCES `Utilisateurs`(`id`) ON DELETE CASCADE,
  FOREIGN KEY (`RoleId`) REFERENCES `Roles`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Dossiers` (
  `id` CHAR(36) NOT NULL,
  `numero` VARCHAR(50) NOT NULL,
  `titre` VARCHAR(255) NOT NULL,
  `description` TEXT NULL,
  `NomCitoyen` VARCHAR(200) NOT NULL,
  `EmailCitoyen` VARCHAR(255) NULL,
  `TelephoneCitoyen` VARCHAR(20) NULL,
  `MotifRejet` TEXT NULL,
  `DateDepot` DATETIME NOT NULL,
  `DateMiseAJourStatut` DATETIME NOT NULL,
  `DateArchivage` DATETIME NULL,
  `AgentId` INT NULL,
  `GroupeArchiveId` CHAR(36) NULL,
  `NumeroVersionArchive` INT NOT NULL DEFAULT 0,
  `EstVersionActive` TINYINT(1) NOT NULL DEFAULT 0,
  `ServiceId` INT NOT NULL,
  `StatutId` INT NOT NULL,
  PRIMARY KEY (`id`),
  FOREIGN KEY (`ServiceId`) REFERENCES `Services`(`id`) ON DELETE CASCADE,
  FOREIGN KEY (`StatutId`) REFERENCES `StatutsDossier`(`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `HistoriqueStatuts` (
  `id` BIGINT AUTO_INCREMENT PRIMARY KEY,
  `DossierId` CHAR(36) NOT NULL,
  `AncienStatutId` INT NULL,
  `NouveauStatutId` INT NOT NULL,
  `commentaire` TEXT NULL,
  `DateChangement` DATETIME NOT NULL,
  `AgentId` INT NULL,
  FOREIGN KEY (`DossierId`) REFERENCES `Dossiers`(`id`) ON DELETE CASCADE,
  FOREIGN KEY (`AncienStatutId`) REFERENCES `StatutsDossier`(`id`) ON DELETE SET NULL,
  FOREIGN KEY (`NouveauStatutId`) REFERENCES `StatutsDossier`(`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `VersionsDocument` (
  `id` CHAR(36) NOT NULL PRIMARY KEY,
  `DossierId` CHAR(36) NOT NULL,
  `NumeroVersion` INT NOT NULL DEFAULT 1,
  `NomFichier` VARCHAR(255) NOT NULL,
  `CheminFichier` VARCHAR(500) NOT NULL,
  `TypeFichier` VARCHAR(100) NULL,
  `TailleFichier` BIGINT NULL,
  `EmpreinteHash` VARCHAR(255) NULL,
  `DateCreation` DATETIME NOT NULL,
  `EstActive` TINYINT(1) NOT NULL DEFAULT 1,
  `UtilisateurId` INT NULL,
  `Commentaire` TEXT NULL,
  FOREIGN KEY (`DossierId`) REFERENCES `Dossiers`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Journaux` (
  `id` INT AUTO_INCREMENT PRIMARY KEY,
  `UtilisateurId` INT NULL,
  `Module` VARCHAR(100) NOT NULL,
  `Action` VARCHAR(100) NOT NULL,
  `Details` TEXT NULL,
  `NiveauId` INT NOT NULL DEFAULT 1,
  `DateAction` DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `EntiteId` VARCHAR(100) NULL,
  `AdresseIp` VARCHAR(50) NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Table de suivi des migrations Doctrine
CREATE TABLE IF NOT EXISTS `doctrine_migration_versions` (
  `version` VARCHAR(191) NOT NULL PRIMARY KEY,
  `executed_at` DATETIME NULL,
  `execution_time` INT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;

-- ── Données initiales ─────────────────────────────────────────

INSERT INTO `Permissions` (`id`, `nom`) VALUES
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
  (35, 'journaux.voir');

INSERT INTO `Roles` (`id`, `nom`, `description`) VALUES
  (7, 'Administrateur', 'Accès complet'),
  (8, 'Agent', 'Agent de traitement'),
  (9, 'Archiviste', 'Archiviste');

INSERT INTO `RolesPermissions` (`RoleId`, `PermissionId`) VALUES
  (7,24),(7,25),(7,26),(7,27),(7,28),(7,29),(7,30),(7,31),(7,32),(7,33),(7,34),(7,35),
  (8,24),(8,25),(8,26),(8,28),(8,29),(8,34),
  (9,24),(9,30),(9,31),(9,34);

INSERT INTO `Services` (`id`, `nom`, `description`, `EstActif`) VALUES
  (11, 'Droit des Affaires', NULL, 1),
  (12, 'Droit de la Famille', NULL, 1),
  (13, 'Droit Pénal', NULL, 1),
  (14, 'Droit Immobilier', NULL, 1);

INSERT INTO `StatutsDossier` (`id`, `code`, `libelle`) VALUES
  (21, 'RECU',      'Reçu'),
  (22, 'EN_COURS',  'En cours de traitement'),
  (23, 'TRANSFERE', 'Transféré'),
  (24, 'REJETE',    'Rejeté'),
  (25, 'TERMINE',   'Terminé'),
  (26, 'ARCHIVE',   'Archivé');

INSERT INTO `Utilisateurs` (`id`, `nom`, `prenom`, `email`, `telephone`, `MotDePasseHash`, `EstActif`, `EstListeNoire`, `MotifListeNoire`, `DerniereConnexion`, `TypeUtilisateur`, `EstSupprime`, `ServiceId`) VALUES
  (8,  'Admin',    'Système', 'mbogo@gmail.com',        NULL, '$2y$13$dqo6qC5R1sKOZVmMc2yL8u222/KS3IbmHlNF0rFWsQ4X/VYoJ0mDK', 1, 0, NULL, '2026-06-10 19:39:13', 'Administrateur', 0, NULL),
  (9,  'Dupont',   'Jean',    'jean.dupont@ged.local',  NULL, '$2y$13$o/ZmSpkMNwk1J1oiXIiD7.qL/7gNpK2R9ZUXIrFpLsfM2zo5H.lmi',  1, 0, NULL, '2026-06-10 22:09:46', 'Agent',          0, 11),
  (10, 'Atangana', 'pierre',  'atangana@gmail.com',     NULL, '$2y$13$To3GspJoYf9r8O2jx3mJpeoX0IYfIUcOzjpYwthDG4TOBmeT9k2mC',  1, 0, NULL, '2026-05-17 13:22:26', 'Agent',          0, 13),
  (11, 'Martin',   'Sophie',  'martin@gmail.com',       NULL, '$2y$13$JNg6AwmWGag1FcsvoHMi.OFIyN44lJ1WsaCDrRzQwQ.rAucMDUx3S',  1, 0, NULL, '2026-06-04 22:48:43', 'Archiviste',     0, NULL);

INSERT INTO `UtilisateursRoles` (`UtilisateurId`, `RoleId`) VALUES
  (8, 7),
  (9, 8),
  (10, 8),
  (11, 9);
