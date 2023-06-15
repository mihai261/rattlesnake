using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ClassModel : InternalProjectComponent
{
    public FileModel ContainingFile { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    public String RelativePath { get; set; }
    public HashSet<MethodModel> MethodsList { get; set; }
    public List<ObjectAssignmentModel> ObjectAssignments { get; set; }
    public List<ClassModel> LocalSuperClassesList { get; set; }
    public List<ClassModel> InternalSuperClassesList { get; set; }
    public List<ExternalNamedEntity> ExternalSuperClassesList { get; set; }

    public ClassModel()
    {
        MethodsList = new HashSet<MethodModel>();
        LocalSuperClassesList = new List<ClassModel>();
        InternalSuperClassesList = new List<ClassModel>();
        ExternalSuperClassesList = new List<ExternalNamedEntity>();
        ObjectAssignments = new List<ObjectAssignmentModel>();
    }
}