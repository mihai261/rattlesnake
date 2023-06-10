using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class MethodModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    public bool IsDefault { get; set; }
    
    public int TotalNumberOfSubCalls { get; set; }
    public String RelativePath { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    [JsonPropertyName("complexity")]
    public int CyclomaticComplexity { get; set; }
    
    [JsonPropertyName("parent")]
    public String Parent { get; set; }
    
    public FileModel ContainingFile { get; set; }

    [JsonPropertyName("decorators")]
    public List<String> DecoratorsList { get; set; }
    
    [JsonPropertyName("args")]
    public List<ArgumentModel> ArgumentsList { get; set; }
    
    [JsonPropertyName("sub_calls")]
    public List<MethodModel> LocalSubCallsList { get; set; }
    public List<MethodModel> InternalSubCallsList { get; set; }
    public List<ExternalNamedEntity> ExternalSubCallsList { get; set; }
    public List<String> UnknownSubCallList { get; set; }

    public MethodModel()
    {
        IsDefault = false;
        ArgumentsList = new List<ArgumentModel>();
        DecoratorsList = new List<string>();
        LocalSubCallsList = new List<MethodModel>();
        InternalSubCallsList = new List<MethodModel>();
        ExternalSubCallsList= new List<ExternalNamedEntity>();
        UnknownSubCallList = new List<String>();
    }
}