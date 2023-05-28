using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawFolderModel
{
    [JsonPropertyName("path")]
    public String RelativePath { get; set; }
    
    [JsonPropertyName("name")]
    public String DirectoryName { get; set; }
    
    [JsonPropertyName("files")]
    public List<RawFileModel> FilesList { get; set; }
}