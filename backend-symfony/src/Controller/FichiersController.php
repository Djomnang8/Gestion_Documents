<?php

namespace App\Controller;

use App\Entity\VersionDocument;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\BinaryFileResponse;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\HttpFoundation\Request;
use Symfony\Component\HttpFoundation\Response;
use Symfony\Component\HttpFoundation\ResponseHeaderBag;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/fichiers')]
class FichiersController extends AbstractController
{
    private array $extensionsInline = ['pdf', 'jpg', 'jpeg', 'png', 'gif', 'webp'];

    public function __construct(
        private EntityManagerInterface $em,
        private string $uploadDir
    ) {}

    #[Route('/download', name: 'api_fichiers_download', methods: ['GET'])]
    public function download(Request $request): Response
    {
        $chemin = $request->query->get('chemin', '');
        if (empty($chemin)) return $this->json(['error' => 'Chemin manquant.'], 400);

        try {
            $cheminNormalise = realpath($chemin) ?: $chemin;
        } catch (\Exception) {
            return $this->json(['error' => 'Chemin invalide.'], 400);
        }

        // Sécurité : path traversal
        $racine = realpath($this->uploadDir) ?: $this->uploadDir;
        if (!str_starts_with($cheminNormalise, $racine)) {
            return new Response('Accès interdit.', Response::HTTP_FORBIDDEN);
        }

        if (!file_exists($cheminNormalise)) {
            return $this->json(['error' => 'Fichier introuvable.'], 404);
        }

        $ext         = strtolower(pathinfo($cheminNormalise, PATHINFO_EXTENSION));
        $nomFichier  = basename($cheminNormalise);
        $contentType = $this->detectMimeType($cheminNormalise);
        $disposition = in_array($ext, $this->extensionsInline) ? 'inline' : 'attachment';

        $response = new BinaryFileResponse($cheminNormalise);
        $response->headers->set('Content-Type', $contentType);
        $response->headers->set('Access-Control-Allow-Origin', '*');
        $response->setContentDisposition(
            $disposition === 'inline' ? ResponseHeaderBag::DISPOSITION_INLINE : ResponseHeaderBag::DISPOSITION_ATTACHMENT,
            $nomFichier
        );
        return $response;
    }

    #[Route('/preview/{dossierId}', name: 'api_fichiers_preview', methods: ['GET'])]
    public function preview(string $dossierId, Request $request): Response
    {
        $version = $this->em->createQueryBuilder()
            ->select('v')
            ->from(VersionDocument::class, 'v')
            ->where('v.dossier = :did AND v.estActive = true')
            ->setParameter('did', $dossierId)
            ->orderBy('v.numeroVersion', 'DESC')
            ->setMaxResults(1)
            ->getQuery()
            ->getOneOrNullResult();

        if (!$version) {
            return $this->json(['error' => 'Aucun document pour ce dossier.'], 404);
        }

        $request->query->set('chemin', $version->getCheminFichier());
        return $this->download($request);
    }

    private function detectMimeType(string $path): string
    {
        $ext = strtolower(pathinfo($path, PATHINFO_EXTENSION));
        return match($ext) {
            'pdf'  => 'application/pdf',
            'jpg', 'jpeg' => 'image/jpeg',
            'png'  => 'image/png',
            'gif'  => 'image/gif',
            'webp' => 'image/webp',
            'docx' => 'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
            'xlsx' => 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
            default => mime_content_type($path) ?: 'application/octet-stream',
        };
    }
}
