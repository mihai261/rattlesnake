using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class FileModel : InternalProjectComponent
{
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    public List<String> ImportsList { get; set; }
    public HashSet<InternalDependency> InternalDependencies { get; set; }
    public HashSet<NamedProjectComponent> ExternalDependencies { get; set; }
    public HashSet<InternalDependencyRelation> DependencyRelations { get; set; }
    public HashSet<ExternalDependencyRelation> ExternalDependencyRelations { get; set; }
    
    public List<ClassModel> ClassesList { get; set; }
    
    public List<ClassModel> ImportedClassesList { get; set; }
    public List<ExternalNamedEntity> ImportedExternalNames { get; set; }
    
    [JsonPropertyName("methods")]
    public List<MethodModel> MethodsList { get; set; }

    public FileModel(){
        ExternalDependencies = new HashSet<NamedProjectComponent>();
        InternalDependencies = new HashSet<InternalDependency>();
        DependencyRelations = new HashSet<InternalDependencyRelation>();
        ExternalDependencyRelations = new HashSet<ExternalDependencyRelation>();
        ImportsList = new List<string>();
        ClassesList = new List<ClassModel>();
        ImportedClassesList = new List<ClassModel>();
        ImportedExternalNames = new List<ExternalNamedEntity>();
        MethodsList = new List<MethodModel>();
    }
}