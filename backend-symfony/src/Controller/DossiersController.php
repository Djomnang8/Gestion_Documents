<?php

namespace App\Controller;

use App\Entity\Dossier;
use App\Entity\HistoriqueStatut;
use App\Entity\Service;
use App\Entity\StatutDossier;
use App\Entity\VersionDocument;
use App\Service\EmailService;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\BinaryFileResponse;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpFoundation\ResponseHeaderBag;
use Symfony\Component\Routing\Annotation\Route;
use Symfony\Component\Security\Http\Attribute\IsGranted;

#[Route('/api/dossiers')]
class DossiersController extends AbstractController
{
    public function __construct(
        private EntityManagerInterface $em,
        private string $uploadDir,
        private EmailService $emailService
    ) {}

    private function getServiceIdAgentConnecte(): ?int
    {
        $token = $this->container->get('security.token_storage')->getToken();
        if (!$token) return null;

        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $role = $payload['role'] ?? '';
        if ($role !== 'Agent') return null;

        $userId = $payload['sub'] ?? null;
        if (!$userId) return null;

        $utilisateur = $this->em->getRepository(\App\Entity\Utilisateur::class)->find($userId);
        return $utilisateur?->getServiceId();
    }

    #[Route('', name: 'api_dossiers_list', methods: ['GET'])]
    public function list(Request $request): JsonResponse
    {
        $statut    = $request->query->get('statut');
        $recherche = $request->query->get('recherche');
        $page      = max(1, (int)$request->query->get('page', 1));
        $taille    = max(1, (int)$request->query->get('taille', 20));
        $serviceId = $request->query->get('serviceId');
        $dateDebut = $request->query->get('dateDebut');
        $dateFin   = $request->query->get('dateFin');

        $qb = $this->em->createQueryBuilder()
            ->select('d', 's')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->where("s.code != 'ARCHIVE'");

        $serviceAgent = $this->getServiceIdAgentConnecte();
        if ($serviceAgent !== null) {
            $qb->andWhere('d.service = :sid')->setParameter('sid', $serviceAgent);
        } elseif ($serviceId) {
            $qb->andWhere('d.service = :sid')->setParameter('sid', (int)$serviceId);
        }

        if ($statut) {
            $qb->andWhere('s.code = :statut')->setParameter('statut', $statut);
        }
        if ($recherche) {
            $qb->andWhere('d.numero LIKE :r OR d.titre LIKE :r OR d.nomCitoyen LIKE :r')
               ->setParameter('r', '%'.$recherche.'%');
        }
        if ($dateDebut) {
            $qb->andWhere('d.dateDepot >= :dd')->setParameter('dd', new \DateTime($dateDebut));
        }
        if ($dateFin) {
            $qb->andWhere('d.dateDepot <= :df')->setParameter('df', new \DateTime($dateFin));
        }

        $total = (clone $qb)->select('COUNT(d.id)')->getQuery()->getSingleScalarResult();

        $dossiers = $qb->select('d', 's')
            ->orderBy('d.dateDepot', 'DESC')
            ->setFirstResult(($page - 1) * $taille)
            ->setMaxResults($taille)
            ->getQuery()
            ->getResult();

        return $this->json([
            'total'    => (int)$total,
            'page'     => $page,
            'taille'   => $taille,
            'dossiers' => array_map(fn(Dossier $d) => [
                'id'                  => $d->getId(),
                'numero'              => $d->getNumero(),
                'titre'               => $d->getTitre(),
                'nomCitoyen'          => $d->getNomCitoyen(),
                'emailCitoyen'        => $d->getEmailCitoyen(),
                'telephoneCitoyen'    => $d->getTelephoneCitoyen(),
                'statutCode'          => $d->getStatut()->getCode(),
                'statutLibelle'       => $d->getStatut()->getLibelle(),
                'dateDepot'           => $d->getDateDepot()->format(\DateTime::ATOM),
                'dateMiseAJourStatut' => $d->getDateMiseAJourStatut()->format(\DateTime::ATOM),
            ], $dossiers),
        ]);
    }

