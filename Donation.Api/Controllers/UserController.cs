using Donation.Api.Extensions;
using Donation.Api.Middlewares;
using Donation.Api.Models.Common;
using Donation.Api.Models.DTOs;
using Donation.Api.Models.Requests;
using Donation.Core.Enums;
using Donation.Core.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Donation.Api.Controllers;

[ServiceFilter(typeof(ValidateModelFilter))]
[ApiController]
[Route("[controller]")]
public sealed class UserController(IUserService userService, ILogger<UserController> logger) : ControllerBase
{
    [Authorize]
    [HttpGet]
    public async Task<ActionResult<BaseResponse<UserDTO>>> GetCurrentUserAsync()
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!User.TryGetUserId(out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        logger.LogInformation("Fetching current user details for {UserId}", userId);

        var user = await userService.GetByIdAsync(userId);
        var dto = UserDTO.BuildFrom(user!);

        logger.LogInformation("User details fetched for {UserId}", userId);
        return Ok(BaseResponse<UserDTO>.Ok(dto));
    }

    [Authorize]
    [HttpPut]
    public async Task<ActionResult<BaseResponse<object>>> UpdateCurrentUserAsync([FromBody] UpdateUserRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!User.TryGetUserId(out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        logger.LogInformation("Updating user {UserId}: name={Name}, lastname={Lastname}", userId, request.Name, request.Lastname);

        var updated = await userService.UpdateAsync(userId, request.Name, request.Lastname);
        var dto = UserDTO.BuildFrom(updated!);

        logger.LogInformation("User {UserId} updated successfully", userId);
        return Ok(BaseResponse<UserDTO>.Ok(dto));
    }

    [Authorize]
    [HttpDelete]
    public async Task<ActionResult<BaseResponse<object>>> DeleteCurrentUserAsync()
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!User.TryGetUserId(out var userId))
            return Unauthorized(BaseResponse<object>.Fail(GeneralError.Unauthorized));

        logger.LogInformation("Delete request for user {UserId}", userId);

        var result = await userService.DeleteAsync(userId);
        if (!result)
        {
            logger.LogError("Delete failed for user {UserId}", userId);
            return BadRequest(BaseResponse<object>.Fail(GeneralError.Unknown));
        }

        logger.LogInformation("User {UserId} deleted successfully", userId);
        return Ok(BaseResponse<object>.Ok());
    }
}
