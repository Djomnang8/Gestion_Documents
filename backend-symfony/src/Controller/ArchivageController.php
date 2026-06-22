<?php

namespace App\Controller;

use App\Entity\Dossier;
use App\Entity\HistoriqueStatut;
use App\Entity\Journal;
use App\Entity\StatutDossier;
use App\Entity\VersionDocument;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/archivage')]
class ArchivageController extends AbstractController
{
    public function __construct(private EntityManagerInterface $em) {}

    #[Route('/kpi', name: 'api_archivage_kpi', methods: ['GET'])]
    public function kpi(): JsonResponse
    {
        $debutMois = new \DateTime('first day of this month midnight');

        $aArchiver = (int)$this->em->createQuery(
            "SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'TERMINE'"
        )->getSingleScalarResult();

        $archivesCeMois = (int)$this->em->createQuery(
            "SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'ARCHIVE' AND d.dateArchivage >= :debut"
        )->setParameter('debut', $debutMois)->getSingleScalarResult();

        $totalArchives = (int)$this->em->createQuery(
            "SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'ARCHIVE'"
        )->getSingleScalarResult();

        return $this->json(['aArchiver' => $aArchiver, 'archivesCeMois' => $archivesCeMois, 'totalArchives' => $totalArchives]);
    }

    #[Route('/a-archiver', name: 'api_archivage_a_archiver', methods: ['GET'])]
    public function dossiersAAArchiver(): JsonResponse
    {
        $dossiers = $this->em->createQueryBuilder()
            ->select('d', 's', 'srv')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->join('d.service', 'srv')
            ->where("s.code = 'TERMINE'")
            ->orderBy('d.dateMiseAJourStatut', 'DESC')
            ->getQuery()
            ->getResult();

        return $this->json(array_map(fn(Dossier $d) => [
            'id'      => $d->getId(),
            'numero'  => $d->getNumero(),
            'titre'   => $d->getTitre(),
            'citoyen' => $d->getNomCitoyen(),
            'email'   => $d->getEmailCitoyen() ?? '',
            'dateFin' => $d->getDateMiseAJourStatut()->format(\DateTime::ATOM),
            'service' => $d->getService()->getNom(),
        ], $dossiers));
    }

    /**
     * Arbre : Service → Citoyen (email+nom) → Versions (chaque version = un dossier archivé + ses fichiers).
     * L'identification du citoyen se fait par email ET nom ensemble.
     */
    #[Route('/historique-versions', name: 'api_archivage_historique_versions', methods: ['GET'])]
    public function historiqueVersions(): JsonResponse
    {
        $dossiers = $this->em->createQueryBuilder()
            ->select('d', 's', 'srv', 'v')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->join('d.service', 'srv')
            ->leftJoin('d.versionsDocument', 'v')
            ->where("s.code = 'ARCHIVE'")
            ->orderBy('srv.nom', 'ASC')
            ->addOrderBy('d.nomCitoyen', 'ASC')
            ->addOrderBy('d.numeroVersionArchive', 'DESC')
            ->getQuery()
            ->getResult();

        $grouped = [];
        foreach ($dossiers as $d) {
            $nomService = $d->getService()->getNom();
            // Identification citoyen : email + nom
            $cleCitoyen = ($d->getEmailCitoyen() ?? '') . '||' . $d->getNomCitoyen();

            if (!isset($grouped[$nomService])) {
                $grouped[$nomService] = ['nomService' => $nomService, 'citoyens' => []];
            }
            if (!isset($grouped[$nomService]['citoyens'][$cleCitoyen])) {
                $grouped[$nomService]['citoyens'][$cleCitoyen] = [
                    'nomCitoyen'     => $d->getNomCitoyen(),
                    'emailCitoyen'   => $d->getEmailCitoyen() ?? '',
                    'groupeArchiveId' => $d->getGroupeArchiveId(),
                    'versions'       => [],
                ];
            }

            $fichiers = array_map(fn(VersionDocument $v) => [
                'id'            => $v->getId(),
                'nomFichier'    => $v->getNomFichier(),
                'cheminFichier' => $v->getCheminFichier(),
                'typeFichier'   => $v->getTypeFichier() ?? '',
                'tailleFichier' => $v->getTailleFichier() ?? 0,
            ], $d->getVersionsDocument()->toArray());

            $grouped[$nomService]['citoyens'][$cleCitoyen]['versions'][] = [
                'numero'        => $d->getNumeroVersionArchive(),
                'dossierId'     => $d->getId(),
                'dossierNumero' => $d->getNumero(),
                'dossierTitre'  => $d->getTitre(),
                'dateArchivage' => $d->getDateArchivage()?->format(\DateTime::ATOM),
                'estActive'     => $d->isEstVersionActive(),
                'fichiers'      => $fichiers,
            ];
        }

        $result = [];
        foreach ($grouped as $srv) {
            $srv['citoyens'] = array_values($srv['citoyens']);
            $result[] = $srv;
        }

        return $this->json($result);
    }

