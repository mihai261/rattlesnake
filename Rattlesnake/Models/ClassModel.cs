using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("methods")]
    public List<MethodModel> MethodsList { get; set; }
    
    [JsonPropertyName("super_classes")]
    public List<String> SuperClassesList { get; set; }
}