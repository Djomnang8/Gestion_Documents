<?php

namespace App\Controller;

use App\Entity\Dossier;
use App\Entity\Utilisateur;
use App\Entity\VersionDocument;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/statistiques')]
class StatistiquesController extends AbstractController
{
    public function __construct(private EntityManagerInterface $em) {}

    #[Route('/dashboard', name: 'api_stats_dashboard', methods: ['GET'])]
    public function dashboard(Request $request): JsonResponse
    {
        $totalDossiers = (int)$this->em->createQuery("SELECT COUNT(d.id) FROM ".Dossier::class." d")->getSingleScalarResult();

        $parStatut = $this->em->createQuery(
            "SELECT s.code as statut, COUNT(d.id) as count FROM ".Dossier::class." d JOIN d.statut s GROUP BY s.code"
        )->getResult();

        $totalUtilisateurs      = (int)$this->em->createQuery("SELECT COUNT(u.id) FROM ".Utilisateur::class." u WHERE u.estSupprime = false")->getSingleScalarResult();
        $utilisateursActifs     = (int)$this->em->createQuery("SELECT COUNT(u.id) FROM ".Utilisateur::class." u WHERE u.estActif = true AND u.estSupprime = false")->getSingleScalarResult();
        $utilisateursListeNoire = (int)$this->em->createQuery("SELECT COUNT(u.id) FROM ".Utilisateur::class." u WHERE u.estListeNoire = true AND u.estSupprime = false")->getSingleScalarResult();

        return $this->json([
            'totalDossiers'          => $totalDossiers,
            'dossiersParStatut'      => $parStatut,
            'totalUtilisateurs'      => $totalUtilisateurs,
            'utilisateursActifs'     => $utilisateursActifs,
            'utilisateursListeNoire' => $utilisateursListeNoire,
        ]);
    }

    #[Route('/dossiers', name: 'api_stats_dossiers', methods: ['GET'])]
    public function dossiers(Request $request): JsonResponse
    {
        $periode   = $request->query->get('periode', '30j');
        $dateDebut = $request->query->get('dateDebut');
        $dateFin   = $request->query->get('dateFin');
        $serviceId = $request->query->get('serviceId');

        [$depuis, $jusqu] = $this->calculerPlage($periode, $dateDebut, $dateFin);
        $precedent = (clone $depuis)->modify('-'.max(1,(int)$depuis->diff($jusqu)->days).' days');

        $qb = $this->em->createQueryBuilder()
            ->from(Dossier::class, 'd')
            ->join('d.statut', 's')
            ->where("s.code != 'ARCHIVE'");
        if ($serviceId) { $qb->andWhere('d.service = :sid')->setParameter('sid', (int)$serviceId); }

        $courant   = $qb->select('d')->andWhere('d.dateDepot BETWEEN :dd AND :df')->setParameter('dd', $depuis)->setParameter('df', $jusqu)->getQuery()->getResult();
        $precedents = (clone $qb)->select('d')->andWhere('d.dateDepot BETWEEN :dp AND :dd2')->setParameter('dp', $precedent)->setParameter('dd2', $depuis)->getQuery()->getResult();

        $total   = count($courant);
        $totalPr = count($precedents);
        $termines = count(array_filter($courant, fn($d) => $d->getStatut()->getCode() === 'TERMINE'));
        $terminPr = count(array_filter($precedents, fn($d) => $d->getStatut()->getCode() === 'TERMINE'));

        $tTrait  = $total   > 0 ? round($termines / $total   * 100, 1) : 0;
        $tTraitP = $totalPr > 0 ? round($terminPr / $totalPr * 100, 1) : 0;

        // Par statut
        $parStatut = [];
        foreach ($courant as $d) {
            $code = $d->getStatut()->getCode();
            $libelle = $d->getStatut()->getLibelle();
            $parStatut[$code] = ['statut' => $libelle, 'code' => $code, 'count' => ($parStatut[$code]['count'] ?? 0) + 1];
        }

        // Par service
        $parService = [];
        foreach ($courant as $d) {
            $nom = $d->getService()->getNom();
            $parService[$nom] = ['service' => $nom, 'count' => ($parService[$nom]['count'] ?? 0) + 1];
        }

        // Évolution mensuelle 12 mois
        $evolution = [];
        for ($i = 11; $i >= 0; $i--) {
            $mois = (new \DateTime())->modify("-$i months");
            $m = array_filter($courant, fn($d) => (int)$d->getDateDepot()->format('m') === (int)$mois->format('m') && (int)$d->getDateDepot()->format('Y') === (int)$mois->format('Y'));
            $evolution[] = [
                'mois'    => $mois->format('M y'),
                'recu'    => count($m),
                'traite'  => count(array_filter($m, fn($d) => $d->getStatut()->getCode() === 'TERMINE')),
            ];
        }

        return $this->json([
            'totalDossiers'          => $total,
            'tauxTraitement'         => $tTrait,
            'tendanceTotalDossiers'  => $this->tendance($total, $totalPr),
            'tendanceTauxTraitement' => $this->tendance($tTrait, $tTraitP),
            'dossiersParStatut'      => array_values($parStatut),
            'repartitionParService'  => array_values($parService),
            'evolutionMensuelle'     => $evolution,
        ]);
    }

