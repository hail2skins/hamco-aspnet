using Hamco.Core.Models;
using System.Security.Claims;

namespace Hamco.Core.Services;

public interface IJwtService
{
    string GenerateToken(User user);
    ClaimsPrincipal? ValidateToken(string token);
}
