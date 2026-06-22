<?php

namespace App\Entity;

use Doctrine\Common\Collections\ArrayCollection;
use Doctrine\Common\Collections\Collection;
use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity]
#[ORM\Table(name: 'Roles')]
class Role
{
    #[ORM\Id]
    #[ORM\GeneratedValue]
    #[ORM\Column(type: 'integer')]
    private ?int $id = null;

    #[ORM\Column(type: 'string', length: 100)]
    private string $nom = '';

    #[ORM\Column(type: 'string', length: 255, nullable: true)]
    private ?string $description = null;

    #[ORM\OneToMany(mappedBy: 'role', targetEntity: UtilisateurRole::class)]
    private Collection $utilisateursRoles;

    #[ORM\OneToMany(mappedBy: 'role', targetEntity: RolePermission::class)]
    private Collection $rolesPermissions;

    public function __construct()
    {
        $this->utilisateursRoles = new ArrayCollection();
        $this->rolesPermissions  = new ArrayCollection();
    }

    public function getId(): ?int { return $this->id; }
    public function getNom(): string { return $this->nom; }
    public function setNom(string $nom): self { $this->nom = $nom; return $this; }
    public function getDescription(): ?string { return $this->description; }
    public function setDescription(?string $description): self { $this->description = $description; return $this; }
    public function getUtilisateursRoles(): Collection { return $this->utilisateursRoles; }
    public function getRolesPermissions(): Collection { return $this->rolesPermissions; }
}
