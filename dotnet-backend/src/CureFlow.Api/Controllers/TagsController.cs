using CureFlow.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CureFlow.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController : ControllerBase
{
    private readonly ITagService _tags;

    public TagsController(ITagService tags) => _tags = tags;

    [HttpGet]
    [Authorize(Policy = "Permission:Patient.View")]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await _tags.ListAsync(ct));
}
