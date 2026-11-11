using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Exceptions;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.Customer;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.Application.Services
{
    public class UserService : IUserService
    {
        private readonly IApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public UserService(IApplicationDbContext _context, IEmailService emailService)
        {
            this._context = _context;
            this._emailService = emailService;
        }

        public async Task<CustomerProfileDto> GetCustomerProfileAsync(int customerId)
        {
            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null)
                throw new NotFoundException(nameof(Customer), customerId);

            return new CustomerProfileDto
            {
                CustomerId = customer.CustomerId,
                FullName = customer.User.FullName,
                Email = customer.User.Email,
                PhoneNumber = customer.User.PhoneNumber,
                Address = customer.User.Address,
                PreferredPayment = customer.PreferredPayment,
                DefaultAddress = customer.DefaultAddress,
                LoyaltyPoints = customer.LoyaltyPoints,
                ProfilePic = customer.ProfilePic ?? customer.User.ProfilePic,
                SavedAddressesJson = customer.SavedAddressesJson,
                SavedSearchesJson = customer.SavedSearchesJson,
                IsTemporarilyDeactivated = customer.User.IsTemporarilyDeactivated,
                RegDate = customer.User.RegDate,
                IsActive = customer.User.IsActive
            };
        }

        public async Task<CustomerProfileDto> UpdateCustomerProfileAsync(int customerId, UpdateCustomerDto request)
        {
            var customer = await _context.Customers
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.CustomerId == customerId);

            if (customer == null)
                throw new NotFoundException(nameof(Customer), customerId);

            // Update Base User Info
            customer.User.FullName = request.FullName;
            customer.User.PhoneNumber = request.PhoneNumber;
            customer.User.Address = request.Address;
            if (!string.IsNullOrEmpty(request.ProfilePic))
            {
                customer.User.ProfilePic = request.ProfilePic;
                customer.ProfilePic = request.ProfilePic;
            }

            // Update Customer specifics
            customer.PreferredPayment = request.PreferredPayment;
            customer.DefaultAddress = request.DefaultAddress ?? request.Address;
            if (request.SavedAddressesJson != null)
            {
                customer.SavedAddressesJson = request.SavedAddressesJson;
            }
            if (request.SavedSearchesJson != null)
            {
                customer.SavedSearchesJson = request.SavedSearchesJson;
            }

            await _context.SaveChangesAsync();

            return await GetCustomerProfileAsync(customerId);
        }

        public async Task<bool> UpdateUserStatusAsync(int userId, bool isActive)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null)
                throw new NotFoundException(nameof(User), userId);

            user.IsActive = isActive;
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                try
                {
                    _ = Task.Run(() => _emailService.SendAccountStatusNotificationAsync(user.Email, user.FullName, isActive));
                }
                catch { /* Non-blocking */ }
            }

            return true;
        }

        public async Task<bool> DeleteCustomerAsync(int customerId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == customerId);
            if (user == null)
                throw new NotFoundException(nameof(User), customerId);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<CustomerProfileDto>> GetAllCustomersAsync()
        {
            return await _context.Customers
                .Include(c => c.User)
                .Select(c => new CustomerProfileDto
                {
                    CustomerId = c.CustomerId,
                    FullName = c.User.FullName,
                    Email = c.User.Email,
                    PhoneNumber = c.User.PhoneNumber,
                    Address = c.User.Address,
                    PreferredPayment = c.PreferredPayment,
                    DefaultAddress = c.DefaultAddress,
                    LoyaltyPoints = c.LoyaltyPoints,
                    ProfilePic = c.ProfilePic ?? c.User.ProfilePic,
                    SavedAddressesJson = c.SavedAddressesJson,
                    SavedSearchesJson = c.SavedSearchesJson,
                    IsTemporarilyDeactivated = c.User.IsTemporarilyDeactivated,
                    RegDate = c.User.RegDate,
                    IsActive = c.User.IsActive
                }).ToListAsync();
        }
    }
}
