using System.ComponentModel.DataAnnotations;

namespace Hamco.Core.Models;

public class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}
