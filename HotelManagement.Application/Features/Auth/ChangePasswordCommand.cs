using HotelManagement.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HotelManagement.Application.Features.Auth
{
    public record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword) : IRequest<bool>;

    public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
    {
        private readonly IApplicationDbContext _ctx;
        private readonly ICurrentUserService _currentUser;

        public ChangePasswordCommandHandler(
            IApplicationDbContext ctx,
            ICurrentUserService currentUser)
        {
            _ctx = ctx;
            _currentUser = currentUser;
        }

        public async Task<bool> Handle(ChangePasswordCommand req, CancellationToken ct)
        {
            var user = await _ctx.Users
    .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId, ct);
            if (user == null)
                throw new Exception("User not found");

            // 🔒 verify current password
            if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                throw new Exception("Current password is incorrect");

            // 🔒 hash new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);

            await _ctx.SaveChangesAsync(ct);

            return true;
        }
    }
}
