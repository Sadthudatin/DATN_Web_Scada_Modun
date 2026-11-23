using Control.Api.Mapping;
using Control.Api.Services;
using Control.Domain.Dtos;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/commands")]
public sealed class CommandsController : ControllerBase
{
    private readonly ControlDbContext _db;
    private readonly CommandService _commands;

    public CommandsController(ControlDbContext db, CommandService commands)
    {
        _db = db;
        _commands = commands;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommandDto>>> GetAll()
    {
        var items = await _db.Commands.Take(500).ToListAsync();
        return Ok(items
            .OrderByDescending(x => x.RequestedAt)
            .Take(200)
            .Select(DtoMapping.ToDto)
            .ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<CommandDto>> Get(string id)
    {
        var command = Guid.TryParse(id, out var guid)
            ? await _db.Commands.SingleOrDefaultAsync(x => x.Id == guid)
            : await _db.Commands.SingleOrDefaultAsync(x => x.CmdId == id);
        return command is null ? NotFound() : Ok(DtoMapping.ToDto(command));
    }

    [Authorize(Roles = "Admin,Engineer,Operator")]
    [HttpPost]
    public async Task<ActionResult<CommandDto>> Create([FromBody] CreateCommandRequest request)
    {
        try
        {
            var command = await _commands.CreateAsync(request, User);
            return Ok(DtoMapping.ToDto(command));
        }
        catch (CommandValidationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
