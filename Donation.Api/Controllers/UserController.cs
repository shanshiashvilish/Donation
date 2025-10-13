using Donation.Api.Models.DTOs;
using Donation.Api.Models.Requests;
using Donation.Core.Users;
using Microsoft.AspNetCore.Mvc;

namespace Donation.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(ILogger<UserController> logger, IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] User user)
    {
        //if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(fullName))
        //{
        //    // EXCEPTION: email, name and fullName cannot be null or empty!
        //    return BadRequest();
        //}
        if(user== null)
        {
            return BadRequest();
        }

        var result = await _userService.CreateAsync(user);

        _logger.LogError("123");
        // create

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDTO>> Get([FromRoute] Guid id)
    {
        if (id == Guid.Empty || id == null)
        {
            // EXCEPTION: id cannot be null or empty!
            return BadRequest("id cannot be null or empty!");
        }

        var result = await _userService.GetByIdAsync(id);

        if (result == null)
        {
            return NotFound();
        }

        return Ok(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Put([FromRoute] Guid id, [FromBody] UpdateUserRequest requst)
    {
        // validate
        // create

        return Ok();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        if (id == Guid.Empty || id == null)
        {
            // EXCEPTION: email, name and fullName cannot be null or empty!
            return BadRequest();
        }

        // delete

        return Ok();
    }
}
