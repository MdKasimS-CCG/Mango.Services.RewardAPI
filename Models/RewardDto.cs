using System;

namespace Mango.Services.RewardAPI.Models
{
    public class RewardDto
    {
        public int OrderId { get; set; }
        public DateTime RewardsDate { get; set; }
        public int RewardsActivity { get; set; }
        public int PointsEarned { get; set; }
    }
}
