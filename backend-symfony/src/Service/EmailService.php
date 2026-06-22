<?php

namespace App\Service;

use Symfony\Component\Mailer\MailerInterface;
use Symfony\Component\Mime\Email;

class EmailService
{
    public function __construct(
        private MailerInterface $mailer,
        private string $fromAddress = 'noreply@gestion-docs.fr',
        private string $fromName    = 'Gestion Documents'
    ) {}

    public function sendRejetEmail(
        string $to,
        string $nomCitoyen,
        string $numeroDossier,
        string $motif,
        string $serviceNom
    ): void {
        $html = <<<HTML
<html><body>
<h2>Bonjour {$nomCitoyen},</h2>
<p>Nous regrettons de vous informer que votre dossier <strong>{$numeroDossier}</strong> a été rejeté.</p>
<p><strong>Motif :</strong> {$motif}</p>
<p>Vous pouvez soumettre un nouveau dossier en corrigeant les informations.</p>
<br/><p>Cordialement,<br/>Le service {$serviceNom}</p>
</body></html>
HTML;
        $this->send($to, "Votre dossier {$numeroDossier} a été rejeté", $html);
    }

    public function sendConfirmationDepot(
        string $to,
        string $nomCitoyen,
        string $numeroDossier,
        string $serviceNom
    ): void {
        $html = <<<HTML
<html><body>
<h2>Bonjour {$nomCitoyen},</h2>
<p>Votre dossier a été déposé avec succès auprès du service <strong>{$serviceNom}</strong>.</p>
<p><strong>Numéro de suivi :</strong> {$numeroDossier}</p>
<p>Conservez ce numéro pour suivre l'avancement de votre demande.</p>
<br/><p>Cordialement,<br/>L'Administration</p>
</body></html>
HTML;
        $this->send($to, "Confirmation de dépôt — Dossier {$numeroDossier}", $html);
    }

    private function send(string $to, string $subject, string $html): void
    {
        try {
            $email = (new Email())
                ->from("{$this->fromName} <{$this->fromAddress}>")
                ->to($to)
                ->subject($subject)
                ->html($html);
            $this->mailer->send($email);
        } catch (\Exception $e) {
            // Log but don't crash
        }
    }
}
