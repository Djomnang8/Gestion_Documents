<?php

namespace App\Entity;

use Doctrine\Common\Collections\ArrayCollection;
use Doctrine\Common\Collections\Collection;
use Doctrine\ORM\Mapping as ORM;
use Symfony\Component\Security\Core\User\PasswordAuthenticatedUserInterface;
use Symfony\Component\Security\Core\User\UserInterface;

#[ORM\Entity]
#[ORM\Table(name: 'Utilisateurs')]
class Utilisateur implements UserInterface, PasswordAuthenticatedUserInterface
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column(type: 'integer')]
    private ?int $id = null;

    #[ORM\Column(type: 'string', length: 100)]
    private string $nom = '';

    #[ORM\Column(type: 'string', length: 100)]
    private string $prenom = '';

    #[ORM\Column(type: 'string', length: 255, unique: true)]
    private string $email = '';

    #[ORM\Column(type: 'string', length: 20, nullable: true)]
    private ?string $telephone = null;

    #[ORM\Column(name: 'MotDePasseHash', type: 'string', length: 255)]
    private string $motDePasseHash = '';

    #[ORM\Column(name: 'EstActif', type: 'boolean')]
    private bool $estActif = true;

    #[ORM\Column(name: 'EstListeNoire', type: 'boolean')]
    private bool $estListeNoire = false;

    #[ORM\Column(name: 'MotifListeNoire', type: 'string', length: 500, nullable: true)]
    private ?string $motifListeNoire = null;

    #[ORM\Column(name: 'DerniereConnexion', type: 'datetime', nullable: true)]
    private ?\DateTimeInterface $derniereConnexion = null;

    #[ORM\Column(name: 'JetonRafraichissement', type: 'string', length: 500, nullable: true)]
    private ?string $jetonRafraichissement = null;

    #[ORM\Column(name: 'ExpirationJeton', type: 'datetime', nullable: true)]
    private ?\DateTimeInterface $expirationJeton = null;

    #[ORM\Column(name: 'ServiceId', type: 'integer', nullable: true)]
    private ?int $serviceId = null;

    #[ORM\Column(name: 'TypeUtilisateur', type: 'string', length: 50)]
    private string $typeUtilisateur = '';

    #[ORM\Column(name: 'EstSupprime', type: 'boolean')]
    private bool $estSupprime = false;

    #[ORM\OneToMany(mappedBy: 'utilisateur', targetEntity: UtilisateurRole::class, cascade: ['persist', 'remove'])]
    private Collection $utilisateursRoles;

    public function __construct()
    {
        $this->utilisateursRoles = new ArrayCollection();
    }

    public function getId(): ?int { return $this->id; }
    public function getNom(): string { return $this->nom; }
    public function setNom(string $nom): self { $this->nom = $nom; return $this; }
    public function getPrenom(): string { return $this->prenom; }
    public function setPrenom(string $prenom): self { $this->prenom = $prenom; return $this; }
    public function getEmail(): string { return $this->email; }
    public function setEmail(string $email): self { $this->email = $email; return $this; }
    public function getTelephone(): ?string { return $this->telephone; }
    public function setTelephone(?string $telephone): self { $this->telephone = $telephone; return $this; }
    public function getMotDePasseHash(): string { return $this->motDePasseHash; }
    public function setMotDePasseHash(string $motDePasseHash): self { $this->motDePasseHash = $motDePasseHash; return $this; }
    public function isEstActif(): bool { return $this->estActif; }
    public function setEstActif(bool $estActif): self { $this->estActif = $estActif; return $this; }
    public function isEstListeNoire(): bool { return $this->estListeNoire; }
    public function setEstListeNoire(bool $estListeNoire): self { $this->estListeNoire = $estListeNoire; return $this; }
    public function getMotifListeNoire(): ?string { return $this->motifListeNoire; }
    public function setMotifListeNoire(?string $motif): self { $this->motifListeNoire = $motif; return $this; }
    public function getDerniereConnexion(): ?\DateTimeInterface { return $this->derniereConnexion; }
    public function setDerniereConnexion(?\DateTimeInterface $date): self { $this->derniereConnexion = $date; return $this; }
    public function getServiceId(): ?int { return $this->serviceId; }
    public function setServiceId(?int $serviceId): self { $this->serviceId = $serviceId; return $this; }
    public function getTypeUtilisateur(): string { return $this->typeUtilisateur; }
    public function setTypeUtilisateur(string $type): self { $this->typeUtilisateur = $type; return $this; }
    public function isEstSupprime(): bool { return $this->estSupprime; }
    public function setEstSupprime(bool $estSupprime): self { $this->estSupprime = $estSupprime; return $this; }
    public function getUtilisateursRoles(): Collection { return $this->utilisateursRoles; }

    // UserInterface
    public function getUserIdentifier(): string { return $this->email; }
    public function getRoles(): array { return ['ROLE_USER']; }
    public function getPassword(): ?string { return $this->motDePasseHash; }
    public function eraseCredentials(): void {}

    // PasswordAuthenticatedUserInterface
    public function getPasswordHash(): string { return $this->motDePasseHash; }
}
