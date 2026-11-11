using SmartHomeMaintenance.Application.DTOs.Customer;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.Common.Interfaces
{
    public interface IUserService
    {
        Task<CustomerProfileDto> GetCustomerProfileAsync(int customerId);
        Task<CustomerProfileDto> UpdateCustomerProfileAsync(int customerId, UpdateCustomerDto request);
        Task<bool> UpdateUserStatusAsync(int userId, bool isActive);
        Task<bool> DeleteCustomerAsync(int customerId);
        Task<IEnumerable<CustomerProfileDto>> GetAllCustomersAsync();
    }
}
