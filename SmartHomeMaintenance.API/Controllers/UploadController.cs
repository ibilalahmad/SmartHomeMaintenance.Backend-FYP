using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SmartHomeMaintenance.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedProfileExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const long MaxProfilePicSize = 2 * 1024 * 1024; // 2 MB per SRS FR-SP-07 / FR-CUST-22

        private static readonly string[] AllowedEvidenceExtensions = { ".jpg", ".jpeg", ".png", ".pdf" };
        private const long MaxEvidenceSize = 5 * 1024 * 1024; // 5 MB per SRS FR-CUST-74

        private static readonly string[] AllowedPaymentProofExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxPaymentProofSize = 5 * 1024 * 1024; // 5 MB

        public UploadController(IWebHostEnvironment env)
        {
            _env = env;
        }

        [HttpPost("profile-pic")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadProfilePicture(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file was uploaded." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedProfileExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Please upload valid image format (JPG, PNG, GIF)." });
            }

            if (file.Length > MaxProfilePicSize)
            {
                return BadRequest(new { message = "Image size cannot exceed 2MB." });
            }

            return await SaveFileAsync(file, "profiles", "profile", extension);
        }

        [HttpPost("complaint-evidence")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadComplaintEvidence(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file was uploaded." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedEvidenceExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid file type. Please upload JPG, PNG, or PDF files only." });
            }

            if (file.Length > MaxEvidenceSize)
            {
                return BadRequest(new { message = "File size exceeds 5MB limit. Please compress or choose a smaller file." });
            }

            return await SaveFileAsync(file, "evidence", "evidence", extension);
        }

        [HttpPost("payment-proof")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        public async Task<IActionResult> UploadPaymentProof(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No payment proof file was uploaded." });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedPaymentProofExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid image format. Please upload JPG, PNG, GIF, or WEBP." });
            }

            if (file.Length > MaxPaymentProofSize)
            {
                return BadRequest(new { message = "Payment proof image cannot exceed 5MB." });
            }

            return await SaveFileAsync(file, "payments", "payment", extension);
        }

        private async Task<IActionResult> SaveFileAsync(IFormFile file, string subFolder, string prefix, string extension)
        {
            var rootPath = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(rootPath, "uploads", subFolder);

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{prefix}_{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            var absoluteUrl = $"{baseUrl}/uploads/{subFolder}/{uniqueFileName}";
            var relativeUrl = $"/uploads/{subFolder}/{uniqueFileName}";

            return Ok(new
            {
                url = absoluteUrl,
                relativeUrl = relativeUrl,
                fileName = file.FileName,
                storedFileName = uniqueFileName,
                message = "File uploaded successfully."
            });
        }

        [HttpGet("dummy-avatars")]
        public IActionResult GetDummyAvatars()
        {
            var dummyAvatars = new
            {
                admin = new[]
                {
                    new { id = "admin-1", name = "Executive Leader", url = "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=400&q=80" },
                    new { id = "admin-2", name = "Chief Manager", url = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=400&q=80" },
                    new { id = "admin-3", name = "Operations Admin", url = "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&w=400&q=80" }
                },
                customer = new[]
                {
                    new { id = "cust-1", name = "Friendly Customer", url = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=400&q=80" },
                    new { id = "cust-2", name = "Urban Resident", url = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=400&q=80" },
                    new { id = "cust-3", name = "Modern Homeowner", url = "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=400&q=80" },
                    new { id = "cust-4", name = "Creative Member", url = "https://images.unsplash.com/photo-1522075469751-3a6694fb2f61?auto=format&fit=crop&w=400&q=80" }
                },
                provider = new[]
                {
                    new { id = "sp-1", name = "Electrician Specialist", url = "https://images.unsplash.com/photo-1621905251189-08b45d6a269e?auto=format&fit=crop&w=400&q=80" },
                    new { id = "sp-2", name = "Certified Technician", url = "https://images.unsplash.com/photo-1581578731548-c64695cc6952?auto=format&fit=crop&w=400&q=80" },
                    new { id = "sp-3", name = "Plumbing Expert", url = "https://images.unsplash.com/photo-1504384308090-c894fdcc538d?auto=format&fit=crop&w=400&q=80" },
                    new { id = "sp-4", name = "Appliance Mechanic", url = "https://images.unsplash.com/photo-1540569014015-19a7be504e3a?auto=format&fit=crop&w=400&q=80" }
                }
            };

            return Ok(dummyAvatars);
        }
    }
}
