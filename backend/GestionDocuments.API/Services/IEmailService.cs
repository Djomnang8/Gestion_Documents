// Services/IEmailService.cs
public interface IEmailService
{
    Task SendEmailAsync(string to, string subject, string body);
}