    #[Route('/stats', name: 'api_dossiers_stats', methods: ['GET'])]
    public function stats(): JsonResponse
    {
        $aujourd    = new \DateTime('today');
        $debutSem   = new \DateTime('monday this week');
        $seuilRetard = new \DateTime('-7 days');

        $serviceAgent = $this->getServiceIdAgentConnecte();
        $extra = $serviceAgent ? ' AND d.service = :sid' : '';
        $params = $serviceAgent ? ['sid' => $serviceAgent] : [];

        $parStatut = $this->em->createQuery(
            "SELECT s.code, COUNT(d.id) as cnt FROM ".Dossier::class." d JOIN d.statut s WHERE 1=1 $extra GROUP BY s.code"
        )->setParameters($params)->getResult();

        $map = array_column($parStatut, 'cnt', 'code');

        $base = $this->em->createQueryBuilder()->from(Dossier::class, 'd')->join('d.statut', 's');
        $count = fn($extra2, $p2 = []) => (int)$base->select('COUNT(d.id)')->where("s.code != 'ARCHIVE' $extra2")->setParameters(array_merge($params, $p2))->getQuery()->getSingleScalarResult();

        return $this->json([
            'total'          => (int)array_sum(array_filter(array_values($map), fn($v) => true)),
            'recusAujourdhui' => (int)($this->em->createQuery("SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code != 'ARCHIVE' AND d.dateDepot >= :a")->setParameter('a', $aujourd)->getSingleScalarResult()),
            'traitesSemine'  => (int)($this->em->createQuery("SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'TERMINE' AND d.dateMiseAJourStatut >= :ds")->setParameter('ds', $debutSem)->getSingleScalarResult()),
            'enRetard'       => (int)($this->em->createQuery("SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code IN ('RECU','EN_COURS') AND d.dateDepot <= :sr")->setParameter('sr', $seuilRetard)->getSingleScalarResult()),
            'recu'           => (int)($map['RECU'] ?? 0),
            'enCours'        => (int)($map['EN_COURS'] ?? 0),
            'transfere'      => (int)($map['TRANSFERE'] ?? 0),
            'rejete'         => (int)($map['REJETE'] ?? 0),
            'termine'        => (int)($map['TERMINE'] ?? 0),
            'archive'        => (int)($map['ARCHIVE'] ?? 0),
        ]);
    }

    #[Route('/en-retard', name: 'api_dossiers_en_retard', methods: ['GET'])]
    public function enRetard(): JsonResponse
    {
        $seuil = new \DateTime('-7 days');
        $serviceAgent = $this->getServiceIdAgentConnecte();

        $qb = $this->em->createQueryBuilder()
            ->select('d', 's')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->where("s.code IN ('RECU','EN_COURS')")
            ->andWhere('d.dateDepot <= :seuil')
            ->setParameter('seuil', $seuil)
            ->orderBy('d.dateDepot', 'ASC');

        if ($serviceAgent !== null) {
            $qb->andWhere('d.service = :sid')->setParameter('sid', $serviceAgent);
        }

        $dossiers = $qb->getQuery()->getResult();
        $now = new \DateTime();

        return $this->json(array_map(fn(Dossier $d) => [
            'id'                  => $d->getId(),
            'numero'              => $d->getNumero(),
            'titre'               => $d->getTitre(),
            'nomCitoyen'          => $d->getNomCitoyen(),
            'statutCode'          => $d->getStatut()->getCode(),
            'statutLibelle'       => $d->getStatut()->getLibelle(),
            'dateDepot'           => $d->getDateDepot()->format(\DateTime::ATOM),
            'dateMiseAJourStatut' => $d->getDateMiseAJourStatut()->format(\DateTime::ATOM),
            'joursEnRetard'       => (int)$d->getDateDepot()->diff($now)->days,
        ], $dossiers));
    }

