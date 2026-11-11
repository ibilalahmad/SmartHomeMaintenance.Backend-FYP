using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.ServiceProvider;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ServiceProvidersController : ControllerBase
    {
        private readonly IServiceProviderService _spService;
        private readonly IApplicationDbContext _context;

        public ServiceProvidersController(IServiceProviderService spService, IApplicationDbContext context)
        {
            _spService = spService;
            _context = context;
        }

        private bool IsAuthorizedForProvider(int requestedId)
        {
            if (User.IsInRole("Admin")) return true;
            var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(currentUserIdStr, out var currentUserId) && currentUserId == requestedId;
        }

        [AllowAnonymous]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            var result = await _spService.GetServiceProviderProfileAsync(id);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateServiceProviderDto request)
        {
            if (!IsAuthorizedForProvider(id))
                return Forbid();

            var result = await _spService.UpdateServiceProviderProfileAsync(id, request);
            return Ok(result);
        }

        [HttpPut("{id}/availability")]
        public async Task<IActionResult> UpdateAvailability(int id, [FromQuery] bool isAvailable)
        {
            if (!IsAuthorizedForProvider(id))
                return Forbid();

            var sp = await _context.ServiceProviders.FirstOrDefaultAsync(s => s.ProviderId == id);
            if (sp == null) return NotFound(new { message = "Service provider not found." });

            sp.IsAvailable = isAvailable;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isAvailable = sp.IsAvailable,
                message = sp.IsAvailable
                    ? "Your status is now Available for bookings."
                    : "Your status is set to Not Available. You are hidden from customer search until restored."
            });
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> DeactivateAccount(int id, [FromQuery] bool deactivate = true)
        {
            if (!IsAuthorizedForProvider(id))
                return Forbid();

            var user = await _context.Users
                .Include(u => u.ServiceProvider)
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null) return NotFound(new { message = "Service provider not found." });

            user.IsTemporarilyDeactivated = deactivate;
            if (user.ServiceProvider != null)
            {
                user.ServiceProvider.IsAvailable = !deactivate;
            }
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isTemporarilyDeactivated = user.IsTemporarilyDeactivated,
                message = deactivate
                    ? "Account temporarily deactivated. Signing back in will reactivate your profile."
                    : "Account reactivated."
            });
        }

        [HttpGet("{id}/analytics")]
        public async Task<IActionResult> GetProviderAnalytics(int id)
        {
            if (!IsAuthorizedForProvider(id))
                return Forbid();

            var allProviders = await _context.ServiceProviders
                .Where(s => s.IsApproved)
                .ToListAsync();

            var completedCountsByProvider = await _context.Bookings
                .Where(b => b.Status == "Completed")
                .GroupBy(b => b.ProviderId)
                .Select(g => new { ProviderId = g.Key, Count = g.Count() })
                .ToListAsync();

            var ratedProviders = allProviders.Where(s => s.AvgRating > 0).ToList();
            decimal platformAvgRating = ratedProviders.Count > 0
                ? Math.Round(ratedProviders.Average(s => s.AvgRating), 1)
                : 0.0m;

            double platformAvgJobs = allProviders.Count > 0
                ? Math.Round(allProviders.Average(p =>
                    completedCountsByProvider.FirstOrDefault(c => c.ProviderId == p.ProviderId)?.Count ?? p.TotalBookings), 1)
                : 0;

            return Ok(new
            {
                platformAvgRating,
                platformAvgJobs
            });
        }

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _spService.GetAllServiceProvidersAsync();
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProvider(int id)
        {
            if (!IsAuthorizedForProvider(id))
                return Forbid();

            // FR-SP-23: Check pending bookings before self-deletion
            var pendingBookings = await _context.Bookings.CountAsync(b =>
                b.ProviderId == id &&
                (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "In Progress"));

            if (pendingBookings > 0)
            {
                return BadRequest(new
                {
                    message = $"Cannot delete account with {pendingBookings} active/pending booking(s). Please complete or resolve them first."
                });
            }

            var result = await _spService.DeleteServiceProviderAsync(id);
            return Ok(new { success = result, message = "Account deleted successfully." });
        }
    }
}
