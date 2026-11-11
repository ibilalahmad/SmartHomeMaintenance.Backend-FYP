using System.Threading.Tasks;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IEmailVerificationService
    {
        Task<bool> IsRealEmailDomainAsync(string email);
        string GenerateAndStoreOtp(string email);
        bool ValidateOtp(string email, string code);
        void InvalidateOtp(string email);
    }
}
