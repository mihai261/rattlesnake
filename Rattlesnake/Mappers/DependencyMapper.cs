using Rattlesnake.Models;

namespace Rattlesnake.Mappers;

public class DependencyMapper
{
    public static (InternalProjectComponent?, ClassModel?) FindInternalDependency(String importString, List<FolderModel> projectFolders)
    {
        // see if import is an entire internal package
        var package = projectFolders.Find(x => x.PackageName == importString);
        if (package != null)
        {
            return (package, null);
        }

        // see if it's a relative import
        package = projectFolders.Find(x => x.PackageName.EndsWith(importString));
        if (package != null)
        {
            return (package, null);
        }
        
        // see if import is a file from an internal package
        if (!importString.Contains("."))
        {
            return (null, null);
        }
        var importedEntityName = importString.Substring(importString.LastIndexOf(".") + 1);
        var containingPackageName = importString.Substring(0, importString.LastIndexOf("."));
        package = projectFolders.Find(x => x.PackageName == containingPackageName);
        
        // see if it's relative import (same as above)
        if (package == null)
        {
            package = projectFolders.Find(x => x.PackageName.EndsWith(importString));
        }
        
        if (package != null)
        {
            var importedFile = package.FilesList.Find(x => x.Name == importString.Substring(importString.LastIndexOf(".")+1) + ".py");
            if (importedFile != null)
            {
                return (importedFile, null);
            }
        }
        
        // maybe import a class/function/constant from a module
        if (!containingPackageName.Contains("."))
        {
            return (null, null);
        }
        
        var importedFileName = containingPackageName.Substring(containingPackageName.LastIndexOf(".") + 1);
        containingPackageName = containingPackageName.Substring(0, containingPackageName.LastIndexOf("."));
        package = projectFolders.Find(x => x.PackageName == containingPackageName);
        
        // see if it's relative import (same as above)
        if (package == null)
        {
            package = projectFolders.Find(x => x.PackageName.EndsWith(importString));
        }
        
        if (package != null)
        {
            var importedFile = package.FilesList.Find(x => x.Name == importedFileName + ".py");
            if (importedFile != null)
            {
                // see if it imports whole class
                var importedClassInstance = importedFile.ClassesList.Find(x => x.Name == importedEntityName);

                // if no class found, it will return null regardless (so it works like the other cases)
                return (importedFile, importedClassInstance);
            }
        }

        return (null, null);
    }
}