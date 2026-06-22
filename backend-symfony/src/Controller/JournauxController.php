<?php

namespace App\Controller;

use App\Entity\Journal;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/journaux')]
class JournauxController extends AbstractController
{
    public function __construct(private EntityManagerInterface $em) {}

    #[Route('', name: 'api_journaux_list', methods: ['GET'])]
    public function list(Request $request): JsonResponse
    {
        $utilisateurId = $request->query->get('utilisateurId');
        $module        = $request->query->get('module');
        $action        = $request->query->get('action');
        $niveauId      = $request->query->get('niveauId');
        $dateDebut     = $request->query->get('dateDebut');
        $dateFin       = $request->query->get('dateFin');
        $page          = max(1, (int)$request->query->get('page', 1));
        $pageSize      = max(1, (int)$request->query->get('pageSize', 30));

        $qb = $this->em->createQueryBuilder()
            ->select('j')
            ->from(Journal::class, 'j');

        if ($utilisateurId) { $qb->andWhere('j.utilisateurId = :uid')->setParameter('uid', $utilisateurId); }
        if ($module)        { $qb->andWhere('j.module LIKE :m')->setParameter('m', '%'.$module.'%'); }
        if ($action)        { $qb->andWhere('j.action LIKE :a')->setParameter('a', '%'.$action.'%'); }
        if ($niveauId)      { $qb->andWhere('j.niveauId = :n')->setParameter('n', (int)$niveauId); }
        if ($dateDebut)     { $qb->andWhere('j.dateAction >= :dd')->setParameter('dd', new \DateTime($dateDebut)); }
        if ($dateFin)       { $qb->andWhere('j.dateAction <= :df')->setParameter('df', (new \DateTime($dateFin))->modify('+1 day')); }

        $total = (clone $qb)->select('COUNT(j.id)')->getQuery()->getSingleScalarResult();

        $journaux = $qb->select('j')
            ->orderBy('j.dateAction', 'DESC')
            ->setFirstResult(($page - 1) * $pageSize)
            ->setMaxResults($pageSize)
            ->getQuery()
            ->getResult();

        // Résoudre les noms d'utilisateurs en une seule requête
        $userIds = array_filter(array_unique(array_map(fn($j) => $j->getUtilisateurId(), $journaux)));
        $userNames = [];
        if ($userIds) {
            $users = $this->em->createQueryBuilder()->select('u.id, u.prenom, u.nom')
                ->from(\App\Entity\Utilisateur::class, 'u')->where('u.id IN (:ids)')
                ->setParameter('ids', array_values($userIds))->getQuery()->getResult();
            foreach ($users as $u) { $userNames[$u['id']] = $u['prenom'].' '.$u['nom']; }
        }

        return $this->json([
            'total'    => (int)$total,
            'page'     => $page,
            'pageSize' => $pageSize,
            'data'     => array_map(fn(Journal $j) => [
                'id'          => $j->getId(),
                'utilisateur' => $j->getUtilisateurId() ? ($userNames[$j->getUtilisateurId()] ?? 'Système') : 'Système',
                'module'      => $j->getModule(),
                'action'      => $j->getAction(),
                'details'     => $j->getDetails() ?? '',
                'niveauId'    => $j->getNiveauId(),
                'dateAction'  => $j->getDateAction()->format(\DateTime::ATOM),
                'adresseIp'   => $j->getAdresseIp() ?? '',
            ], $journaux),
        ]);
    }

