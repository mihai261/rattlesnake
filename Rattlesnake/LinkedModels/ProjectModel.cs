

namespace Rattlesnake.LinkedModels;

public class ProjectModel
{
    public String Name { get; set; }
    
    public List<FolderModel> FoldersList { get; set; }

    public ProjectModel()
    {
        FoldersList = new List<FolderModel>();
    }
}