    /**
     * Archives consultables : seulement la version active par citoyen (email+nom) par service.
     * Après "Restaurer une Version", le dossier restauré devient la version active et apparaît ici.
     */
    #[Route('/archives-consultables', name: 'api_archivage_archives_consultables', methods: ['GET'])]
    public function archivesConsultables(): JsonResponse
    {
        $dossiers = $this->em->createQueryBuilder()
            ->select('d', 's', 'srv', 'v')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->join('d.service', 'srv')
            ->leftJoin('d.versionsDocument', 'v')
            ->where("s.code = 'ARCHIVE'")
            ->andWhere('d.estVersionActive = true')
            ->orderBy('srv.nom', 'ASC')
            ->addOrderBy('d.nomCitoyen', 'ASC')
            ->getQuery()
            ->getResult();

        $grouped = [];
        foreach ($dossiers as $d) {
            $nomService = $d->getService()->getNom();
            // Identification citoyen : email + nom
            $cleCitoyen = ($d->getEmailCitoyen() ?? '') . '||' . $d->getNomCitoyen();

            if (!isset($grouped[$nomService])) {
                $grouped[$nomService] = ['nomService' => $nomService, 'citoyens' => []];
            }

            $fichiers = array_map(fn(VersionDocument $v) => [
                'id'            => $v->getId(),
                'nomFichier'    => $v->getNomFichier(),
                'cheminFichier' => $v->getCheminFichier(),
                'typeFichier'   => $v->getTypeFichier() ?? '',
                'tailleFichier' => $v->getTailleFichier() ?? 0,
            ], $d->getVersionsDocument()->toArray());

            $grouped[$nomService]['citoyens'][$cleCitoyen] = [
                'nomCitoyen'         => $d->getNomCitoyen(),
                'emailCitoyen'       => $d->getEmailCitoyen() ?? '',
                'groupeArchiveId'    => $d->getGroupeArchiveId(),
                'dossierId'          => $d->getId(),
                'dossierNumero'      => $d->getNumero(),
                'dossierTitre'       => $d->getTitre(),
                'dateArchivage'      => $d->getDateArchivage()?->format(\DateTime::ATOM),
                'numeroVersionActive' => $d->getNumeroVersionArchive(),
                'fichiers'           => $fichiers,
            ];
        }

        $result = [];
        foreach ($grouped as $srv) {
            $srv['citoyens'] = array_values($srv['citoyens']);
            $result[] = $srv;
        }

        return $this->json($result);
    }

    /**
     * Archive un dossier TERMINE.
     * Identifie le citoyen par email+nom pour incrémenter le numéro de version correctement.
     * Après archivage, le dossier disparaît de l'espace Agent (statut ARCHIVE).
     */
    #[Route('/{id}', name: 'api_archivage_archiver', methods: ['POST'], requirements: ['id' => '[0-9a-f\-]{36}'])]
    public function archiver(string $id, Request $request): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->find($id);
        if (!$dossier) {
            return $this->json(['error' => 'Dossier introuvable.'], 404);
        }

        if ($dossier->getStatut()->getCode() !== 'TERMINE') {
            return $this->json(['error' => "Seuls les dossiers 'TERMINÉ' peuvent être archivés."], 400);
        }

        $statutArchive = $this->em->getRepository(StatutDossier::class)->findOneBy(['code' => 'ARCHIVE']);
        if (!$statutArchive) {
            return $this->json(['error' => "Statut 'ARCHIVE' non défini."], 500);
        }

        $ancienStatut = $dossier->getStatut();
        $dossier->setStatut($statutArchive);
        $dossier->setDateArchivage(new \DateTime());
        $dossier->setDateMiseAJourStatut(new \DateTime());

        // Recherche le groupe existant pour ce citoyen (email + nom) dans le même service
        $ancienneVersion = $this->em->createQuery(
            "SELECT d FROM ".Dossier::class." d JOIN d.statut s
             WHERE s.code = 'ARCHIVE'
               AND d.emailCitoyen = :email
               AND d.nomCitoyen = :nom
               AND d.service = :sid
             ORDER BY d.numeroVersionArchive DESC"
        )->setParameters([
            'email' => $dossier->getEmailCitoyen(),
            'nom'   => $dossier->getNomCitoyen(),
            'sid'   => $dossier->getService(),
        ])->setMaxResults(1)->getOneOrNullResult();

        if ($ancienneVersion) {
            $groupeId = $ancienneVersion->getGroupeArchiveId() ?? $ancienneVersion->getId();
            $dossier->setGroupeArchiveId($groupeId);
            $dossier->setNumeroVersionArchive($ancienneVersion->getNumeroVersionArchive() + 1);
        } else {
            $dossier->setGroupeArchiveId($dossier->getId());
            $dossier->setNumeroVersionArchive(1);
        }

        // Le dossier nouvellement archivé devient la version active
        $dossier->setEstVersionActive(true);

        // Désactiver l'ancienne version active du groupe
        if ($dossier->getGroupeArchiveId()) {
            $this->em->createQuery(
                "UPDATE ".Dossier::class." d SET d.estVersionActive = false
                 WHERE d.groupeArchiveId = :gid AND d.id != :id"
            )->setParameters(['gid' => $dossier->getGroupeArchiveId(), 'id' => $dossier->getId()])->execute();
        }

