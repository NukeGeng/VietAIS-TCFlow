using System.ComponentModel.DataAnnotations;

namespace FSH.Framework.Core.Identity;

public sealed class BootstrapAdminOptions
{
    [Required]
    [MinLength(12)]
    public string Password { get; set; } = string.Empty;
}
