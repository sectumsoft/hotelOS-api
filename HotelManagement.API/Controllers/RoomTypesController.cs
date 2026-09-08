using HotelManagement.Application.Common.Models;
using HotelManagement.Application.Features.RoomTypes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HotelManagement.API.Controllers;

public class SaveRoomTypeRequest
{
    public string Name { get; set; } = string.Empty;
}

[Authorize]
[ApiController]
[Route("api/roomtypes")]
public class RoomTypesController : ControllerBase
{
    private readonly IMediator _mediator;
    public RoomTypesController(IMediator mediator) { _mediator = mediator; }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoomTypeDto>>>> GetAll()
        => Ok(ApiResponse<List<RoomTypeDto>>.Ok(await _mediator.Send(new GetRoomTypesQuery())));

    [HttpPost]
    public async Task<ActionResult<ApiResponse<Guid>>> Create([FromBody] SaveRoomTypeRequest request)
    {
        var id = await _mediator.Send(new SaveRoomTypeCommand(null, request.Name));
        return Ok(ApiResponse<Guid>.Ok(id, "Room type added"));
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiResponse<Guid>>> Update(Guid id, [FromBody] SaveRoomTypeRequest request)
    {
        var savedId = await _mediator.Send(new SaveRoomTypeCommand(id, request.Name));
        return Ok(ApiResponse<Guid>.Ok(savedId, "Room type updated"));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
    {
        var ok = await _mediator.Send(new DeleteRoomTypeCommand(id));
        if (!ok) return NotFound(ApiResponse<bool>.Fail("Room type not found"));
        return Ok(ApiResponse<bool>.Ok(true, "Room type deleted"));
    }
}
