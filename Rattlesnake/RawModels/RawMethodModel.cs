using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawMethodModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public RawLinesOfCode Lines { get; set; }
    
    [JsonPropertyName("complexity")]
    public int CyclomaticComplexity { get; set; }
    
    [JsonPropertyName("parent")]
    public String Parent { get; set; }
    
    [JsonPropertyName("decorators")]
    public List<String> DecoratorsList { get; set; }

    [JsonPropertyName("args")]
    public List<RawArgumentModel> ArgumentsList { get; set; }
    
    [JsonPropertyName("sub_calls")]
    public List<String> SubCallsList { get; set; }
}