using Rattlesnake.Models;

namespace Rattlesnake.Linkers.Models;

public class SuperClassLink
{
    public List<ClassModel> SuperClassesList { get; set; }
    public ClassModel ExtendingClass { get; set; }
    // public FileModel ContainingFile { get; set; }
    // public FolderModel ContainingFolder { get; set; }

    public SuperClassLink()
    {
        SuperClassesList = new List<ClassModel>();
    }
}