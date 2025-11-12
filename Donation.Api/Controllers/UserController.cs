using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.DTOs;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;

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

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<BaseResponse<UserDTO>>> GetCurrentUserAsync()
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var sub = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        var result = await _userService.GetByIdAsync(userId);

        return Ok(BaseResponse<UserDTO>.Ok(UserDTO.BuildFrom(result!)));
    }

    [Authorize]
    [HttpPut]
    public async Task<ActionResult<BaseResponse<object>>> UpdateCurrentUserAsync([FromBody] UpdateUserRequest requst)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var sub = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        var result = await _userService.UpdateAsync(userId, requst.Name, requst.Lastname);

        return Ok(BaseResponse<UserDTO>.Ok(UserDTO.BuildFrom(result!)));
    }

    [Authorize]
    [HttpDelete]
    public async Task<ActionResult<BaseResponse<object>>> DeleteCurrentUserAsync()
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var sub = User.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        if (!Guid.TryParse(sub, out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        var result = await _userService.DeleteAsync(userId);

        if (!result)
        {
            return BadRequest(BaseResponse<object>.Fail(GeneralError.Unknown));
        }

        return Ok(BaseResponse<object>.Ok());
    }
}
