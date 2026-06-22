<?php

namespace App\EventListener;

use App\Entity\Utilisateur;
use Doctrine\ORM\EntityManagerInterface;
use Lexik\Bundle\JWTAuthenticationBundle\Event\JWTCreatedEvent;

/**
 * Ajoute des claims personnalisées au token JWT :
 * nom, prenom, role, serviceId, serviceNom, permissions
 */
class JwtCreatedListener
{
    public function __construct(private EntityManagerInterface $em) {}

    public function onJwtCreated(JWTCreatedEvent $event): void
    {
        $user = $event->getUser();
        if (!$user instanceof Utilisateur) return;

        $payload = $event->getData();

        // Rôle et permissions
        $role = null;
        $permissions = [];
        foreach ($user->getUtilisateursRoles() as $ur) {
            $role = $ur->getRole()->getNom();
            foreach ($ur->getRole()->getRolesPermissions() as $rp) {
                $permissions[] = $rp->getPermission()->getNom();
            }
            break;
        }

        // Service
        $serviceNom = '';
        if ($user->getServiceId()) {
            $service = $this->em->getRepository(\App\Entity\Service::class)->find($user->getServiceId());
            $serviceNom = $service?->getNom() ?? '';
        }

        $payload['sub']        = $user->getId();
        $payload['nom']        = $user->getNom();
        $payload['prenom']     = $user->getPrenom();
        $payload['role']       = $role ?? '';
        $payload['serviceId']  = (string)($user->getServiceId() ?? '');
        $payload['serviceNom'] = $serviceNom;
        $payload['permission'] = array_values(array_unique($permissions));

        $event->setData($payload);
    }
}
