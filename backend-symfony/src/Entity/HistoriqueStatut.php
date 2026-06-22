<?php

namespace App\Entity;

use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity]
#[ORM\Table(name: 'HistoriqueStatuts')]
class HistoriqueStatut
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column(type: 'bigint')]
    private ?int $id = null;

    #[ORM\ManyToOne(targetEntity: Dossier::class, inversedBy: 'historiqueStatuts')]
    #[ORM\JoinColumn(name: 'DossierId', nullable: false, onDelete: 'CASCADE')]
    private Dossier $dossier;

    /** Garde une copie du code de l'ancien statut (nullable si c'est la création) */
    #[ORM\ManyToOne(targetEntity: StatutDossier::class)]
    #[ORM\JoinColumn(name: 'AncienStatutId', nullable: true, onDelete: 'SET NULL')]
    private ?StatutDossier $ancienStatut = null;

    #[ORM\ManyToOne(targetEntity: StatutDossier::class)]
    #[ORM\JoinColumn(name: 'NouveauStatutId', nullable: false, onDelete: 'RESTRICT')]
    private StatutDossier $nouveauStatut;

    #[ORM\Column(type: 'text', nullable: true)]
    private ?string $commentaire = null;

    #[ORM\Column(name: 'DateChangement', type: 'datetime')]
    private \DateTimeInterface $dateChangement;

    #[ORM\Column(name: 'AgentId', type: 'integer', nullable: true)]
    private ?int $agentId = null;

    public function __construct()
    {
        $this->dateChangement = new \DateTime();
    }

    public function getId(): ?int { return $this->id; }
    public function getDossier(): Dossier { return $this->dossier; }
    public function setDossier(Dossier $dossier): self { $this->dossier = $dossier; return $this; }
    public function getAncienStatut(): ?StatutDossier { return $this->ancienStatut; }
    public function setAncienStatut(?StatutDossier $ancienStatut): self { $this->ancienStatut = $ancienStatut; return $this; }
    public function getNouveauStatut(): StatutDossier { return $this->nouveauStatut; }
    public function setNouveauStatut(StatutDossier $nouveauStatut): self { $this->nouveauStatut = $nouveauStatut; return $this; }
    public function getCommentaire(): ?string { return $this->commentaire; }
    public function setCommentaire(?string $commentaire): self { $this->commentaire = $commentaire; return $this; }
    public function getDateChangement(): \DateTimeInterface { return $this->dateChangement; }
    public function setDateChangement(\DateTimeInterface $dateChangement): self { $this->dateChangement = $dateChangement; return $this; }
    public function getAgentId(): ?int { return $this->agentId; }
    public function setAgentId(?int $agentId): self { $this->agentId = $agentId; return $this; }
}
