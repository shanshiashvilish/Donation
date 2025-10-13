using Donation.Api.Models.DTOs;
using Donation.Api.Models.Requests;
using Donation.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Donation.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CreateUserRequest user)
    {
        if (user == null)
        {
            return BadRequest();
        }

        var result = await _userService.CreateAsync(user.ToEntity());

        return Ok(UserDTO.BuildFrom(result));
    }

    [Authorize]
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

        return Ok(UserDTO.BuildFrom(result));
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Put([FromRoute] Guid id, [FromBody] UpdateUserRequest requst)
    {
        // validate
        // create

        var result = await _userService.UpdateAsync(id, requst.Name, requst.Lastname);

        return Ok(UserDTO.BuildFrom(result));
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        if (id == Guid.Empty || id == null)
        {
            // EXCEPTION: email, name and fullName cannot be null or empty!
            return BadRequest();
        }

        var result = await _userService.DeleteAsync(id);

        if (!result)
        {
            return BadRequest();
        }

        return NoContent();
    }
}
