<?php

namespace App\Entity;

use Doctrine\Common\Collections\ArrayCollection;
use Doctrine\Common\Collections\Collection;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity]
#[ORM\Table(name: 'Dossiers')]
class Dossier
{
    #[ORM\Id]
    #[ORM\Column(type: 'guid')]
    private string $id;

    #[ORM\Column(type: 'string', length: 50)]
    private string $numero = '';

    #[ORM\Column(type: 'string', length: 255)]
    private string $titre = '';

    #[ORM\Column(type: 'text', nullable: true)]
    private ?string $description = null;

    #[ORM\Column(name: 'NomCitoyen', type: 'string', length: 200)]
    private string $nomCitoyen = '';

    #[ORM\Column(name: 'EmailCitoyen', type: 'string', length: 255, nullable: true)]
    private ?string $emailCitoyen = null;

    #[ORM\Column(name: 'TelephoneCitoyen', type: 'string', length: 20, nullable: true)]
    private ?string $telephoneCitoyen = null;

    #[ORM\Column(name: 'MotifRejet', type: 'text', nullable: true)]
    private ?string $motifRejet = null;

    #[ORM\Column(name: 'DateDepot', type: 'datetime')]
    private \DateTimeInterface $dateDepot;

    #[ORM\Column(name: 'DateMiseAJourStatut', type: 'datetime')]
    private \DateTimeInterface $dateMiseAJourStatut;

    #[ORM\Column(name: 'DateArchivage', type: 'datetime', nullable: true)]
    private ?\DateTimeInterface $dateArchivage = null;

    #[ORM\Column(name: 'AgentId', type: 'integer', nullable: true)]
    private ?int $agentId = null;

    #[ORM\Column(name: 'GroupeArchiveId', type: 'guid', nullable: true)]
    private ?string $groupeArchiveId = null;

    #[ORM\Column(name: 'NumeroVersionArchive', type: 'integer')]
    private int $numeroVersionArchive = 0;

    #[ORM\Column(name: 'EstVersionActive', type: 'boolean')]
    private bool $estVersionActive = false;

    #[ORM\ManyToOne(targetEntity: Service::class, inversedBy: 'dossiers')]
    #[ORM\JoinColumn(name: 'ServiceId', nullable: false, onDelete: 'CASCADE')]
    private Service $service;

    #[ORM\ManyToOne(targetEntity: StatutDossier::class, inversedBy: 'dossiers')]
    #[ORM\JoinColumn(name: 'StatutId', nullable: false, onDelete: 'RESTRICT')]
    private StatutDossier $statut;

    #[ORM\OneToMany(mappedBy: 'dossier', targetEntity: HistoriqueStatut::class, cascade: ['remove'])]
    private Collection $historiqueStatuts;

    #[ORM\OneToMany(mappedBy: 'dossier', targetEntity: VersionDocument::class, cascade: ['remove'])]
    private Collection $versionsDocument;

    public function __construct()
    {
        $this->id = \Symfony\Component\Uid\Uuid::v4()->toRfc4122();
        $this->dateDepot = new \DateTime();
        $this->dateMiseAJourStatut = new \DateTime();
        $this->historiqueStatuts = new ArrayCollection();
        $this->versionsDocument  = new ArrayCollection();
    }

    public function getId(): string { return $this->id; }
    public function setId(string $id): self { $this->id = $id; return $this; }
    public function getNumero(): string { return $this->numero; }
    public function setNumero(string $numero): self { $this->numero = $numero; return $this; }
    public function getTitre(): string { return $this->titre; }
    public function setTitre(string $titre): self { $this->titre = $titre; return $this; }
    public function getDescription(): ?string { return $this->description; }
    public function setDescription(?string $description): self { $this->description = $description; return $this; }
    public function getNomCitoyen(): string { return $this->nomCitoyen; }
    public function setNomCitoyen(string $nomCitoyen): self { $this->nomCitoyen = $nomCitoyen; return $this; }
    public function getEmailCitoyen(): ?string { return $this->emailCitoyen; }
    public function setEmailCitoyen(?string $emailCitoyen): self { $this->emailCitoyen = $emailCitoyen; return $this; }
    public function getTelephoneCitoyen(): ?string { return $this->telephoneCitoyen; }
    public function setTelephoneCitoyen(?string $telephoneCitoyen): self { $this->telephoneCitoyen = $telephoneCitoyen; return $this; }
    public function getMotifRejet(): ?string { return $this->motifRejet; }
    public function setMotifRejet(?string $motifRejet): self { $this->motifRejet = $motifRejet; return $this; }
    public function getDateDepot(): \DateTimeInterface { return $this->dateDepot; }
    public function setDateDepot(\DateTimeInterface $dateDepot): self { $this->dateDepot = $dateDepot; return $this; }
    public function getDateMiseAJourStatut(): \DateTimeInterface { return $this->dateMiseAJourStatut; }
    public function setDateMiseAJourStatut(\DateTimeInterface $date): self { $this->dateMiseAJourStatut = $date; return $this; }
    public function getDateArchivage(): ?\DateTimeInterface { return $this->dateArchivage; }
    public function setDateArchivage(?\DateTimeInterface $dateArchivage): self { $this->dateArchivage = $dateArchivage; return $this; }
    public function getAgentId(): ?int { return $this->agentId; }
    public function setAgentId(?int $agentId): self { $this->agentId = $agentId; return $this; }
    public function getGroupeArchiveId(): ?string { return $this->groupeArchiveId; }
    public function setGroupeArchiveId(?string $groupeArchiveId): self { $this->groupeArchiveId = $groupeArchiveId; return $this; }
    public function getNumeroVersionArchive(): int { return $this->numeroVersionArchive; }
    public function setNumeroVersionArchive(int $n): self { $this->numeroVersionArchive = $n; return $this; }
    public function isEstVersionActive(): bool { return $this->estVersionActive; }
    public function setEstVersionActive(bool $estVersionActive): self { $this->estVersionActive = $estVersionActive; return $this; }
    public function getService(): Service { return $this->service; }
    public function setService(Service $service): self { $this->service = $service; return $this; }
    public function getStatut(): StatutDossier { return $this->statut; }
    public function setStatut(StatutDossier $statut): self { $this->statut = $statut; return $this; }
    public function getHistoriqueStatuts(): Collection { return $this->historiqueStatuts; }
    public function getVersionsDocument(): Collection { return $this->versionsDocument; }
}
