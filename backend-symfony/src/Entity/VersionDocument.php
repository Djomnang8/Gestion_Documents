<?php

namespace App\Entity;

use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity]
#[ORM\Table(name: 'VersionsDocument')]
class VersionDocument
{
    #[ORM\Id]
    #[ORM\Column(type: 'guid')]
    private string $id;

    #[ORM\ManyToOne(targetEntity: Dossier::class, inversedBy: 'versionsDocument')]
    #[ORM\JoinColumn(name: 'DossierId', nullable: false, onDelete: 'CASCADE')]
    private Dossier $dossier;

    #[ORM\Column(name: 'NumeroVersion', type: 'integer')]
    private int $numeroVersion = 1;

    #[ORM\Column(name: 'NomFichier', type: 'string', length: 255)]
    private string $nomFichier = '';

    #[ORM\Column(name: 'CheminFichier', type: 'string', length: 500)]
    private string $cheminFichier = '';

    #[ORM\Column(name: 'TypeFichier', type: 'string', length: 100, nullable: true)]
    private ?string $typeFichier = null;

    #[ORM\Column(name: 'TailleFichier', type: 'bigint', nullable: true)]
    private ?int $tailleFichier = null;

    #[ORM\Column(name: 'EmpreinteHash', type: 'string', length: 255, nullable: true)]
    private ?string $empreinteHash = null;

    #[ORM\Column(name: 'DateCreation', type: 'datetime')]
    private \DateTimeInterface $dateCreation;

    #[ORM\Column(name: 'EstActive', type: 'boolean')]
    private bool $estActive = true;

    #[ORM\Column(name: 'UtilisateurId', type: 'integer', nullable: true)]
    private ?int $utilisateurId = null;

    #[ORM\Column(name: 'Commentaire', type: 'text', nullable: true)]
    private ?string $commentaire = null;

    public function __construct()
    {
        $this->id = \Symfony\Component\Uid\Uuid::v4()->toRfc4122();
        $this->dateCreation = new \DateTime();
    }

    public function getId(): string { return $this->id; }
    public function setId(string $id): self { $this->id = $id; return $this; }
    public function getDossier(): Dossier { return $this->dossier; }
    public function setDossier(Dossier $dossier): self { $this->dossier = $dossier; return $this; }
    public function getNumeroVersion(): int { return $this->numeroVersion; }
    public function setNumeroVersion(int $numeroVersion): self { $this->numeroVersion = $numeroVersion; return $this; }
    public function getNomFichier(): string { return $this->nomFichier; }
    public function setNomFichier(string $nomFichier): self { $this->nomFichier = $nomFichier; return $this; }
    public function getCheminFichier(): string { return $this->cheminFichier; }
    public function setCheminFichier(string $cheminFichier): self { $this->cheminFichier = $cheminFichier; return $this; }
    public function getTypeFichier(): ?string { return $this->typeFichier; }
    public function setTypeFichier(?string $typeFichier): self { $this->typeFichier = $typeFichier; return $this; }
    public function getTailleFichier(): ?int { return $this->tailleFichier; }
    public function setTailleFichier(?int $tailleFichier): self { $this->tailleFichier = $tailleFichier; return $this; }
    public function getDateCreation(): \DateTimeInterface { return $this->dateCreation; }
    public function setDateCreation(\DateTimeInterface $dateCreation): self { $this->dateCreation = $dateCreation; return $this; }
    public function isEstActive(): bool { return $this->estActive; }
    public function setEstActive(bool $estActive): self { $this->estActive = $estActive; return $this; }
    public function getUtilisateurId(): ?int { return $this->utilisateurId; }
    public function setUtilisateurId(?int $utilisateurId): self { $this->utilisateurId = $utilisateurId; return $this; }
    public function getCommentaire(): ?string { return $this->commentaire; }
    public function setCommentaire(?string $commentaire): self { $this->commentaire = $commentaire; return $this; }
}
