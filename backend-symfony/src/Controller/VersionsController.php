<?php

namespace App\Controller;

use App\Entity\Journal;
use App\Entity\Utilisateur;
use App\Entity\VersionDocument;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/versions')]
class VersionsController extends AbstractController
{
    public function __construct(private EntityManagerInterface $em) {}

    #[Route('/{dossierId}', name: 'api_versions_list', methods: ['GET'])]
    public function list(string $dossierId): JsonResponse
    {
        $versions = $this->em->createQueryBuilder()
            ->select('v')
            ->from(VersionDocument::class, 'v')
            ->where('v.dossier = :did')
            ->setParameter('did', $dossierId)
            ->orderBy('v.numeroVersion', 'DESC')
            ->getQuery()
            ->getResult();

        $utilisateurIds = array_filter(array_unique(array_map(fn($v) => $v->getUtilisateurId(), $versions)));
        $utilisateurs = [];
        if ($utilisateurIds) {
            $users = $this->em->createQueryBuilder()
                ->select('u')
                ->from(Utilisateur::class, 'u')
                ->where('u.id IN (:ids)')
                ->setParameter('ids', array_values($utilisateurIds))
                ->getQuery()->getResult();
            foreach ($users as $u) {
                $utilisateurs[$u->getId()] = $u->getPrenom().' '.$u->getNom();
            }
        }

        return $this->json(array_map(fn(VersionDocument $v) => [
            'id'            => $v->getId(),
            'dossierId'     => $v->getDossier()->getId(),
            'numero'        => $v->getNumeroVersion(),
            'nomFichier'    => $v->getNomFichier(),
            'tailleFichier' => $v->getTailleFichier() ?? 0,
            'typeFichier'   => $v->getTypeFichier() ?? '',
            'dateCreation'  => $v->getDateCreation()->format(\DateTime::ATOM),
            'estActive'     => $v->isEstActive(),
            'commentaire'   => $v->getCommentaire() ?? '',
            'auteur'        => $v->getUtilisateurId() ? ($utilisateurs[$v->getUtilisateurId()] ?? 'Système') : 'Système',
        ], $versions));
    }

    #[Route('/{versionId}/restaurer', name: 'api_versions_restaurer', methods: ['POST'])]
    public function restaurer(string $versionId): JsonResponse
    {
        $version = $this->em->getRepository(VersionDocument::class)->find($versionId);
        if (!$version) return $this->json(['error' => 'Version introuvable.'], 404);

        if ($version->isEstActive()) {
            return $this->json(['error' => 'Cette version est déjà la version active.'], 400);
        }

        $dossierId = $version->getDossier()->getId();

        // Désactiver toutes les versions
        $this->em->createQuery(
            "UPDATE ".VersionDocument::class." v SET v.estActive = false WHERE v.dossier = :did"
        )->setParameter('did', $dossierId)->execute();

        $version->setEstActive(true);
        $this->em->flush();

        // Journal
        $token = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $userId = $payload['sub'] ?? null;

        if ($userId) {
            $j = new Journal();
            $j->setUtilisateurId($userId)
              ->setModule('Archivage')
              ->setAction('RESTAURATION_VERSION')
              ->setEntiteId($versionId)
              ->setDetails("Restauré version {$version->getNumeroVersion()} du dossier {$version->getDossier()->getNumero()}")
              ->setDateAction(new \DateTime())
              ->setNiveauId(1);
            $this->em->persist($j);
            $this->em->flush();
        }

        return $this->json(['message' => "Version {$version->getNumeroVersion()} restaurée avec succès."]);
    }
}
