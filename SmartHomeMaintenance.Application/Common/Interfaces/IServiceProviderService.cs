using SmartHomeMaintenance.Application.DTOs.ServiceProvider;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IServiceProviderService
    {
        Task<ServiceProviderProfileDto> GetServiceProviderProfileAsync(int providerId);
        Task<ServiceProviderProfileDto> UpdateServiceProviderProfileAsync(int providerId, UpdateServiceProviderDto request);
        Task<bool> DeleteServiceProviderAsync(int providerId);
        Task<IEnumerable<ServiceProviderProfileDto>> GetAllServiceProvidersAsync();
        Task<bool> UpdateApprovalStatusAsync(int providerId, bool isApproved, string? reason = null, bool requestMoreInfo = false);
    }
}