    #[Route('/archiviste', name: 'api_stats_archiviste', methods: ['GET'])]
    public function archiviste(Request $request): JsonResponse
    {
        $periode   = $request->query->get('periode', '30j');
        $dateDebut = $request->query->get('dateDebut');
        $dateFin   = $request->query->get('dateFin');

        [$depuis, $jusqu] = $this->calculerPlage($periode, $dateDebut, $dateFin);

        $archivesPeriode = $this->em->createQuery(
            "SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'ARCHIVE' AND d.dateArchivage BETWEEN :dd AND :df"
        )->setParameters(['dd' => $depuis, 'df' => $jusqu])->getSingleScalarResult();

        $totalArchives = $this->em->createQuery(
            "SELECT COUNT(d.id) FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'ARCHIVE'"
        )->getSingleScalarResult();

        $restaurations = $this->em->createQuery(
            "SELECT COUNT(v.id) FROM ".VersionDocument::class." v WHERE v.estActive = true AND v.dateCreation BETWEEN :dd AND :df"
        )->setParameters(['dd' => $depuis, 'df' => $jusqu])->getSingleScalarResult();

        // Évolution mensuelle 12 mois
        $tousArchives = $this->em->createQuery(
            "SELECT d FROM ".Dossier::class." d JOIN d.statut s WHERE s.code = 'ARCHIVE' AND d.dateArchivage IS NOT NULL"
        )->getResult();

        $evolution = [];
        for ($i = 11; $i >= 0; $i--) {
            $mois = (new \DateTime())->modify("-$i months");
            $evolution[] = [
                'mois'     => $mois->format('M y'),
                'archives' => count(array_filter($tousArchives, fn($d) =>
                    (int)$d->getDateArchivage()->format('m') === (int)$mois->format('m') &&
                    (int)$d->getDateArchivage()->format('Y') === (int)$mois->format('Y')
                )),
            ];
        }

        $parService = $this->em->createQuery(
            "SELECT srv.nom as service, COUNT(d.id) as count FROM ".Dossier::class." d JOIN d.statut s JOIN d.service srv WHERE s.code = 'ARCHIVE' GROUP BY srv.nom ORDER BY count DESC"
        )->getResult();

        return $this->json([
            'totalArchivesPeriode' => (int)$archivesPeriode,
            'totalArchivesGlobal'  => (int)$totalArchives,
            'restaurationsPeriode' => (int)$restaurations,
            'evolution'            => $evolution,
            'parService'           => $parService,
        ]);
    }

    #[Route('/export/pdf', name: 'api_stats_export_pdf', methods: ['GET'])]
    public function exportPdf(Request $request): Response
    {
        $periode = $request->query->get('periode', '30j');
        [$depuis, $jusqu] = $this->calculerPlage($periode);

        $dossiers = $this->em->createQuery(
            "SELECT d FROM ".Dossier::class." d JOIN d.statut s WHERE d.dateDepot BETWEEN :dd AND :df"
        )->setParameters(['dd' => $depuis, 'df' => $jusqu])->getResult();

        $rows = [['Numero', 'Titre', 'Statut', 'Date depot', 'Service']];
        foreach ($dossiers as $d) {
            $rows[] = [
                $d->getNumero(),
                mb_substr($d->getTitre(), 0, 40),
                $d->getStatut()->getLibelle(),
                $d->getDateDepot()->format('d/m/Y'),
                $d->getService()->getNom(),
            ];
        }

        $pdf = $this->buildMinimalPdf('Rapport Statistiques - '.date('d/m/Y'), $rows);
        return new Response($pdf, 200, [
            'Content-Type' => 'application/pdf',
            'Content-Disposition' => 'attachment; filename="statistiques-'.date('Y-m-d').'.pdf"',
        ]);
    }

