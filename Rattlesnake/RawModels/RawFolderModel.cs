using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawFolderModel
{
    [JsonPropertyName("name")]
    public String RelativePath { get; set; }
    
    [JsonPropertyName("files")]
    public List<RawFileModel> FilesList { get; set; }
}