    #[Route('/export-csv', name: 'api_dossiers_export_csv', methods: ['GET'])]
    public function exportCsv(Request $request): Response
    {
        $statut    = $request->query->get('statut');
        $recherche = $request->query->get('recherche');
        $serviceId = $request->query->get('serviceId');
        $dateDebut = $request->query->get('dateDebut');
        $dateFin   = $request->query->get('dateFin');

        $qb = $this->em->createQueryBuilder()
            ->select('d', 's')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->orderBy('d.dateDepot', 'DESC');

        $serviceAgent = $this->getServiceIdAgentConnecte();
        if ($serviceAgent !== null) {
            $qb->andWhere('d.service = :sid')->setParameter('sid', $serviceAgent);
        } elseif ($serviceId) {
            $qb->andWhere('d.service = :sid')->setParameter('sid', (int)$serviceId);
        }
        if ($statut) { $qb->andWhere('s.code = :s')->setParameter('s', $statut); }
        if ($recherche) { $qb->andWhere('d.numero LIKE :r OR d.nomCitoyen LIKE :r')->setParameter('r', '%'.$recherche.'%'); }
        if ($dateDebut) { $qb->andWhere('d.dateDepot >= :dd')->setParameter('dd', new \DateTime($dateDebut)); }
        if ($dateFin)   { $qb->andWhere('d.dateDepot <= :df')->setParameter('df', new \DateTime($dateFin)); }

        $dossiers = $qb->getQuery()->getResult();

        $csv = "Numero;Titre;Citoyen;Email;Téléphone;Statut;Date Dépôt;Dernière MAJ\n";
        foreach ($dossiers as $d) {
            $csv .= sprintf("%s;%s;%s;%s;%s;%s;%s;%s\n",
                $d->getNumero(), $d->getTitre(), $d->getNomCitoyen(),
                $d->getEmailCitoyen() ?? '', $d->getTelephoneCitoyen() ?? '',
                $d->getStatut()->getCode(),
                $d->getDateDepot()->format('Y-m-d'),
                $d->getDateMiseAJourStatut()->format('Y-m-d')
            );
        }

        $response = new Response($csv);
        $response->headers->set('Content-Type', 'text/csv; charset=utf-8');
        $response->headers->set('Content-Disposition', 'attachment; filename="dossiers_'.date('Ymd').'.csv"');
        return $response;
    }

    #[Route('/archives', name: 'api_dossiers_archives', methods: ['GET'])]
    public function archives(Request $request): JsonResponse
    {
        $numero    = $request->query->get('numero');
        $dateDebut = $request->query->get('dateDebut');
        $dateFin   = $request->query->get('dateFin');
        $page      = max(1, (int)$request->query->get('page', 1));
        $size      = max(1, (int)$request->query->get('size', 12));

        $qb = $this->em->createQueryBuilder()
            ->select('d', 's', 'srv')
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->join('d.service', 'srv')
            ->where("s.code = 'ARCHIVE'");

        if ($numero) { $qb->andWhere('d.numero LIKE :n OR d.nomCitoyen LIKE :n')->setParameter('n', '%'.$numero.'%'); }
        if ($dateDebut) { $qb->andWhere('d.dateArchivage >= :dd')->setParameter('dd', new \DateTime($dateDebut)); }
        if ($dateFin)   { $qb->andWhere('d.dateArchivage <= :df')->setParameter('df', new \DateTime($dateFin)); }

        $total = (clone $qb)->select('COUNT(d.id)')->getQuery()->getSingleScalarResult();

        $dossiers = $qb->select('d', 's', 'srv')
            ->orderBy('srv.nom', 'ASC')
            ->addOrderBy('d.emailCitoyen', 'ASC')
            ->addOrderBy('d.dateArchivage', 'DESC')
            ->setFirstResult(($page - 1) * $size)
            ->setMaxResults($size)
            ->getQuery()
            ->getResult();

        return $this->json([
            'total' => (int)$total,
            'page'  => $page,
            'size'  => $size,
            'data'  => array_map(fn(Dossier $d) => [
                'id'            => $d->getId(),
                'numero'        => $d->getNumero(),
                'titre'         => $d->getTitre(),
                'citoyen'       => $d->getNomCitoyen(),
                'emailCitoyen'  => $d->getEmailCitoyen(),
                'service'       => $d->getService()->getNom(),
                'dateArchivage' => ($d->getDateArchivage() ?? $d->getDateMiseAJourStatut())->format(\DateTime::ATOM),
                'nbDocuments'   => count($d->getVersionsDocument()),
                'miniature'     => '',
            ], $dossiers),
        ]);
    }

