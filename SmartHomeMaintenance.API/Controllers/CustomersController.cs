using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Application.DTOs.Customer;
using SmartHomeMaintenance.Domain.Entities;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CustomersController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IApplicationDbContext _context;

        public CustomersController(IUserService userService, IApplicationDbContext context)
        {
            _userService = userService;
            _context = context;
        }

        private int? GetCurrentUserId()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idStr, out var id) ? id : null;
        }

        private bool IsAuthorizedForUser(int requestedId)
        {
            if (User.IsInRole("Admin")) return true;
            var currentUserId = GetCurrentUserId();
            return currentUserId.HasValue && currentUserId.Value == requestedId;
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProfile(int id)
        {
            if (!IsAuthorizedForUser(id))
                return Forbid();

            var result = await _userService.GetCustomerProfileAsync(id);
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProfile(int id, [FromBody] UpdateCustomerDto request)
        {
            if (!IsAuthorizedForUser(id))
                return Forbid();

            var result = await _userService.UpdateCustomerProfileAsync(id, request);
            return Ok(result);
        }

        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> DeactivateAccount(int id, [FromQuery] bool deactivate = true)
        {
            if (!IsAuthorizedForUser(id))
                return Forbid();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound(new { message = "Customer not found." });

            user.IsTemporarilyDeactivated = deactivate;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                isTemporarilyDeactivated = user.IsTemporarilyDeactivated,
                message = deactivate
                    ? "Your account has been temporarily deactivated. Signing back in will reactivate it."
                    : "Your account has been reactivated."
            });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCustomer(int id)
        {
            if (!IsAuthorizedForUser(id))
                return Forbid();

            // FR-CUST-23: Block deletion while pending/confirmed/in-progress bookings exist
            var pendingBookings = await _context.Bookings.CountAsync(b =>
                b.CustomerId == id &&
                (b.Status == "Pending" || b.Status == "Confirmed" || b.Status == "In Progress"));

            if (pendingBookings > 0)
            {
                return BadRequest(new
                {
                    message = $"Cannot delete account with {pendingBookings} active/pending booking(s). Please complete or cancel them first."
                });
            }

            var result = await _userService.DeleteCustomerAsync(id);
            return Ok(new { success = result, message = "Account deleted successfully." });
        }

        // ---------------- FAVORITES (WISHLIST) FR-CUST-89..90 ----------------

        [Authorize(Roles = "Customer,Admin")]
        [HttpGet("favorites")]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var favIds = await _context.CustomerFavorites
                .Where(f => f.CustomerId == userId.Value)
                .OrderByDescending(f => f.Id)
                .Select(f => f.ProviderId.ToString())
                .ToListAsync();

            return Ok(favIds);
        }

        [Authorize(Roles = "Customer")]
        [HttpPost("favorites/{providerId:int}")]
        public async Task<IActionResult> AddFavorite(int providerId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var exists = await _context.CustomerFavorites
                .AnyAsync(f => f.CustomerId == userId.Value && f.ProviderId == providerId);

            if (!exists)
            {
                _context.CustomerFavorites.Add(new CustomerFavorite
                {
                    CustomerId = userId.Value,
                    ProviderId = providerId,
                    CreatedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, providerId = providerId.ToString() });
        }

        [Authorize(Roles = "Customer")]
        [HttpDelete("favorites/{providerId:int}")]
        public async Task<IActionResult> RemoveFavorite(int providerId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue) return Unauthorized();

            var item = await _context.CustomerFavorites
                .FirstOrDefaultAsync(f => f.CustomerId == userId.Value && f.ProviderId == providerId);

            if (item != null)
            {
                _context.CustomerFavorites.Remove(item);
                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, providerId = providerId.ToString() });
        }
    }
}
