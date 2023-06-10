using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class FileModel : ProjectComponent
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    [JsonPropertyName("imports")]
    public List<String> ImportsList { get; set; }

    public HashSet<InternalDependency> InternalDependencies { get; set; }
    public HashSet<ExternalDependency> ExternalDependencies { get; set; }
    public HashSet<DependencyRelation> DependencyRelations { get; set; }
    public HashSet<ExternalDependencyRelation> ExternalDependencyRelations { get; set; }
    
    [JsonPropertyName("classes")]
    public List<ClassModel> ClassesList { get; set; }
    
    public List<ClassModel> ImportedClassesList { get; set; }
    public List<ExternalNamedEntity> ImportedExternalNames { get; set; }
    
    [JsonPropertyName("methods")]
    public List<MethodModel> MethodsList { get; set; }

    public FileModel(){
        ExternalDependencies = new HashSet<ExternalDependency>();
        InternalDependencies = new HashSet<InternalDependency>();
        DependencyRelations = new HashSet<DependencyRelation>();
        ExternalDependencyRelations = new HashSet<ExternalDependencyRelation>();
        ImportsList = new List<string>();
        ClassesList = new List<ClassModel>();
        ImportedClassesList = new List<ClassModel>();
        ImportedExternalNames = new List<ExternalNamedEntity>();
        MethodsList = new List<MethodModel>();
    }
}