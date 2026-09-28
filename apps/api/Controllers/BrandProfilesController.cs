using Api.Application.Authentication;
using Api.Contracts.Brand;
using Api.Contracts.Responses;
using Api.Domain.Entities;
using Api.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/brand-profile")]
public sealed class BrandProfilesController : ControllerBase
{
    private readonly AppDbContext _dbContext;
    private readonly CurrentUserService _currentUser;

    public BrandProfilesController(
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
        var profile = await _dbContext.BrandProfiles
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
                    Code = "BRAND_PROFILE_NOT_FOUND",
                    Message = "Brand profile has not been configured."
                },
                TraceId = HttpContext.TraceIdentifier
            });
        }

        return Ok(new ApiResponse<BrandProfileResponse>
        {
            Data = Map(profile),
            Message = "Brand profile retrieved successfully."
        });
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        UpdateBrandProfileRequest request,
        CancellationToken cancellationToken)
    {
        var profile = await _dbContext.BrandProfiles
            .FirstOrDefaultAsync(
                x => x.WorkspaceId == _currentUser.WorkspaceId,
                cancellationToken);

        if (profile is null)
        {
            profile = new BrandProfile
            {
                Id = Guid.NewGuid(),
                WorkspaceId = _currentUser.WorkspaceId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.BrandProfiles.Add(profile);
        }

        profile.Name = request.Name.Trim();
        profile.Description = request.Description.Trim();
        profile.Industry = request.Industry.Trim();
        profile.ToneOfVoice = request.ToneOfVoice.Trim();
        profile.ContentGoals = request.ContentGoals.Trim();
        profile.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<BrandProfileResponse>
        {
            Data = Map(profile),
            Message = "Brand profile saved successfully."
        });
    }

    private static BrandProfileResponse Map(
        BrandProfile profile)
    {
        return new BrandProfileResponse
        {
            Id = profile.Id,
            WorkspaceId = profile.WorkspaceId,
            Name = profile.Name,
            Description = profile.Description,
            Industry = profile.Industry,
            ToneOfVoice = profile.ToneOfVoice,
            ContentGoals = profile.ContentGoals
        };
    }
}