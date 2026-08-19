using System.ComponentModel.DataAnnotations;

namespace DigiLine.API.Features.Queue
{
    public class CreateWalkInTokenRequest
    {
        public Guid SessionId { get; set; }

        [Required]
        [StringLength(200)]
        public string WalkInName { get; set; } = string.Empty;

        [StringLength(20)]
        public string? PhoneNumber { get; set; }
    }
}
