using System.Text.Json;
using Rattlesnake.Linkers;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake
{
    class Converter {         
        static void Main(string[] args)
        {
            string path = args[0];
            if (File.Exists(path))
            {
                string result = File.OpenText(path).ReadToEnd();
                RawProjectModel rawProject = JsonSerializer.Deserialize<RawProjectModel>(result);
                ProjectModel projectModel = JsonSerializer.Deserialize<ProjectModel>(result);

                SuperClassesLinker superClassesLinker = new SuperClassesLinker(rawProject);

                foreach (var folder in projectModel.FoldersList)
                {
                    foreach (var file in folder.FilesList)
                    {
                        foreach (var cls in file.ClassesList)
                        {
                            foreach (var link in superClassesLinker.Links)
                            {
                                if (cls.Equals(link.ExtendingClass)) cls.SuperClassesList = link.SuperClassesList;
                            }
                        }
                    }
                }
            }
            else
            {
                throw new ArgumentException("Invalid file path!");
            }
        }
    }
}