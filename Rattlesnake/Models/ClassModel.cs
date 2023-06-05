using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    [JsonPropertyName("methods")]
    public HashSet<MethodModel> MethodsList { get; set; }
    
    
    [JsonPropertyName("object_assignments")]
    public List<ObjectAssignmentModel> ObjectAssignments { get; set; }
    
    [JsonIgnore]
    public List<ClassModel> SuperClassesList { get; set; }

    public ClassModel()
    {
        MethodsList = new HashSet<MethodModel>();
        SuperClassesList = new List<ClassModel>();
        ObjectAssignments = new List<ObjectAssignmentModel>();
    }
}