using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class FolderModel
{
    [JsonPropertyName("name")]
    public String RelativePath { get; set; }
    
    [JsonPropertyName("files")]
    public List<FileModel> FilesList { get; set; }
}