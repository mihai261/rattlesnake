using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class FolderModel : InternalProjectComponent
{
    public String DirectoryName { get; set; }
    public Boolean IsPackage { get; set; }
    public String PackageName { get; set; }
    public List<FileModel> FilesList { get; set; }

    public FolderModel()
    {
        FilesList = new List<FileModel>();
    }
}