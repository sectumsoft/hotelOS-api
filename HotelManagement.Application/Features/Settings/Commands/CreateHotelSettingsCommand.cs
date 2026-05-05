using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using MediatR;

namespace HotelManagement.Application.Features.Settings.Commands;

public record CreateHotelSettingsCommand(
    string HotelName,
    string Subdomain,
    string Email,
    string Phone,
    string Address
) : IRequest<Guid>;
