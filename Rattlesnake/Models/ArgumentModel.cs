using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ArgumentModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("annotation")]
    public String Annotation { get; set; }
}