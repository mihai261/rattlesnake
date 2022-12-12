using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels
{
    public class RawProjectModel
    {
        [JsonPropertyName("name")]
        public String Name { get; set; }
    
        [JsonPropertyName("folders")]
        public List<RawFolderModel> FoldersList { get; set; }
    }
}