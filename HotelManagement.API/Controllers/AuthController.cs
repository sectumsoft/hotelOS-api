using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.Auth;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) { _mediator = mediator; }

    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResultDto>>> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        if (result == null)
            return Unauthorized(ApiResponse<LoginResultDto>.Fail("Invalid email or password"));
        return Ok(ApiResponse<LoginResultDto>.Ok(result, "Login successful"));
    }
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<bool>>> ChangePassword(ChangePasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(ApiResponse<bool>.Ok(result, "Password updated successfully"));
    }
}
