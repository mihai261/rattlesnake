using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ObjectAssignmentModel
{
    [JsonPropertyName("variable_name")]
    public String VariableName { get; set; }
    
    [JsonPropertyName("assigned_class")]
    public ClassModel ClassName { get; set; }
}