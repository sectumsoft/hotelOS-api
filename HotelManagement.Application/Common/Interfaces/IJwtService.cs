using HotelManagement.Domain.Entities;
namespace HotelManagement.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateToken(User user);
    string GenerateRefreshToken();
    bool ValidateToken(string token, out Guid userId, out Guid tenantId);
}
