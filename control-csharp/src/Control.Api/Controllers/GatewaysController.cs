using Control.Api.Mapping;
using Control.Domain.Dtos;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/gateways")]
public sealed class GatewaysController : ControllerBase
{
    private readonly ControlDbContext _db;

    public GatewaysController(ControlDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GatewayDto>>> GetAll()
    {
        var list = await _db.Gateways
            .Include(x => x.Plcs)
            .ThenInclude(x => x.Tags)
            .OrderBy(x => x.Name)
            .ToListAsync();
        return Ok(list.Select(DtoMapping.ToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<GatewayDto>> Get(string id)
    {
        var gateway = await _db.Gateways
            .Include(x => x.Plcs)
            .ThenInclude(x => x.Tags)
            .SingleOrDefaultAsync(x => x.Id == id);
        return gateway is null ? NotFound() : Ok(DtoMapping.ToDto(gateway));
    }
}
