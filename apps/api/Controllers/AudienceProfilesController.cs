using Api.Application.Authentication;
using Api.Contracts.Audience;
using Api.Contracts.Responses;
using Api.Domain.Entities;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/audience-profile")]
public sealed class AudienceProfilesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;

    public AudienceProfilesController(
        AppDbContext dbContext,
        CurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<IActionResult> Get(
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.AudienceProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.WorkspaceId == _currentUser.WorkspaceId,
                cancellationToken);

        if (profile is null)
        {
            return NotFound(new ApiErrorResponse
            {
                Error = new ApiError
                {
                    Code = "AUDIENCE_PROFILE_NOT_FOUND",
                    Message = "Audience profile has not been configured."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new ApiResponse<AudienceProfileResponse>
        {
            Data = Map(profile),
            Message = "Audience profile retrieved successfully."
        });
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        UpdateAudienceProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.AudienceProfiles
            .FirstOrDefaultAsync(
                x => x.WorkspaceId == _currentUser.WorkspaceId,
                cancellationToken);

        if (profile is null)
        {
            profile = new AudienceProfile
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _currentUser.WorkspaceId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AudienceProfiles.Add(profile);
        }

        profile.Description = request.Description.Trim();
        profile.Demographics = request.Demographics.Trim();
        profile.Interests = request.Interests.Trim();
        profile.PainPoints = request.PainPoints.Trim();
        profile.Goals = request.Goals.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<AudienceProfileResponse>
        {
            Data = Map(profile),
            Message = "Audience profile saved successfully."
        });
    }

    private static AudienceProfileResponse Map(
        AudienceProfile profile)
    {
        return new AudienceProfileResponse
        {
            Id = profile.Id,
            WorkspaceId = profile.WorkspaceId,
            Description = profile.Description,
            Demographics = profile.Demographics,
            Interests = profile.Interests,
            PainPoints = profile.PainPoints,
            Goals = profile.Goals
        };
    }
}