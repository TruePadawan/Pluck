using System.ComponentModel.DataAnnotations;

namespace Pluck.Api.Security;

public class PluckApiOptions
{
    public const string SectionName = "PluckApi";

    [Required] public string AdminKey { get; set; } = string.Empty;
}