using System.Text.Json;
using Rattlesnake.Linkers.Models;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake.Linkers;

public class SuperClassesLinker
{
    public List<ClassModel> ClassesList { get; set; }
    public List<SuperClassLink> Links { get; set; }
    
    public SuperClassesLinker(RawProjectModel projectModel)
    {
        ClassesList = new List<ClassModel>();
        Links = new List<SuperClassLink>();
        
        FindProjectClasses(projectModel);
        LinkProjectClasses(projectModel);
    }

    private void FindProjectClasses(RawProjectModel rawProject)
    {
        foreach (var folder in rawProject.FoldersList)
        {
            foreach (var file in folder.FilesList)
            {
                foreach (var rawClass in file.ClassesList)
                {
                    ClassModel cls = new ClassModel();
                    cls.Name = rawClass.Name;
                    cls.MethodsList = JsonSerializer.Deserialize<List<MethodModel>>(JsonSerializer.Serialize(rawClass.MethodsList));
                    ClassesList.Add(cls);
                }
            }
        }
    }

    private void LinkProjectClasses(RawProjectModel rawProject)
    {
        foreach (var folder in rawProject.FoldersList)
        {
            foreach (var file in folder.FilesList)
            {
                foreach (var rawClass in file.ClassesList)
                {
                    if (rawClass.SuperClassesList.Any())
                    {
                        SuperClassLink link = new SuperClassLink();
                        
                        ClassModel currentClass = new ClassModel();
                        currentClass.Name = rawClass.Name;
                        currentClass.MethodsList = JsonSerializer.Deserialize<List<MethodModel>>(JsonSerializer.Serialize(rawClass.MethodsList));
                        link.ExtendingClass = currentClass;

                        foreach (var cls in ClassesList)
                        {
                            if (rawClass.SuperClassesList.Contains(cls.Name))
                            {
                                link.SuperClassesList.Add(cls);
                            }
                        }
                        
                        if(link.SuperClassesList.Any()) Links.Add(link);
                    }
                }
            }
        }
    }
}