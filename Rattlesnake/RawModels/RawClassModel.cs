using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public RawLinesOfCode Lines { get; set; }
    
    [JsonPropertyName("methods")]
    public List<RawMethodModel> MethodsList { get; set; }

    [JsonPropertyName("object_assignments")]
    public List<RawObjectAssignmentModel> ObjectAssignments { get; set; }    
    
    [JsonPropertyName("super_classes")]
    public List<String> SuperClassesList { get; set; }
}