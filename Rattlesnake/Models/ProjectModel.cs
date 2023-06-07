using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ProjectModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("folders")]
    public List<FolderModel> FoldersList { get; set; }

    public ProjectModel()
    {
        FoldersList = new List<FolderModel>();
    }
}