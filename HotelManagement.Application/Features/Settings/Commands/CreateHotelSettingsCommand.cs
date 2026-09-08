using MediatR;

namespace HotelManagement.Application.Features.Settings.Commands;

/// <summary>Create-or-update the hotel profile for the current tenant.</summary>
public record CreateHotelSettingsCommand(
    string HotelName,
    string Subdomain,
    string Email,
    string Phone,
    string Address
) : IRequest<Guid>;
