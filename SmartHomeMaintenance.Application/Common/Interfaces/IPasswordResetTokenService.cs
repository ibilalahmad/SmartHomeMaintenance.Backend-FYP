namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IPasswordResetTokenService
    {
        string GenerateAndStoreCode(string email);
        bool ValidateCode(string email, string code);
        void InvalidateCode(string email);
    }
}
