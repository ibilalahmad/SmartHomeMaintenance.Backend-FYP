using SmartHomeMaintenance.Application.DTOs.Auth;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IAuthService
    {
        Task<LoginResponseDto> LoginAsync(LoginRequestDto request);
        Task<RegistrationResponseDto> RegisterCustomerAsync(CustomerRegisterDto request);
        Task<RegistrationResponseDto> RegisterServiceProviderAsync(ServiceProviderRegisterDto request);
        Task<LoginResponseDto> VerifyRegistrationOtpAsync(VerifyRegistrationOtpRequestDto request);
        Task<bool> ResendRegistrationOtpAsync(string email);
        Task<bool> AdminResetPasswordAsync(int userId, string newPassword);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword);
        Task<bool> ForgotPasswordAsync(string email);
        Task<bool> VerifyResetCodeAsync(string email, string code);
        Task<bool> ResetPasswordWithCodeAsync(string email, string code, string newPassword);
    }
}
