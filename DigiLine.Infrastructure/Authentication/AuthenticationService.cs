using DigiLine.Application.Common.Authentication;
using DigiLine.Application.Common.Interfaces;
using DigiLine.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace DigiLine.Infrastructure.Authentication
{

    public class AuthenticationService : IAuthenticationService
    {
        private readonly DigiLineDbContext _dbContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly JwtSettings _jwtSettings;

        public AuthenticationService(
            DigiLineDbContext dbContext,
            IPasswordHasher passwordHasher,
            IOptions<JwtSettings> jwtOptions)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _jwtSettings = jwtOptions.Value;
        }

        public async Task<string?> LoginAsync(
            string email,
            string password,
            CancellationToken cancellationToken = default)
        {
            var staff = await _dbContext.Staff
                .AsNoTracking().Include(x => x.Roles)
                .SingleOrDefaultAsync(
                    x => x.NormalizedEmail == email.Trim().ToUpperInvariant(),
                    cancellationToken);

            if (staff is null || !staff.IsActive || staff.InvitationPending)
            {
                return null;
            }

            if (!_passwordHasher.Verify(password, staff.PasswordHash))
            {
                return null;
            }

            var claims = new List<Claim>
            {
            new Claim(JwtRegisteredClaimNames.Sub, staff.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, staff.Email),
            new Claim(JwtRegisteredClaimNames.Name, staff.Name),

            new Claim("clinic_id", staff.ClinicId.ToString()),
            new Claim("password_version", staff.PasswordVersion.ToString())
        };
            claims.AddRange(staff.Roles.Select(role => new Claim("role", role.Role.ToString())));

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var expires = DateTime.UtcNow.AddMinutes(
                _jwtSettings.ExpirationMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
