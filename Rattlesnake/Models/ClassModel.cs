using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    [JsonPropertyName("methods")]
    public List<MethodModel> MethodsList { get; set; }
    
    
    [JsonPropertyName("object_assignments")]
    public List<ObjectAssignmentModel> ObjectAssignments { get; set; }
    
    [JsonIgnore]
    public List<ClassModel> SuperClassesList { get; set; }

    public ClassModel()
    {
        MethodsList = new List<MethodModel>();
        SuperClassesList = new List<ClassModel>();
        ObjectAssignments = new List<ObjectAssignmentModel>();
    }
}