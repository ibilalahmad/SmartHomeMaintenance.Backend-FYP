using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SmartHomeMaintenance.Application.Common.Interfaces;
using SmartHomeMaintenance.Domain.Entities;
using SmartHomeMaintenance.Domain.Enums;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SmartHomeMaintenance.Infrastructure.Identity
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly IConfiguration _configuration;

        public JwtTokenGenerator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(User user, int expiryDays = 7)
        {
            var secret = _configuration["Jwt:Secret"] ?? "SmartHomeMaintenanceSystem_SuperSecretKey_2026!#SecureJWT";
            var issuer = _configuration["Jwt:Issuer"] ?? "SmartHomeMaintenance.API";
            var audience = _configuration["Jwt:Audience"] ?? "SmartHomeMaintenance.Frontend";
            var resolvedDays = expiryDays > 0
                ? expiryDays
                : (int.TryParse(_configuration["Jwt:ExpiryDays"], out var days) ? days : 7);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.UserType.ToString()),
                new Claim("UserType", user.UserType.ToString())
            };

            if (user.UserType == UserType.Admin)
            {
                var roleName = user.Admin?.Role ?? "SuperAdmin";
                claims.Add(new Claim("AdminRole", roleName));
            }

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(resolvedDays),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
