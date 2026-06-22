<?php

namespace App\Entity;

use Doctrine\ORM\Mapping as ORM;

#[ORM\Entity]
#[ORM\Table(name: 'UtilisateursRoles')]
class UtilisateurRole
{
    #[ORM\Id]
    #[ORM\ManyToOne(targetEntity: Utilisateur::class, inversedBy: 'utilisateursRoles')]
    #[ORM\JoinColumn(name: 'UtilisateurId', referencedColumnName: 'id', nullable: false)]
    private Utilisateur $utilisateur;

    #[ORM\Id]
    #[ORM\ManyToOne(targetEntity: Role::class, inversedBy: 'utilisateursRoles')]
    #[ORM\JoinColumn(name: 'RoleId', referencedColumnName: 'id', nullable: false)]
    private Role $role;

    public function getUtilisateur(): Utilisateur { return $this->utilisateur; }
    public function setUtilisateur(Utilisateur $utilisateur): self { $this->utilisateur = $utilisateur; return $this; }
    public function getRole(): Role { return $this->role; }
    public function setRole(Role $role): self { $this->role = $role; return $this; }
}
