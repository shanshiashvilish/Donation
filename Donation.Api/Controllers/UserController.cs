using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.DTOs;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<BaseResponse<UserDTO>>> Get([FromRoute, Required] Guid id)
    {
        if (id == Guid.Empty || id == null)
        {
            return BadRequest(BaseResponse<object>.Fail(GeneralError.MissingParameter));
        }

        var result = await _userService.GetByIdAsync(id);

        return Ok(BaseResponse<UserDTO>.Ok(UserDTO.BuildFrom(result)));
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<ActionResult<BaseResponse<object>>> Put([FromRoute, Required] Guid id, [FromBody] UpdateUserRequest requst)
    {
        if (id == Guid.Empty || id == null)
        {
            return BadRequest(BaseResponse<object>.Fail(GeneralError.MissingParameter));
        }

        var result = await _userService.UpdateAsync(id, requst.Name, requst.Lastname);

        return Ok(BaseResponse<UserDTO>.Ok(UserDTO.BuildFrom(result)));
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<ActionResult<BaseResponse<object>>> Delete([FromRoute, Required] Guid id)
    {
        if (id == Guid.Empty || id == null)
        {
            return BadRequest(BaseResponse<object>.Fail(GeneralError.MissingParameter));
        }

        var result = await _userService.DeleteAsync(id);

        if (!result)
        {
            return BadRequest(BaseResponse<object>.Fail(GeneralError.Unknown));
        }

        return Ok(BaseResponse<object>.Ok());
    }
}