    #[Route('/export/excel', name: 'api_stats_export_excel', methods: ['GET'])]
    public function exportExcel(Request $request): Response
    {
        $periode = $request->query->get('periode', '30j');
        [$depuis, $jusqu] = $this->calculerPlage($periode);

        $dossiers = $this->em->createQuery(
            "SELECT d FROM ".Dossier::class." d JOIN d.statut s WHERE d.dateDepot BETWEEN :dd AND :df"
        )->setParameters(['dd' => $depuis, 'df' => $jusqu])->getResult();

        $csv = "Numero;Titre;Statut;Date depot;Service\n";
        foreach ($dossiers as $d) {
            $csv .= implode(';', [
                $d->getNumero(),
                '"'.str_replace('"', '""', $d->getTitre()).'"',
                $d->getStatut()->getLibelle(),
                $d->getDateDepot()->format('d/m/Y'),
                '"'.str_replace('"', '""', $d->getService()->getNom()).'"',
            ])."\n";
        }

        return new Response("\xEF\xBB\xBF".$csv, 200, [
            'Content-Type' => 'application/vnd.ms-excel; charset=utf-8',
            'Content-Disposition' => 'attachment; filename="statistiques-'.date('Y-m-d').'.xls"',
        ]);
    }

    private function buildMinimalPdf(string $title, array $rows): string
    {
        $esc     = fn(string $s): string => str_replace(['\\', '(', ')'], ['\\\\', '\\(', '\\)'], $s);
        $lines   = ["BT", "/F1 12 Tf", "50 780 Td", "({$esc($title)}) Tj"];
        $y       = 750;
        foreach ($rows as $row) {
            $line    = implode('  |  ', array_map(fn($v) => mb_substr((string)$v, 0, 55), $row));
            $lines[] = "50 $y Td";
            $lines[] = "({$esc($line)}) Tj";
            $y      -= 18;
            if ($y < 50) break;
        }
        $lines[]  = "ET";
        $stream   = implode("\n", $lines);
        $len      = strlen($stream);

        $parts   = ["%PDF-1.4\n"];
        $off     = [];
        $off[1]  = strlen($parts[0]);
        $parts[] = "1 0 obj\n<</Type /Catalog /Pages 2 0 R>>\nendobj\n";
        $off[2]  = array_sum(array_map('strlen', $parts));
        $parts[] = "2 0 obj\n<</Type /Pages /Kids [3 0 R] /Count 1>>\nendobj\n";
        $off[3]  = array_sum(array_map('strlen', $parts));
        $parts[] = "3 0 obj\n<</Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources<</Font<</F1 5 0 R>>>>>>\nendobj\n";
        $off[4]  = array_sum(array_map('strlen', $parts));
        $parts[] = "4 0 obj\n<</Length $len>>\nstream\n$stream\nendstream\nendobj\n";
        $off[5]  = array_sum(array_map('strlen', $parts));
        $parts[] = "5 0 obj\n<</Type /Font /Subtype /Type1 /BaseFont /Helvetica>>\nendobj\n";

        $xref    = array_sum(array_map('strlen', $parts));
        $parts[] = "xref\n0 6\n0000000000 65535 f \n";
        foreach ($off as $o) {
            $parts[] = str_pad((string)$o, 10, '0', STR_PAD_LEFT)." 00000 n \n";
        }
        $parts[] = "trailer\n<</Size 6 /Root 1 0 R>>\nstartxref\n$xref\n%%EOF";

        return implode('', $parts);
    }

    private function calculerPlage(string $periode, ?string $dateDebut = null, ?string $dateFin = null): array
    {
        $fin = new \DateTime();
        $deb = match($periode) {
            '7j'     => (clone $fin)->modify('-7 days'),
            '90j'    => (clone $fin)->modify('-90 days'),
            'custom' => $dateDebut ? new \DateTime($dateDebut) : (clone $fin)->modify('-30 days'),
            default  => (clone $fin)->modify('-30 days'),
        };
        if ($periode === 'custom' && $dateFin) $fin = new \DateTime($dateFin);
        return [$deb, $fin];
    }

    private function tendance(float $actuel, float $prec): float
    {
        if ($prec == 0) return 0;
        return round(($actuel - $prec) / $prec * 100, 1);
    }
}
