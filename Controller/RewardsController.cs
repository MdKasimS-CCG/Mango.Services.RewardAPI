using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mango.Services.RewardAPI.Data;
using Mango.Services.RewardAPI.Models.Dto;

namespace Mango.Services.RewardAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RewardsController : ControllerBase
{
    private readonly AppDbContext _db;

    public RewardsController(AppDbContext db) => _db = db;

    // GET api/rewards/user/{userId}/total
    [HttpGet("user/{userId}/total")]
    public async Task<ActionResult<Mango.Services.RewardAPI.Models.Dto.ResponseDto>> GetTotalPoints(string userId)
    {
        var response = new Mango.Services.RewardAPI.Models.Dto.ResponseDto();

        if (string.IsNullOrWhiteSpace(userId))
        {
            response.IsSuccess = false;
            response.Message = "userId is required.";
            return BadRequest(response);
        }

        var total = await _db.Rewards
            .Where(r => r.UserId == userId)
            .SumAsync(r => (int?)r.PointsEarned) ?? 0;

        response.Result = total;
        return Ok(response);
    }

    // GET api/rewards/user/{userId}
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<Mango.Services.RewardAPI.Models.Dto.ResponseDto>> GetRewardsForUser(string userId)
    {
        var response = new Mango.Services.RewardAPI.Models.Dto.ResponseDto();

        if (string.IsNullOrWhiteSpace(userId))
        {
            response.IsSuccess = false;
            response.Message = "userId is required.";
            return BadRequest(response);
        }

        var list = await _db.Rewards
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.RewardsDate)
            .Select(r => new RewardDto
            {
                OrderId = r.OrderId,
                RewardsDate = r.RewardsDate,
                RewardsActivity = r.RewardsActivity,
                PointsEarned = r.PointsEarned
            })
            .ToListAsync();

        response.Result = list;
        return Ok(response);
    }
}