    #[Route('/mes-activites', name: 'api_journaux_mes_activites', methods: ['GET'])]
    public function mesActivites(Request $request): JsonResponse
    {
        $token = $this->container->get('security.token_storage')->getToken();
        $payload = method_exists($token, 'getPayload') ? $token->getPayload() : [];
        $utilisateurId = $request->query->get('utilisateurId') ?? ($payload['sub'] ?? null);

        $page     = max(1, (int)$request->query->get('page', 1));
        $pageSize = max(1, (int)$request->query->get('pageSize', 20));
        $dateDebut = $request->query->get('dateDebut');
        $dateFin   = $request->query->get('dateFin');

        $qb = $this->em->createQueryBuilder()
            ->select('j')
            ->from(Journal::class, 'j')
            ->where('j.utilisateurId = :uid')
            ->setParameter('uid', $utilisateurId);

        if ($dateDebut) { $qb->andWhere('j.dateAction >= :dd')->setParameter('dd', new \DateTime($dateDebut)); }
        if ($dateFin)   { $qb->andWhere('j.dateAction <= :df')->setParameter('df', (new \DateTime($dateFin))->modify('+1 day')); }

        $total    = (clone $qb)->select('COUNT(j.id)')->getQuery()->getSingleScalarResult();
        $journaux = $qb->select('j')->orderBy('j.dateAction', 'DESC')
            ->setFirstResult(($page - 1) * $pageSize)->setMaxResults($pageSize)
            ->getQuery()->getResult();

        return $this->json([
            'total'    => (int)$total,
            'page'     => $page,
            'pageSize' => $pageSize,
            'data'     => array_map(fn(Journal $j) => [
                'id'         => $j->getId(),
                'module'     => $j->getModule(),
                'action'     => $j->getAction(),
                'details'    => $j->getDetails() ?? '',
                'niveauId'   => $j->getNiveauId(),
                'dateAction' => $j->getDateAction()->format(\DateTime::ATOM),
                'adresseIp'  => $j->getAdresseIp() ?? '',
            ], $journaux),
        ]);
    }

    #[Route('/modules', name: 'api_journaux_modules', methods: ['GET'])]
    public function modules(): JsonResponse
    {
        $modules = $this->em->createQuery("SELECT DISTINCT j.module FROM ".Journal::class." j ORDER BY j.module ASC")
            ->getResult();
        return $this->json(array_column($modules, 'module'));
    }

    #[Route('/export', name: 'api_journaux_export', methods: ['GET'])]
    public function export(Request $request): Response
    {
        $qb = $this->em->createQueryBuilder()->select('j')->from(Journal::class, 'j');
        if ($request->query->get('utilisateurId')) { $qb->andWhere('j.utilisateurId = :uid')->setParameter('uid', $request->query->get('utilisateurId')); }
        if ($request->query->get('module'))        { $qb->andWhere('j.module LIKE :m')->setParameter('m', '%'.$request->query->get('module').'%'); }
        if ($request->query->get('niveauId'))      { $qb->andWhere('j.niveauId = :n')->setParameter('n', (int)$request->query->get('niveauId')); }
        if ($request->query->get('dateDebut'))     { $qb->andWhere('j.dateAction >= :dd')->setParameter('dd', new \DateTime($request->query->get('dateDebut'))); }
        if ($request->query->get('dateFin'))       { $qb->andWhere('j.dateAction <= :df')->setParameter('df', (new \DateTime($request->query->get('dateFin')))->modify('+1 day')); }

        $journaux = $qb->orderBy('j.dateAction', 'DESC')->getQuery()->getResult();

        // Résoudre les noms utilisateurs
        $userIds = array_filter(array_unique(array_map(fn($j) => $j->getUtilisateurId(), $journaux)));
        $userNames = [];
        if ($userIds) {
            $users = $this->em->createQueryBuilder()->select('u.id, u.prenom, u.nom')
                ->from(\App\Entity\Utilisateur::class, 'u')->where('u.id IN (:ids)')
                ->setParameter('ids', array_values($userIds))->getQuery()->getResult();
            foreach ($users as $u) { $userNames[$u['id']] = $u['prenom'].' '.$u['nom']; }
        }

        $csv = "Date,Utilisateur,Module,Action,Détails,Niveau,IP\n";
        foreach ($journaux as $j) {
            $u = $j->getUtilisateurId() ? ($userNames[$j->getUtilisateurId()] ?? 'Système') : 'Système';
            $csv .= sprintf('"%s","%s","%s","%s","%s",%d,"%s"'."\n",
                $j->getDateAction()->format('d/m/Y H:i'), $u, $j->getModule(), $j->getAction(),
                str_replace('"', '""', $j->getDetails() ?? ''), $j->getNiveauId(), $j->getAdresseIp() ?? '');
        }

        $response = new Response($csv);
        $response->headers->set('Content-Type', 'text/csv; charset=utf-8');
        $response->headers->set('Content-Disposition', 'attachment; filename="journaux_'.date('Ymd').'.csv"');
        return $response;
    }
}
