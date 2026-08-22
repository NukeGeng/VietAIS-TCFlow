using System.ComponentModel.DataAnnotations;

namespace FSH.Framework.Infrastructure.Jobs;

public class HangfireOptions
{
    [Required]
    public string UserName { get; set; } = "admin";

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string Route { get; set; } = "/jobs";
}
