using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.Settings.Commands;
using HotelManagement.Application.Features.Settings.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;

    public SettingsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("hotelSettings")]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] CreateHotelSettingsCommand command)
    {
        var id = await _mediator.Send(command);
        return Ok(ApiResponse<Guid>.Ok(id, "Settings created successfully"));
    }
    [HttpGet("hotelDetails")]
    public async Task<ActionResult<ApiResponse<HotelSettingsDto>>> Get()
    {
        var result = await _mediator.Send(new GetHotelSettingsQuery());

        if (result == null)
            return Ok(ApiResponse<HotelSettingsDto>.Ok(new HotelSettingsDto()));

        return Ok(ApiResponse<HotelSettingsDto>.Ok(result));
    }
}