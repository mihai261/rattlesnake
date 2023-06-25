using System.Text.Json.Serialization;

namespace Rattlesnake.LinkedModels;

public class MethodModel : InternalProjectComponent
{
    public bool IsDefault { get; set; }
    public int TotalNumberOfSubCalls { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    public int CyclomaticComplexity { get; set; }
    public String Parent { get; set; }
    public FileModel ContainingFile { get; set; }
    public List<String> DecoratorsList { get; set; }
    public List<MethodModel> LocalSubCallsList { get; set; }
    public List<MethodModel> InternalSubCallsList { get; set; }
    public List<ExternalNamedEntity> ExternalSubCallsList { get; set; }
    public List<String> UnknownSubCallList { get; set; }

    public MethodModel()
    {
        IsDefault = false;
        DecoratorsList = new List<string>();
        LocalSubCallsList = new List<MethodModel>();
        InternalSubCallsList = new List<MethodModel>();
        ExternalSubCallsList= new List<ExternalNamedEntity>();
        UnknownSubCallList = new List<String>();
    }
}