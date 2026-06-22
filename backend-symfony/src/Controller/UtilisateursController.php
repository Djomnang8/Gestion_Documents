<?php

namespace App\Controller;

use App\Entity\Role;
use App\Entity\Service;
use App\Entity\Utilisateur;
use App\Entity\UtilisateurRole;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/utilisateurs')]
class UtilisateursController extends AbstractController
{
    public function __construct(private EntityManagerInterface $em) {}

    #[Route('', name: 'api_utilisateurs_list', methods: ['GET'])]
    public function list(Request $request): JsonResponse
    {
        $recherche = $request->query->get('recherche');
        $role      = $request->query->get('role');
        $page      = max(1, (int)$request->query->get('page', 1));
        $pageSize  = max(1, (int)$request->query->get('pageSize', 20));

        $qb = $this->em->createQueryBuilder()
            ->select('u')
            ->from(Utilisateur::class, 'u')
            ->where('u.estSupprime = false');

        if ($recherche) {
            $qb->andWhere('u.nom LIKE :r OR u.prenom LIKE :r OR u.email LIKE :r')
               ->setParameter('r', '%'.$recherche.'%');
        }

        if ($role) {
            $qb->join('u.utilisateursRoles', 'ur')->join('ur.role', 'ro')
               ->andWhere('ro.nom = :role')->setParameter('role', $role);
        }

        $total = (clone $qb)->select('COUNT(u.id)')->getQuery()->getSingleScalarResult();

        $utilisateurs = $qb->select('u')
            ->orderBy('u.nom', 'ASC')
            ->setFirstResult(($page - 1) * $pageSize)
            ->setMaxResults($pageSize)
            ->getQuery()
            ->getResult();

        return $this->json([
            'total'    => (int)$total,
            'page'     => $page,
            'pageSize' => $pageSize,
            'data'     => array_map(fn(Utilisateur $u) => $this->toDto($u), $utilisateurs),
        ]);
    }

    #[Route('/roles', name: 'api_roles_list', methods: ['GET'])]
    public function roles(): JsonResponse
    {
        $roles = $this->em->getRepository(Role::class)->findAll();
        return $this->json(array_map(fn(Role $r) => [
            'id'  => $r->getId(),
            'nom' => $r->getNom(),
        ], $roles));
    }

    #[Route('/services', name: 'api_utilisateurs_services', methods: ['GET'])]
    public function services(): JsonResponse
    {
        $services = $this->em->getRepository(Service::class)
            ->createQueryBuilder('s')
            ->where('s.estActif = true')
            ->orderBy('s.nom', 'ASC')
            ->getQuery()
            ->getResult();
        return $this->json(array_map(fn(Service $s) => [
            'id'  => $s->getId(),
            'nom' => $s->getNom(),
        ], $services));
    }

    #[Route('/{id}', name: 'api_utilisateurs_get', methods: ['GET'])]
    public function get(string $id): JsonResponse
    {
        $u = $this->em->getRepository(Utilisateur::class)->find($id);
        if (!$u || $u->isEstSupprime()) return $this->json(['error' => 'Utilisateur introuvable.'], 404);
        return $this->json($this->toDto($u));
    }

    #[Route('', name: 'api_utilisateurs_create', methods: ['POST'])]
    public function create(Request $request): JsonResponse
    {
        $data = json_decode($request->getContent(), true);

        $existing = $this->em->getRepository(Utilisateur::class)->findOneBy(['email' => $data['email'] ?? '']);
        if ($existing) return $this->json(['error' => 'Email déjà utilisé.'], 409);

        $u = new Utilisateur();
        $u->setNom($data['nom'] ?? '')
          ->setPrenom($data['prenom'] ?? '')
          ->setEmail($data['email'] ?? '')
          ->setTelephone($data['telephone'] ?? null)
          ->setMotDePasseHash($data['motDePasse'] ?? 'password')
          ->setEstActif($data['estActif'] ?? true)
          ->setTypeUtilisateur($data['typeUtilisateur'] ?? '')
          ->setServiceId($data['serviceId'] ?? null);

        $this->em->persist($u);

        if (isset($data['roleId'])) {
            $role = $this->em->getRepository(Role::class)->find($data['roleId']);
            if ($role) {
                $ur = new UtilisateurRole();
                $ur->setUtilisateur($u)->setRole($role);
                $this->em->persist($ur);
            }
        }

        $this->em->flush();
        return $this->json($this->toDto($u), 201);
    }

    #[Route('/{id}', name: 'api_utilisateurs_update', methods: ['PUT', 'PATCH'])]
    public function update(string $id, Request $request): JsonResponse
    {
        $u = $this->em->getRepository(Utilisateur::class)->find($id);
        if (!$u || $u->isEstSupprime()) return $this->json(['error' => 'Utilisateur introuvable.'], 404);

        $data = json_decode($request->getContent(), true);
        if (isset($data['nom']))              $u->setNom($data['nom']);
        if (isset($data['prenom']))           $u->setPrenom($data['prenom']);
        if (isset($data['email']))            $u->setEmail($data['email']);
        if (isset($data['telephone']))        $u->setTelephone($data['telephone']);
        if (isset($data['estActif']))         $u->setEstActif($data['estActif']);
        if (isset($data['estListeNoire']))    $u->setEstListeNoire($data['estListeNoire']);
        if (isset($data['motifListeNoire']))  $u->setMotifListeNoire($data['motifListeNoire']);
        if (isset($data['serviceId']))        $u->setServiceId($data['serviceId']);
        if (isset($data['typeUtilisateur']))  $u->setTypeUtilisateur($data['typeUtilisateur']);
        if (!empty($data['motDePasse']))      $u->setMotDePasseHash($data['motDePasse']);

        if (isset($data['roleId'])) {
            foreach ($u->getUtilisateursRoles() as $ur) { $this->em->remove($ur); }
            $this->em->flush();
            $role = $this->em->getRepository(Role::class)->find($data['roleId']);
            if ($role) {
                $ur = new UtilisateurRole();
                $ur->setUtilisateur($u)->setRole($role);
                $this->em->persist($ur);
            }
        }

        $this->em->flush();
        return $this->json($this->toDto($u));
    }

    #[Route('/{id}', name: 'api_utilisateurs_delete', methods: ['DELETE'])]
    public function delete(string $id): JsonResponse
    {
        $u = $this->em->getRepository(Utilisateur::class)->find($id);
        if (!$u || $u->isEstSupprime()) return $this->json(['error' => 'Utilisateur introuvable.'], 404);
        $u->setEstSupprime(true);
        $this->em->flush();
        return $this->json(['message' => 'Utilisateur supprimé.']);
    }

    private function toDto(Utilisateur $u): array
    {
        $role = null;
        $roleId = null;
        foreach ($u->getUtilisateursRoles() as $ur) {
            $role   = $ur->getRole()->getNom();
            $roleId = $ur->getRole()->getId();
            break;
        }
        return [
            'id'              => $u->getId(),
            'nom'             => $u->getNom(),
            'prenom'          => $u->getPrenom(),
            'email'           => $u->getEmail(),
            'telephone'       => $u->getTelephone(),
            'estActif'        => $u->isEstActif(),
            'estListeNoire'   => $u->isEstListeNoire(),
            'motifListeNoire' => $u->getMotifListeNoire(),
            'serviceId'       => $u->getServiceId(),
            'typeUtilisateur' => $u->getTypeUtilisateur(),
            'role'            => $role,
            'roleId'          => $roleId,
            'derniereConnexion' => $u->getDerniereConnexion()?->format(\DateTime::ATOM),
        ];
    }
}
