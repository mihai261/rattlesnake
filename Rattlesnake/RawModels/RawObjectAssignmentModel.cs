using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawObjectAssignmentModel
{
    [JsonPropertyName("variable_name")]
    public String VariableName { get; set; }
    
    [JsonPropertyName("class_name")]
    public String ClassName { get; set; }
}