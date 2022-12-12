using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawArgumentModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("annotation")]
    public String Annotation { get; set; }
}