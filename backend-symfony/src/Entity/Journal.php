<?php

namespace App\Entity;

use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity]
#[ORM\Table(name: 'Journaux')]
class Journal
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column(type: 'integer')]
    private ?int $id = null;

    #[ORM\Column(name: 'UtilisateurId', type: 'integer', nullable: true)]
    private ?int $utilisateurId = null;

    #[ORM\Column(name: 'Module', type: 'string', length: 100)]
    private string $module = '';

    #[ORM\Column(name: 'Action', type: 'string', length: 100)]
    private string $action = '';

    #[ORM\Column(name: 'Details', type: 'text', nullable: true)]
    private ?string $details = null;

    #[ORM\Column(name: 'NiveauId', type: 'integer')]
    private int $niveauId = 1;

    #[ORM\Column(name: 'DateAction', type: 'datetime')]
    private \DateTimeInterface $dateAction;

    #[ORM\Column(name: 'EntiteId', type: 'string', length: 100, nullable: true)]
    private ?string $entiteId = null;

    #[ORM\Column(name: 'AdresseIp', type: 'string', length: 50, nullable: true)]
    private ?string $adresseIp = null;

    public function __construct()
    {
        $this->dateAction = new \DateTime();
    }

    public function getId(): ?int { return $this->id; }
    public function getUtilisateurId(): ?int { return $this->utilisateurId; }
    public function setUtilisateurId(?int $utilisateurId): self { $this->utilisateurId = $utilisateurId; return $this; }
    public function getModule(): string { return $this->module; }
    public function setModule(string $module): self { $this->module = $module; return $this; }
    public function getAction(): string { return $this->action; }
    public function setAction(string $action): self { $this->action = $action; return $this; }
    public function getDetails(): ?string { return $this->details; }
    public function setDetails(?string $details): self { $this->details = $details; return $this; }
    public function getNiveauId(): int { return $this->niveauId; }
    public function setNiveauId(int $niveauId): self { $this->niveauId = $niveauId; return $this; }
    public function getDateAction(): \DateTimeInterface { return $this->dateAction; }
    public function setDateAction(\DateTimeInterface $dateAction): self { $this->dateAction = $dateAction; return $this; }
    public function getEntiteId(): ?string { return $this->entiteId; }
    public function setEntiteId(?string $entiteId): self { $this->entiteId = $entiteId; return $this; }
    public function getAdresseIp(): ?string { return $this->adresseIp; }
    public function setAdresseIp(?string $adresseIp): self { $this->adresseIp = $adresseIp; return $this; }
}
