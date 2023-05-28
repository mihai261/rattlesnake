using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ProjectComponent
{
    [JsonPropertyName("path")]
    public String RelativePath { get; set; }
}