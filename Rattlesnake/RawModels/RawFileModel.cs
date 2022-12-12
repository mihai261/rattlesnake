using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawFileModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public RawLinesOfCode Lines { get; set; }
    
    [JsonPropertyName("imports")]
    public List<String> ImportsList { get; set; }
    
    [JsonPropertyName("classes")]
    public List<RawClassModel> ClassesList { get; set; }
    
    [JsonPropertyName("methods")]
    public List<RawMethodModel> MethodsList { get; set; }
}