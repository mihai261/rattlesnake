using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class FileModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    [JsonPropertyName("imports")]
    public List<String> ImportsList { get; set; }
    
    [JsonPropertyName("classes")]
    public List<ClassModel> ClassesList { get; set; }
    
    [JsonPropertyName("methods")]
    public List<MethodModel> MethodsList { get; set; }
}