using Control.Api.Mapping;
using Control.Domain.Dtos;
using Control.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Control.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Engineer")]
[Route("api/audit")]
public sealed class AuditController : ControllerBase
{
    private readonly ControlDbContext _db;

    public AuditController(ControlDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditDto>>> Get()
    {
        var items = await _db.AuditLogs.Take(500).ToListAsync();
        return Ok(items
            .OrderByDescending(x => x.Timestamp)
            .Take(300)
            .Select(DtoMapping.ToDto)
            .ToList());
    }
}
