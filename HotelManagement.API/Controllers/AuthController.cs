using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace HotelManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) { _mediator = mediator; }

    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        if (result == null)
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Invalid email or password"));
        return Ok(ApiResponse<LoginResultDto>.Ok(result, "Login successful"));
    }

    // Was missing [Authorize] — ChangePasswordCommandHandler resolves the caller
    // via ICurrentUserService, which only works when the request actually carries
    // an authenticated identity. Making that an explicit gate here (rather than a
    // side-effect of the handler failing to find a user) is the correct place to
    // enforce it.
    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(ChangePasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<bool>.Ok(result, "Password updated successfully"));
    }
}
