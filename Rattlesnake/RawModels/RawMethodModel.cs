using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawMethodModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("decoratos")]
    public List<String> DecoratorsList { get; set; }
    
    [JsonPropertyName("args")]
    public List<RawArgumentModel> ArgumentsList { get; set; }
    
    [JsonPropertyName("sub_calls")]
    public List<String> SubCallsList { get; set; }
}