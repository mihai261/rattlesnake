using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("methods")]
    public List<RawMethodModel> MethodsList { get; set; }
    
    [JsonPropertyName("super_classes")]
    public List<String> SuperClassesList { get; set; }
}