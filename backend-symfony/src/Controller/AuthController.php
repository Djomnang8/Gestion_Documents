<?php

namespace App\Controller;

use App\Entity\Utilisateur;
use Doctrine\ORM\EntityManagerInterface;
use Lexik\Bundle\JWTAuthenticationBundle\Services\JWTTokenManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/auth')]
class AuthController extends AbstractController
{
    public function __construct(
        private EntityManagerInterface $em,
        private JWTTokenManagerInterface $jwtManager
    ) {}

    #[Route('/login', name: 'api_auth_login', methods: ['POST'])]
    public function login(Request $request): JsonResponse
    {
        $data = json_decode($request->getContent(), true);
        $email    = $data['email'] ?? '';
        $password = $data['motDePasse'] ?? '';

        /** @var Utilisateur|null $utilisateur */
        $utilisateur = $this->em->getRepository(Utilisateur::class)
            ->findOneBy(['email' => $email, 'estSupprime' => false]);

        if (!$utilisateur) {
            return $this->json(['message' => 'Email ou mot de passe incorrect.'], Response::HTTP_UNAUTHORIZED);
        }

        if (!$utilisateur->isEstActif()) {
            return $this->json(['message' => 'Compte désactivé. Contactez l\'administrateur.'], Response::HTTP_UNAUTHORIZED);
        }

        if ($utilisateur->isEstListeNoire()) {
            return $this->json(['message' => 'Accès bloqué. Contactez l\'administrateur.'], Response::HTTP_UNAUTHORIZED);
        }

        // Vérification mot de passe en clair (même logique que C#)
        if ($utilisateur->getMotDePasseHash() !== $password) {
            return $this->json(['message' => 'Email ou mot de passe incorrect.'], Response::HTTP_UNAUTHORIZED);
        }

        // Récupérer rôle et permissions
        $role = null;
        $permissions = [];
        foreach ($utilisateur->getUtilisateursRoles() as $ur) {
            $role = $ur->getRole();
            foreach ($ur->getRole()->getRolesPermissions() as $rp) {
                $perm = $rp->getPermission()->getNom();
                if (!in_array($perm, $permissions)) {
                    $permissions[] = $perm;
                }
            }
            break; // Premier rôle seulement
        }

        // Récupérer le nom du service
        $serviceNom = '';
        if ($utilisateur->getServiceId()) {
            $service = $this->em->getRepository(\App\Entity\Service::class)->find($utilisateur->getServiceId());
            $serviceNom = $service?->getNom() ?? '';
        }

        // Générer le token JWT avec les claims personnalisées
        $payload = [
            'sub'        => $utilisateur->getId(),
            'email'      => $utilisateur->getEmail(),
            'nom'        => $utilisateur->getNom(),
            'prenom'     => $utilisateur->getPrenom(),
            'role'       => $role?->getNom() ?? '',
            'serviceId'  => (string)($utilisateur->getServiceId() ?? ''),
            'serviceNom' => $serviceNom,
            'permission' => $permissions,
        ];

        $token = $this->jwtManager->createFromPayload($utilisateur, $payload);

        // Mettre à jour la dernière connexion
        $utilisateur->setDerniereConnexion(new \DateTime());
        $this->em->flush();

        return $this->json([
            'token' => $token,
            'user' => [
                'id'          => $utilisateur->getId(),
                'nom'         => $utilisateur->getNom(),
                'prenom'      => $utilisateur->getPrenom(),
                'email'       => $utilisateur->getEmail(),
                'role'        => $role?->getNom() ?? '',
                'permissions' => $permissions,
                'serviceId'   => $utilisateur->getServiceId(),
                'serviceNom'  => $serviceNom,
            ],
        ]);
    }
}
