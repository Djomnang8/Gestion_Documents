<?php

namespace App\Controller;

use App\Entity\Service;
use Doctrine\ORM\EntityManagerInterface;
use Symfony\Bundle\FrameworkBundle\Controller\AbstractController;
use Symfony\Component\HttpFoundation\JsonResponse;
use Symfony\Component\Routing\Annotation\Route;

#[Route('/api/services')]
class ServicesController extends AbstractController
{
    public function __construct(private EntityManagerInterface $em) {}

    #[Route('', name: 'api_services_list', methods: ['GET'])]
    public function list(): JsonResponse
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

    #[Route('/{id}', name: 'api_services_get', methods: ['GET'])]
    public function get(int $id): JsonResponse
    {
        $service = $this->em->getRepository(Service::class)->find($id);
        if (!$service) {
            return $this->json(['error' => 'Service introuvable.'], 404);
        }
        return $this->json([
            'id'          => $service->getId(),
            'nom'         => $service->getNom(),
            'description' => $service->getDescription(),
            'estActif'    => $service->isEstActif(),
        ]);
    }

    #[Route('', name: 'api_services_create', methods: ['POST'])]
    public function create(\Symfony\Component\HttpFoundation\Request $request): JsonResponse
    {
        $data = json_decode($request->getContent(), true);
        $service = new Service();
        $service->setNom($data['nom'] ?? '');
        $service->setDescription($data['description'] ?? null);
        $service->setEstActif($data['estActif'] ?? true);
        $this->em->persist($service);
        $this->em->flush();
        return $this->json(['id' => $service->getId(), 'nom' => $service->getNom()], 201);
    }

    #[Route('/{id}', name: 'api_services_update', methods: ['PUT', 'PATCH'])]
    public function update(int $id, \Symfony\Component\HttpFoundation\Request $request): JsonResponse
    {
        $service = $this->em->getRepository(Service::class)->find($id);
        if (!$service) {
            return $this->json(['error' => 'Service introuvable.'], 404);
        }
        $data = json_decode($request->getContent(), true);
        if (isset($data['nom'])) $service->setNom($data['nom']);
        if (isset($data['description'])) $service->setDescription($data['description']);
        if (isset($data['estActif'])) $service->setEstActif($data['estActif']);
        $this->em->flush();
        return $this->json(['id' => $service->getId(), 'nom' => $service->getNom()]);
    }
}
