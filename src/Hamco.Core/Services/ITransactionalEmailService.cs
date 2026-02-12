namespace Hamco.Core.Services;

public interface ITransactionalEmailService
{
    Task SendVerificationEmailAsync(string toEmail, string verificationLink);
    Task SendPasswordResetEmailAsync(string toEmail, string resetLink);
}
