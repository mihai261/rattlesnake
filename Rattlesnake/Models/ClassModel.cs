using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    public String RelativePath { get; set; }

    [JsonPropertyName("methods")]
    public HashSet<MethodModel> MethodsList { get; set; }
    
    
    [JsonPropertyName("object_assignments")]
    public List<ObjectAssignmentModel> ObjectAssignments { get; set; }
    
    [JsonIgnore]
    public List<ClassModel> LocalSuperClassesList { get; set; }
    public List<ClassModel> InternalSuperClassesList { get; set; }

    public ClassModel()
    {
        MethodsList = new HashSet<MethodModel>();
        LocalSuperClassesList = new List<ClassModel>();
        InternalSuperClassesList = new List<ClassModel>();
        ObjectAssignments = new List<ObjectAssignmentModel>();
    }
}