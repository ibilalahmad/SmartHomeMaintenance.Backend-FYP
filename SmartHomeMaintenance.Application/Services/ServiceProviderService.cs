using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Exceptions;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.ServiceProvider;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartHomeMaintenance.Application.Services
{
    public class ServiceProviderService : IServiceProviderService
    {
        private readonly IApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public ServiceProviderService(IApplicationDbContext _context, IEmailService emailService)
        {
            this._context = _context;
            this._emailService = emailService;
        }

        public async Task<ServiceProviderProfileDto> GetServiceProviderProfileAsync(int providerId)
        {
            var sp = await _context.ServiceProviders
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ProviderId == providerId);

            if (sp == null)
                throw new NotFoundException(nameof(ServiceProvider), providerId);

            return new ServiceProviderProfileDto
            {
                ProviderId = sp.ProviderId,
                FullName = sp.User.FullName,
                Email = sp.User.Email,
                PhoneNumber = sp.User.PhoneNumber,
                Address = sp.User.Address,
                WorkExperience = sp.WorkExperience,
                Expertise = sp.Expertise,
                Qualification = sp.Qualification,
                HourlyRate = sp.HourlyRate,
                IsApproved = sp.IsApproved,
                AvgRating = sp.AvgRating,
                TotalBookings = sp.TotalBookings,
                Location = sp.Location,
                Latitude = sp.Latitude,
                Longitude = sp.Longitude,
                ProfilePic = sp.ProfilePic ?? sp.User.ProfilePic,
                IsActive = sp.User.IsActive,
                IsAvailable = sp.IsAvailable,
                RejectionReason = sp.RejectionReason,
                IsTemporarilyDeactivated = sp.User.IsTemporarilyDeactivated,
                RegistrationDate = sp.User.RegDate,
                RegDate = sp.User.RegDate
            };
        }

        public async Task<ServiceProviderProfileDto> UpdateServiceProviderProfileAsync(int providerId, UpdateServiceProviderDto request)
        {
            var sp = await _context.ServiceProviders
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ProviderId == providerId);

            if (sp == null)
                throw new NotFoundException(nameof(ServiceProvider), providerId);

            // Update Base User Info
            sp.User.FullName = request.FullName;
            sp.User.PhoneNumber = request.PhoneNumber;
            sp.User.Address = request.Address;
            if (!string.IsNullOrEmpty(request.ProfilePic))
            {
                sp.User.ProfilePic = request.ProfilePic;
                sp.ProfilePic = request.ProfilePic;
            }

            // Update Provider Specifics
            sp.WorkExperience = request.WorkExperience;
            sp.Expertise = request.Expertise;
            sp.Qualification = request.Qualification;
            sp.HourlyRate = request.HourlyRate;
            sp.Location = request.Location;
            sp.Latitude = request.Latitude;
            sp.Longitude = request.Longitude;
            if (request.IsAvailable.HasValue)
            {
                sp.IsAvailable = request.IsAvailable.Value;
            }

            await _context.SaveChangesAsync();

            return await GetServiceProviderProfileAsync(providerId);
        }

        public async Task<bool> DeleteServiceProviderAsync(int providerId)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == providerId);
            if (user == null)
                throw new NotFoundException(nameof(User), providerId);

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<ServiceProviderProfileDto>> GetAllServiceProvidersAsync()
        {
            return await _context.ServiceProviders
                .Include(s => s.User)
                .Select(sp => new ServiceProviderProfileDto
                {
                    ProviderId = sp.ProviderId,
                    FullName = sp.User.FullName,
                    Email = sp.User.Email,
                    PhoneNumber = sp.User.PhoneNumber,
                    Address = sp.User.Address,
                    WorkExperience = sp.WorkExperience,
                    Expertise = sp.Expertise,
                    Qualification = sp.Qualification,
                    HourlyRate = sp.HourlyRate,
                    IsApproved = sp.IsApproved,
                    AvgRating = sp.AvgRating,
                    TotalBookings = sp.TotalBookings,
                    Location = sp.Location,
                    Latitude = sp.Latitude,
                    Longitude = sp.Longitude,
                    ProfilePic = sp.ProfilePic ?? sp.User.ProfilePic,
                    IsActive = sp.User.IsActive,
                    IsAvailable = sp.IsAvailable,
                    RejectionReason = sp.RejectionReason,
                    IsTemporarilyDeactivated = sp.User.IsTemporarilyDeactivated,
                    RegistrationDate = sp.User.RegDate,
                    RegDate = sp.User.RegDate
                }).ToListAsync();
        }

        public async Task<bool> UpdateApprovalStatusAsync(int providerId, bool isApproved, string? reason = null, bool requestMoreInfo = false)
        {
            var sp = await _context.ServiceProviders
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.ProviderId == providerId);

            if (sp == null)
                throw new NotFoundException(nameof(ServiceProvider), providerId);

            if (requestMoreInfo)
            {
                sp.IsApproved = false;
                sp.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Additional information requested by Admin." : reason.Trim();
            }
            else
            {
                sp.IsApproved = isApproved;
                sp.RejectionReason = isApproved ? null : (string.IsNullOrWhiteSpace(reason) ? null : reason.Trim());
            }

            await _context.SaveChangesAsync();

            // Notify the Service Provider of approval, rejection, or more-info request via email & in-app notification
            if (sp.User != null)
            {
                _context.Notifications.Add(new AppNotification
                {
                    UserId = sp.ProviderId,
                    Title = requestMoreInfo ? "More Information Requested" : (isApproved ? "Account Approved" : "Application Rejected"),
                    Message = requestMoreInfo
                        ? $"Admin requested more details on your SP application: {sp.RejectionReason}"
                        : (isApproved
                            ? "Congratulations! Your Service Provider account has been approved. You can now log in and receive bookings."
                            : $"Your Service Provider application was rejected.{(string.IsNullOrWhiteSpace(sp.RejectionReason) ? "" : " Reason: " + sp.RejectionReason)}"),
                    Type = isApproved ? "success" : "warning",
                    LinkUrl = "/sp/profile",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                if (!string.IsNullOrWhiteSpace(sp.User.Email))
                {
                    try
                    {
                        if (requestMoreInfo)
                        {
                            var msg = $"Our administration team reviewed your Service Provider application and needs additional information before approval:\n\n{sp.RejectionReason}\n\nPlease update your details or reply to support.";
                            _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                                sp.User.Email, sp.User.FullName,
                                "Action Required: Additional Information for SHMS Application",
                                "INFO REQUESTED", "Provider Application Review",
                                "Additional details required for your application", msg,
                                "Status: Pending Admin Review", true));
                        }
                        else if (isApproved)
                        {
                            _ = Task.Run(() => _emailService.SendWelcomeEmailAsync(sp.User.Email, sp.User.FullName, "ServiceProvider"));
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(sp.RejectionReason))
                            {
                                _ = Task.Run(() => _emailService.SendNotificationEmailAsync(
                                    sp.User.Email, sp.User.FullName,
                                    "Update on Your Service Provider Application - SHMS",
                                    "APPLICATION UPDATE", "Provider Registration Review",
                                    "Your Service Provider application was not approved",
                                    $"Unfortunately, your Service Provider application was rejected.\nReason: {sp.RejectionReason}\nYou may address the feedback and contact support.",
                                    "Status: Rejected", true));
                            }
                            else
                            {
                                _ = Task.Run(() => _emailService.SendProviderRejectedNotificationAsync(sp.User.Email, sp.User.FullName));
                            }
                        }
                    }
                    catch { /* Non-blocking */ }
                }
            }

            return true;
        }
    }
}
