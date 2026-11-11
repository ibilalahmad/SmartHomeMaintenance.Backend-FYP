using SmartHomeMaintenance.Domain.Entities;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(User user, int expiryDays = 7);
    }
}
