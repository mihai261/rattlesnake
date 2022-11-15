using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class MethodModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("decoratos")]
    public List<String> DecoratorsList { get; set; }
    
    [JsonPropertyName("args")]
    public List<ArgumentModel> ArgumentsList { get; set; }
    
    [JsonPropertyName("sub_calls")]
    public List<String> SubCallsList { get; set; }
}