        $token   = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $agentId = $payload['sub'] ?? null;

        $h = new HistoriqueStatut();
        $h->setDossier($dossier)
          ->setAncienStatut($ancienStatut)
          ->setNouveauStatut($statutArchive)
          ->setCommentaire("Dossier archivé (Version {$dossier->getNumeroVersionArchive()})")
          ->setDateChangement(new \DateTime())
          ->setAgentId($agentId);
        $this->em->persist($h);

        if ($agentId) {
            $j = new Journal();
            $j->setUtilisateurId($agentId)->setModule('Archivage')->setAction('ARCHIVER')
              ->setDetails("Dossier {$dossier->getNumero()} archivé — Version {$dossier->getNumeroVersionArchive()} — Citoyen : {$dossier->getNomCitoyen()} ({$dossier->getEmailCitoyen()})")
              ->setEntiteId($dossier->getId())->setDateAction(new \DateTime())->setNiveauId(1);
            $this->em->persist($j);
        }

        $this->em->flush();

        return $this->json([
            'message'         => "Dossier archivé — Version {$dossier->getNumeroVersionArchive()} créée.",
            'versionArchive'  => $dossier->getNumeroVersionArchive(),
            'groupeArchiveId' => $dossier->getGroupeArchiveId(),
        ]);
    }

    /**
     * Restaure une version archivée comme version active.
     * Les fichiers de la version restaurée apparaissent alors dans Archives Consultables.
     */
    #[Route('/{dossierId}/restaurer-version', name: 'api_archivage_restaurer_version', methods: ['POST'])]
    public function restaurerVersionArchive(string $dossierId): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->find($dossierId);
        if (!$dossier) {
            return $this->json(['error' => 'Dossier introuvable.'], 404);
        }

        if ($dossier->getStatut()->getCode() !== 'ARCHIVE') {
            return $this->json(['error' => 'Seuls les dossiers archivés peuvent être restaurés.'], 400);
        }

        if ($dossier->isEstVersionActive()) {
            return $this->json(['error' => 'Cette version est déjà la version active.'], 400);
        }

        $groupeId = $dossier->getGroupeArchiveId();
        if (!$groupeId) {
            return $this->json(['error' => "Ce dossier n'appartient à aucun groupe d'archive."], 400);
        }

        // Désactiver toutes les versions du groupe
        $this->em->createQuery(
            "UPDATE ".Dossier::class." d SET d.estVersionActive = false WHERE d.groupeArchiveId = :gid"
        )->setParameter('gid', $groupeId)->execute();

        // Activer la version demandée
        $dossier->setEstVersionActive(true);

        $token   = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $agentId = $payload['sub'] ?? null;

        if ($agentId) {
            $j = new Journal();
            $j->setUtilisateurId($agentId)->setModule('Archivage')->setAction('RESTAURER_VERSION')
              ->setDetails("Version {$dossier->getNumeroVersionArchive()} restaurée — Dossier {$dossier->getNumero()} — Citoyen : {$dossier->getNomCitoyen()}")
              ->setEntiteId($dossier->getId())->setDateAction(new \DateTime())->setNiveauId(1);
            $this->em->persist($j);
        }

        $this->em->flush();

        return $this->json([
            'message' => "Version {$dossier->getNumeroVersionArchive()} restaurée. Les fichiers sont maintenant visibles dans Archives Consultables.",
        ]);
    }

    #[Route('/versions/{groupeId}', name: 'api_archivage_versions', methods: ['GET'])]
    public function versionsArchive(string $groupeId): JsonResponse
    {
        $versions = $this->em->createQueryBuilder()
            ->select('d', 's', 'srv', 'v')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->join('d.service', 'srv')
            ->leftJoin('d.versionsDocument', 'v')
            ->where('d.groupeArchiveId = :gid')
            ->setParameter('gid', $groupeId)
            ->orderBy('d.numeroVersionArchive', 'DESC')
            ->getQuery()
            ->getResult();

        return $this->json(array_map(fn(Dossier $d) => [
            'id'                   => $d->getId(),
            'numero'               => $d->getNumero(),
            'titre'                => $d->getTitre(),
            'citoyen'              => $d->getNomCitoyen(),
            'service'              => $d->getService()->getNom(),
            'dateArchivage'        => $d->getDateArchivage()?->format(\DateTime::ATOM),
            'numeroVersionArchive' => $d->getNumeroVersionArchive(),
            'estVersionActive'     => $d->isEstVersionActive(),
            'fichiers'             => array_map(fn(VersionDocument $v) => [
                'id'            => $v->getId(),
                'nomFichier'    => $v->getNomFichier(),
                'cheminFichier' => $v->getCheminFichier(),
                'typeFichier'   => $v->getTypeFichier() ?? '',
                'tailleFichier' => $v->getTailleFichier() ?? 0,
            ], $d->getVersionsDocument()->toArray()),
        ], $versions));
    }
}