    #[Route('/suivi/{numero}', name: 'api_dossiers_suivi', methods: ['GET'])]
    public function suivi(string $numero): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->findOneBy(['numero' => $numero]);
        if (!$dossier) {
            return $this->json(['message' => "Aucun dossier trouvé avec le numéro $numero."], 404);
        }

        $historique = array_map(fn(HistoriqueStatut $h) => [
            'ancienStatut'  => $h->getAncienStatut()?->getLibelle() ?? 'Création',
            'nouveauStatut' => $h->getNouveauStatut()->getLibelle(),
            'commentaire'   => $h->getCommentaire(),
            'date'          => $h->getDateChangement()->format(\DateTime::ATOM),
        ], array_reverse($dossier->getHistoriqueStatuts()->toArray()));

        return $this->json([
            'id'                  => $dossier->getId(),
            'numero'              => $dossier->getNumero(),
            'titre'               => $dossier->getTitre(),
            'description'         => $dossier->getDescription(),
            'nomCitoyen'          => $dossier->getNomCitoyen(),
            'service'             => $dossier->getService()->getNom(),
            'statutCode'          => $dossier->getStatut()->getCode(),
            'statutLibelle'       => $dossier->getStatut()->getLibelle(),
            'dateDepot'           => $dossier->getDateDepot()->format(\DateTime::ATOM),
            'dateMiseAJourStatut' => $dossier->getDateMiseAJourStatut()->format(\DateTime::ATOM),
            'motifRejet'          => $dossier->getMotifRejet(),
            'historique'          => $historique,
        ]);
    }

    #[Route('/{id}', name: 'api_dossiers_get', methods: ['GET'])]
    public function get(string $id): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->find($id);
        if (!$dossier) {
            return $this->json(['error' => 'Dossier introuvable.'], 404);
        }

        $historique = array_map(fn(HistoriqueStatut $h) => [
            'ancienStatut'  => $h->getAncienStatut()?->getLibelle() ?? '—',
            'nouveauStatut' => $h->getNouveauStatut()->getLibelle(),
            'commentaire'   => $h->getCommentaire(),
            'dateChangement' => $h->getDateChangement()->format(\DateTime::ATOM),
        ], array_reverse($dossier->getHistoriqueStatuts()->toArray()));

        $documents = array_map(fn(VersionDocument $v) => [
            'id'           => $v->getId(),
            'nomFichier'   => $v->getNomFichier(),
            'cheminFichier' => $v->getCheminFichier(),
            'typeFichier'  => $v->getTypeFichier() ?? '',
            'tailleFichier' => $v->getTailleFichier() ?? 0,
            'numeroVersion' => $v->getNumeroVersion(),
            'dateCreation' => $v->getDateCreation()->format(\DateTime::ATOM),
        ], $dossier->getVersionsDocument()->toArray());

        return $this->json([
            'id'                  => $dossier->getId(),
            'numero'              => $dossier->getNumero(),
            'titre'               => $dossier->getTitre(),
            'description'         => $dossier->getDescription(),
            'nomCitoyen'          => $dossier->getNomCitoyen(),
            'emailCitoyen'        => $dossier->getEmailCitoyen(),
            'telephoneCitoyen'    => $dossier->getTelephoneCitoyen(),
            'motifRejet'          => $dossier->getMotifRejet(),
            'statutCode'          => $dossier->getStatut()->getCode(),
            'statutLibelle'       => $dossier->getStatut()->getLibelle(),
            'serviceNom'          => $dossier->getService()->getNom(),
            'dateDepot'           => $dossier->getDateDepot()->format(\DateTime::ATOM),
            'dateMiseAJourStatut' => $dossier->getDateMiseAJourStatut()->format(\DateTime::ATOM),
            'historique'          => $historique,
            'documents'           => $documents,
        ]);
    }

    #[Route('', name: 'api_dossiers_create', methods: ['POST'])]
    public function create(Request $request): JsonResponse
    {
        $data = json_decode($request->getContent(), true);

        $statutRecu = $this->em->getRepository(StatutDossier::class)->findOneBy(['code' => 'RECU']);
        if (!$statutRecu) {
            return $this->json(['error' => "Statut 'RECU' non défini."], 400);
        }

        $token = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $agentId = $payload['sub'] ?? null;

        $count = (int)$this->em->createQuery("SELECT COUNT(d.id) FROM ".Dossier::class." d")->getSingleScalarResult();
        $numero = "DOS-".date('Y')."-".str_pad($count + 1, 5, '0', STR_PAD_LEFT);

        $service = $this->em->getRepository(Service::class)->find($data['serviceId'] ?? 0);
        if (!$service) {
            return $this->json(['error' => 'Service invalide.'], 400);
        }

        $dossier = new Dossier();
        $dossier->setNumero($numero)
            ->setTitre($data['titre'] ?? '')
            ->setDescription($data['description'] ?? null)
            ->setNomCitoyen($data['nomCitoyen'] ?? '')
            ->setEmailCitoyen($data['emailCitoyen'] ?? null)
            ->setTelephoneCitoyen($data['telephoneCitoyen'] ?? null)
            ->setStatut($statutRecu)
            ->setService($service)
            ->setAgentId($agentId)
            ->setDateDepot(new \DateTime())
            ->setDateMiseAJourStatut(new \DateTime());

        $this->em->persist($dossier);
        $this->em->flush();

        return $this->json(['id' => $dossier->getId(), 'numero' => $dossier->getNumero()]);
    }

    #[Route('/{id}/statut', name: 'api_dossiers_statut', methods: ['PATCH'])]
    public function changerStatut(string $id, Request $request): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->find($id);
        if (!$dossier) {
            return $this->json(['error' => 'Dossier introuvable.'], 404);
        }

        $data = json_decode($request->getContent(), true);
        $nouveauCode = $data['nouveauStatutCode'] ?? '';
        $commentaire = $data['commentaire'] ?? null;

        if ($nouveauCode === 'REJETE' && empty(trim($commentaire ?? ''))) {
            return $this->json(['error' => 'Un motif de rejet est obligatoire.'], 400);
        }

        $nouveauStatut = $this->em->getRepository(StatutDossier::class)->findOneBy(['code' => $nouveauCode]);
        if (!$nouveauStatut) {
            return $this->json(['error' => "Statut '$nouveauCode' inexistant."], 400);
        }

        $ancienStatut = $dossier->getStatut();

        if ($nouveauCode === 'REJETE') {
            $dossier->setMotifRejet($commentaire);
            if ($dossier->getEmailCitoyen()) {
                $this->emailService->sendRejetEmail(
                    $dossier->getEmailCitoyen(),
                    $dossier->getNomCitoyen(),
                    $dossier->getNumero(),
                    $commentaire ?? '',
                    $dossier->getService()->getNom()
                );
            }
        }

        $token = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $agentId = $payload['sub'] ?? null;

        $dossier->setStatut($nouveauStatut);
        $dossier->setDateMiseAJourStatut(new \DateTime());

        $historique = new HistoriqueStatut();
        $historique->setDossier($dossier)
            ->setAncienStatut($ancienStatut)
            ->setNouveauStatut($nouveauStatut)
            ->setCommentaire($commentaire)
            ->setDateChangement(new \DateTime())
            ->setAgentId($agentId);

        $this->em->persist($historique);
        $this->em->flush();

        return $this->json(['message' => 'Statut mis à jour.', 'statut' => $nouveauStatut->getLibelle()]);
    }

    #[Route('/{id}/documents', name: 'api_dossiers_upload', methods: ['POST'])]
    public function uploadDocument(string $id, Request $request): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->find($id);
        if (!$dossier) {
            return $this->json(['error' => 'Dossier introuvable.'], 404);
        }

        $fichier = $request->files->get('fichier');
        if (!$fichier) {
            return $this->json(['error' => 'Aucun fichier fourni.'], 400);
        }

        $slug = fn(string $s): string => preg_replace('/[^a-z0-9._-]/i', '_', $s);
        $dossierPath = $this->uploadDir
            . '/' . $slug($dossier->getService()->getNom())
            . '/' . $slug($dossier->getNomCitoyen()) . '_' . $slug($dossier->getEmailCitoyen() ?? 'inconnu');
        if (!is_dir($dossierPath)) { mkdir($dossierPath, 0777, true); }

        // Capture before move — temp file is gone after move()
        $mimeType    = $fichier->getMimeType() ?? 'application/octet-stream';
        $taille      = $fichier->getSize();
        $nomOriginal = $fichier->getClientOriginalName();

        $nomFichier  = uniqid('', true).'_'.$nomOriginal;
        $fichier->move($dossierPath, $nomFichier);
        $cheminFinal = $dossierPath.'/'.$nomFichier;

        $maxVersion = 0;
        foreach ($dossier->getVersionsDocument() as $v) {
            $this->em->createQuery("UPDATE ".VersionDocument::class." v SET v.estActive = false WHERE v.dossier = :d")->setParameter('d', $dossier)->execute();
            $maxVersion = max($maxVersion, $v->getNumeroVersion());
        }

        $token = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $agentId = $payload['sub'] ?? null;

        $version = new VersionDocument();
        $version->setDossier($dossier)
            ->setNumeroVersion($maxVersion + 1)
            ->setNomFichier($nomOriginal)
            ->setCheminFichier($cheminFinal)
            ->setTypeFichier($mimeType)
            ->setTailleFichier($taille)
            ->setDateCreation(new \DateTime())
            ->setEstActive(true)
            ->setUtilisateurId($agentId);

        $this->em->persist($version);
        $this->em->flush();

        return $this->json(['id' => $maxVersion + 1, 'nomFichier' => $nomOriginal]);
    }

    #[Route('/public/depot', name: 'api_dossiers_depot_public', methods: ['POST'])]
    public function depotPublic(Request $request): JsonResponse
    {
        $serviceId = (int)$request->request->get('serviceId', 0);
        $service = $this->em->getRepository(Service::class)->find($serviceId);
        if (!$service) { return $this->json(['message' => 'Service invalide.'], 400); }

        $fichiers = $request->files->get('fichiers') ?? [];
        if (!is_array($fichiers)) $fichiers = [$fichiers];
        $fichiers = array_filter($fichiers);

        if (empty($fichiers)) { return $this->json(['message' => 'Au moins un fichier est requis.'], 400); }
        if (count($fichiers) > 4) { return $this->json(['message' => 'Maximum 4 fichiers autorisés.'], 400); }

        $count = (int)$this->em->createQuery("SELECT COUNT(d.id) FROM ".Dossier::class." d")->getSingleScalarResult();
        $numero = "DOS-".date('Y')."-".str_pad($count + 1, 5, '0', STR_PAD_LEFT);

        $statutRecu   = $this->em->getRepository(StatutDossier::class)->findOneBy(['code' => 'RECU']);
        $nomCitoyen   = $request->request->get('nomCitoyen', '');
        $emailCitoyen = $request->request->get('emailCitoyen');

        $dossier = new Dossier();
        $dossier->setNumero($numero)
            ->setTitre($request->request->get('titre', ''))
            ->setDescription($request->request->get('description'))
            ->setNomCitoyen($nomCitoyen)
            ->setEmailCitoyen($emailCitoyen)
            ->setTelephoneCitoyen($request->request->get('telephoneCitoyen'))
            ->setService($service)
            ->setDateDepot(new \DateTime())
            ->setDateMiseAJourStatut(new \DateTime());

        if ($statutRecu) $dossier->setStatut($statutRecu);

        $this->em->persist($dossier);
        $this->em->flush();

        $allowed    = ['pdf', 'jpg', 'jpeg', 'png', 'gif', 'webp', 'bmp'];
        $slug       = fn(string $s): string => preg_replace('/[^a-z0-9._-]/i', '_', $s);
        $folderPath = $this->uploadDir
            . '/' . $slug($service->getNom())
            . '/' . $slug($nomCitoyen) . '_' . $slug($emailCitoyen ?? 'inconnu');
        if (!is_dir($folderPath)) mkdir($folderPath, 0777, true);

        $version = 1;
        foreach ($fichiers as $fichier) {
            $ext = strtolower($fichier->getClientOriginalExtension());
            if (!in_array($ext, $allowed)) continue;
            if ($fichier->getSize() > 10 * 1024 * 1024) continue;

            // Capture before move — temp file is gone after move()
            $mimeType    = $fichier->getMimeType() ?? 'application/octet-stream';
            $taille      = $fichier->getSize();
            $nomOriginal = $fichier->getClientOriginalName();

            $nomFichier = uniqid('', true).'_'.$nomOriginal;
            $fichier->move($folderPath, $nomFichier);

            $vd = new VersionDocument();
            $vd->setDossier($dossier)
               ->setNumeroVersion($version++)
               ->setNomFichier($nomOriginal)
               ->setCheminFichier($folderPath.'/'.$nomFichier)
               ->setTypeFichier($mimeType)
               ->setTailleFichier($taille)
               ->setDateCreation(new \DateTime())
               ->setEstActive(true);

            $this->em->persist($vd);
        }
        $this->em->flush();

        if ($emailCitoyen) {
            $this->emailService->sendConfirmationDepot(
                $emailCitoyen,
                $nomCitoyen,
                $numero,
                $service->getNom()
            );
        }

        return $this->json(['numeroDossier' => $numero]);
    }

    #[Route('/{id}/transferer', name: 'api_dossiers_transferer', methods: ['PATCH'])]
    public function transferer(string $id, Request $request): JsonResponse
    {
        $dossier = $this->em->getRepository(Dossier::class)->find($id);
        if (!$dossier) { return $this->json(['message' => 'Dossier introuvable.'], 404); }

        $data = json_decode($request->getContent(), true);
        $serviceId = (int)($data['serviceId'] ?? 0);

        $service = $this->em->getRepository(Service::class)->findOneBy(['id' => $serviceId, 'estActif' => true]);
        if (!$service) { return $this->json(['message' => 'Service cible introuvable ou inactif.'], 400); }

        $ancienStatut = $dossier->getStatut();
        $dossier->setService($service);

        $statutTransf = $this->em->getRepository(StatutDossier::class)->findOneBy(['code' => 'TRANSFERE']);
        if ($statutTransf) $dossier->setStatut($statutTransf);
        $dossier->setDateMiseAJourStatut(new \DateTime());

        $token = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $agentId = $payload['sub'] ?? null;

        $commentaire = "Transféré vers le service {$serviceId}" . (($data['commentaire'] ?? '') ? " : ".$data['commentaire'] : '');

        $h = new HistoriqueStatut();
        $h->setDossier($dossier)
          ->setAncienStatut($ancienStatut)
          ->setNouveauStatut($dossier->getStatut())
          ->setCommentaire($commentaire)
          ->setDateChangement(new \DateTime())
          ->setAgentId($agentId);

        $this->em->persist($h);
        $this->em->flush();

        return $this->json(['message' => 'Dossier transféré avec succès.']);
    }
}
