using System.Text.Json;
using System.Text.RegularExpressions;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake
{
    class Converter {

        private static List<ClassModel> ConvertClassesWithBaseLinks(RawFileModel rawFile)
        {
            var superclassNamesMap = new Dictionary <ClassModel, List<string>> ();
            var linkedClasses = new List<ClassModel>();

            // create instances for each class in file (not linked to superclass)
            foreach (var cls in rawFile.ClassesList)
            {
                ClassModel currentClass = new ClassModel();
                currentClass.Name = cls.Name;
                currentClass.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(cls.Lines));
                superclassNamesMap.Add(currentClass,
                    cls.SuperClassesList != null ? cls.SuperClassesList : new List<string>());

                // can only reference classes already defined when assigning, so this should work
                foreach (var obja in cls.ObjectAssignments)
                {
                    var matchedClassConstructor = superclassNamesMap.Keys.FirstOrDefault(x => x.Name.Equals(obja.ClassName), null);
                    if (matchedClassConstructor != null)
                    {
                        ObjectAssignmentModel assignment = new ObjectAssignmentModel
                        {
                            VariableName = obja.VariableName,
                            ClassName = matchedClassConstructor
                        };
                        currentClass.ObjectAssignments.Add(assignment);
                    }
                }
            }

            // replace superclass names with actual instances form the known classes list
            foreach (var clsLink in superclassNamesMap)
            {
                ClassModel currentClass = clsLink.Key;
                foreach (var superclassName in clsLink.Value)
                {
                    foreach (var key in superclassNamesMap.Keys)
                    {
                        if (key.Name.Equals(superclassName))
                        {
                            currentClass.SuperClassesList.Add(key);
                        }
                    }
                }
                linkedClasses.Add(currentClass);
            }

            return linkedClasses;
        }

        private static List<MethodModel> ConvertFileMethods(RawFileModel rawFile, String relativePath, List<ClassModel> fileClasses)
        {
            var fileMethods = new List<MethodModel>();
            // map file methods
            foreach (var mth in rawFile.MethodsList)
            {
                MethodModel currentMethod = new MethodModel();
                currentMethod.RelativePath = relativePath;
                currentMethod.Name = mth.Name;
                currentMethod.TotalNumberOfSubCalls = mth.SubCallsList.Count;
                currentMethod.CyclomaticComplexity = mth.CyclomaticComplexity;
                currentMethod.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(mth.Lines));
                currentMethod.Parent = mth.Parent;
                currentMethod.DecoratorsList = mth.DecoratorsList;
                currentMethod.ArgumentsList = new List<ArgumentModel>();
                
                // map arguments
                foreach (var methodArg in mth.ArgumentsList)
                {
                    ArgumentModel mappedArg = new ArgumentModel();
                    mappedArg.Name = methodArg.Name;
                    // see if there's a matching class instance
                    var matchedArgType = fileClasses.Find(x => x.Name.Equals(methodArg.Annotation.Replace("self.", "")));
                    // if not matching instance, just use string names
                    mappedArg.Annotation = matchedArgType != null ? matchedArgType : methodArg.Annotation;
                    
                    currentMethod.ArgumentsList.Add(mappedArg);
                }
                
                fileMethods.Add(currentMethod);
            }
            
            // map file method subcalls to other file methods
            foreach (var mth in rawFile.MethodsList)
            {
                var currentMethod = fileMethods.Find(x => x.Name.Equals(mth.Name));
                if (currentMethod == null) continue;
                foreach (var subcall in mth.SubCallsList)
                {
                    var fileMethod = fileMethods.Find(x => x.Name.Equals(subcall));
                    if (fileMethod != null)
                    {
                        currentMethod.LocalSubCallsList.Add(fileMethod);
                    }
                }
            }
            
            // TODO: map file method subcalls to method from imports lists
            return fileMethods;
        }

        private static void ConvertClassMethods(RawFileModel rawFile, String relativePath, List<ClassModel> classes, List<MethodModel> fileMethods)
        {
            // map class methods
            foreach (var cls in rawFile.ClassesList)
            {
                var linkedClass = classes.Find(x => x.Name.Equals(cls.Name));
                if (linkedClass == null) continue;
                
                // add default __init__ method if none is defined
                if (cls.MethodsList.Find(x => x.Name.Equals("__init__")) == null)
                {
                    // see if we can infer the init method definition from a base class
                    if (linkedClass.SuperClassesList.Count != 0)
                    {
                        if (linkedClass.SuperClassesList[0].Name.Equals(cls.SuperClassesList[0]))
                        {
                            var baseClass = classes.Find(x => x.Name == cls.SuperClassesList[0]);
                            linkedClass.MethodsList.Add(baseClass.MethodsList.Find(x => x.Name == "__init__"));
                        }
                    }
                    
                    // if no definition, set the stats for the default init method
                    else
                    {
                        MethodModel initMethod = new MethodModel();
                        initMethod.Name = "__init__";
                        initMethod.TotalNumberOfSubCalls = 0;
                        initMethod.RelativePath = relativePath;
                        initMethod.CyclomaticComplexity = 1;
                        initMethod.Lines = new LinesOfCode();
                        initMethod.Parent = cls.Name;
                        initMethod.DecoratorsList = new List<string>();
                        initMethod.ArgumentsList = new List<ArgumentModel>();
                        linkedClass.MethodsList.Add(initMethod);
                    }
                }
                
                // map class methods
                foreach (var mth in cls.MethodsList)
                {
                    MethodModel currentMethod = new MethodModel();
                    currentMethod.Name = mth.Name;
                    mth.SubCallsList.RemoveAll(x => x == "error_parsing_in_Attr_node");
                    currentMethod.TotalNumberOfSubCalls = mth.SubCallsList.Count;
                    currentMethod.RelativePath = relativePath;
                    currentMethod.CyclomaticComplexity = mth.CyclomaticComplexity;
                    currentMethod.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(mth.Lines));
                    currentMethod.Parent = mth.Parent;
                    currentMethod.DecoratorsList = mth.DecoratorsList;
                    currentMethod.ArgumentsList = new List<ArgumentModel>();
            
                    // map arguments
                    foreach (var methodArg in mth.ArgumentsList)
                    {
                        ArgumentModel mappedArg = new ArgumentModel();
                        mappedArg.Name = methodArg.Name;
                        // see if there's a matching class instance
                        var matchedArgType = classes.Find(x => x.Name.Equals(methodArg.Annotation.Replace("self.", "")));
                        // if not matching instance, just use string names
                        mappedArg.Annotation = matchedArgType != null ? matchedArgType : methodArg.Annotation;
                
                        currentMethod.ArgumentsList.Add(mappedArg);
                    }
                    
                    // map method subcalls
                    foreach (var subcall in mth.SubCallsList)
                    {
                        // check if call is to a class' init method
                        var potentialClassNameMatch = classes.Find(x => x.Name.Equals(subcall));
                        if (potentialClassNameMatch != null)
                        {
                            currentMethod.LocalSubCallsList.Add(potentialClassNameMatch.MethodsList.Find(x => x.Name == "__init__"));
                            continue;
                        }
                        
                        // check file methods
                        var fileMethod = fileMethods.Find(x => x.Name.Equals(subcall));
                        if (fileMethod != null)
                        {
                            currentMethod.LocalSubCallsList.Add(fileMethod);
                            continue;
                        }

                        var parentClass = classes.Find(x => x.Name.Equals(mth.Parent));
                        // check if method is defined within a class
                        if (parentClass != null)
                        {
                            // check if method call uses an attribute
                            if (subcall.StartsWith("self")){
                                foreach (var assignment in parentClass.ObjectAssignments)
                                {
                                    var pattern = @"self\." + assignment.VariableName + @"\.";
                                    if (Regex.IsMatch(subcall, pattern))
                                    {
                                        var methodName = Regex.Split(subcall, pattern)[1];
                                        var methodInstance = assignment.ClassName.MethodsList.Find(x => x.Name.Equals(methodName));
                                        if (methodInstance != null)
                                        {
                                            currentMethod.LocalSubCallsList.Add(methodInstance);
                                        }
                                    }
                                }
                            }
                            
                            // check if method call uses a parameter or is a static call
                            else if (Regex.IsMatch(subcall, @".*\..*"))
                            {
                                var argument = currentMethod.ArgumentsList.Find(x =>
                                    x.Name.Equals(Regex.Split(subcall, @"\.")[0]));
                                if (argument != null)
                                {
                                    if (argument.GetType() == typeof(ClassModel))
                                    {
                                        var methodInstance = ((ClassModel)argument.Annotation).MethodsList.Find(x =>
                                            x.Name.Equals(Regex.Split(subcall, @"\.")[1]));
                                        if (methodInstance != null)
                                        {
                                            currentMethod.LocalSubCallsList.Add(methodInstance);
                                        }
                                    }
                                }

                                else
                                {
                                    var classInstance = classes.Find(x =>
                                        x.Name.Equals(Regex.Split(subcall, @"\.")[0]));
                                    
                                    if (classInstance != null)
                                    {
                                        var methodInstance = classInstance.MethodsList.Find(x =>
                                            x.Name.Equals(Regex.Split(subcall, @"\.")[1]));
                                        if (methodInstance != null)
                                        {
                                            currentMethod.LocalSubCallsList.Add(methodInstance);
                                        }
                                    }
                                }
                            }
                        }
                    }
            
                    linkedClass.MethodsList.Add(currentMethod);
                }
            }
        }

        private static void UpdateFileMethodSubcallsList(RawFileModel rawFile, List<ClassModel> fileClasses, List<MethodModel> currentFileMethods)
        {
            foreach (var mth in rawFile.MethodsList)
            {
                var currentMethod = currentFileMethods.Find(x => x.Name.Equals(mth.Name));
                if (currentMethod == null) continue;

                foreach (var subcall in mth.SubCallsList)
                {
                    // check if call is to a class' init method
                    var potentialClassNameMatch = fileClasses.Find(x => x.Name.Equals(subcall));
                    if (potentialClassNameMatch != null)
                    {
                        currentMethod.LocalSubCallsList.Add(potentialClassNameMatch.MethodsList.Find(x => x.Name == "__init__"));
                        continue;
                    }
                    
                    // check if method call uses a parameter or is a static call
                    if (Regex.IsMatch(subcall, @".*\..*"))
                    {
                        var callString = Regex.Split(subcall, @"\.");
                        var argument = currentMethod.ArgumentsList.Find(x =>
                            x.Name.Equals(callString[0]));
                        if (argument != null)
                        {
                            if (argument.Annotation.GetType() == typeof(ClassModel))
                            {
                                var methodInstance = ((ClassModel)argument.Annotation).MethodsList.Find(x =>
                                    x.Name.Equals(Regex.Split(subcall, @"\.")[1]));
                                if (methodInstance != null)
                                {
                                    currentMethod.LocalSubCallsList.Add(methodInstance);
                                }
                            }
                        }
                        
                        else
                        {
                            var classInstance = fileClasses.Find(x =>
                                x.Name.Equals(Regex.Split(subcall, @"\.")[0]));
                                    
                            if (classInstance != null)
                            {
                                var methodInstance = classInstance.MethodsList.Find(x =>
                                    x.Name.Equals(Regex.Split(subcall, @"\.")[1]));
                                if (methodInstance != null)
                                {
                                    currentMethod.LocalSubCallsList.Add(methodInstance);
                                }
                            }
                        }
                    }
                }
            }
        }

        private static ProjectComponent findInternalDependency(String importString, List<FolderModel> projectFolders)
        {
            // see if import is an entire internal package
            var package = projectFolders.Find(x => x.PackageName == importString);
            if (package != null)
            {
                return package;
            }
            
            
            // see if import is a file from an internal package
            if (!importString.Contains("."))
            {
                return null;
            }
            var containingComponentName = importString.Substring(0, importString.LastIndexOf("."));
            package = projectFolders.Find(x => x.PackageName == containingComponentName);
            if (package != null)
            {
                var importedFile = package.FilesList.Find(x => x.Name == importString.Substring(importString.LastIndexOf(".")+1) + ".py");
                if (importedFile != null)
                {
                    return importedFile;
                }
            }
            
            // maybe import a class/function/constant from a module
            if (!containingComponentName.Contains("."))
            {
                return null;
            }
            var containingPackageName = containingComponentName.Substring(0, containingComponentName.LastIndexOf("."));
            package = projectFolders.Find(x => x.PackageName == containingPackageName);
            if (package != null)
            {
                var importedFile = package.FilesList.Find(x => x.Name == containingComponentName.Substring(containingComponentName.LastIndexOf(".")+1) + ".py");
                if (importedFile != null)
                {
                    return importedFile;
                }
            }

            return null;
        }
        
        static void Main(string[] args)
        {
            string path = args[0];
            if (File.Exists(path))
            {
                string result = File.OpenText(path).ReadToEnd();
                RawProjectModel rawProject = JsonSerializer.Deserialize<RawProjectModel>(result);

                ProjectModel project = new ProjectModel();
                project.Name = rawProject.Name;
                project.FoldersList = new List<FolderModel>();

                foreach (var rawFolder in rawProject.FoldersList)
                {
                    FolderModel folder = new FolderModel();
                    folder.RelativePath = rawFolder.RelativePath + "/";
                    folder.DirectoryName = rawFolder.DirectoryName;
                    folder.FilesList = new List<FileModel>();

                    if (rawFolder.FilesList.Find(x => x.Name == "__init__.py") != null)
                    {
                        folder.IsPackage = true;
                        if (folder.RelativePath == ".")
                        {
                            folder.PackageName = project.Name;
                        }

                        folder.PackageName = folder.RelativePath.Replace(".", "").Replace("/", ".");
                        folder.PackageName = folder.PackageName.Remove(folder.PackageName.Length - 1);
                    }

                    foreach (var rawFile in rawFolder.FilesList)
                    {
                        FileModel file = new FileModel();
                        file.Name = rawFile.Name;
                        file.RelativePath = folder.RelativePath + file.Name;

                        List<ClassModel> classes = ConvertClassesWithBaseLinks(rawFile);
                        List<MethodModel> fileMethods = ConvertFileMethods(rawFile, file.RelativePath, classes);
                        ConvertClassMethods(rawFile, file.RelativePath, classes, fileMethods);
                        UpdateFileMethodSubcallsList(rawFile, classes, fileMethods);

                        file.ImportsList = rawFile.ImportsList;
                        file.externalDependencies = new List<ExternalDependency>();
                        file.internalDependencies = new List<InternalDependency>();
                        file.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(rawFile.Lines));
                        file.MethodsList = fileMethods;
                        file.ClassesList = classes;
                        
                        folder.FilesList.Add(file);
                    }
                    
                    project.FoldersList.Add(folder);
                }
                
                // create results directory if it does not exists
                var projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
                Directory.CreateDirectory($"{projectDirectory}/results");
                
                // create file linking results CSV
                var fileLinksStream = File.Create($"{projectDirectory}/results/file_links.csv");
                var csvContentFileLinks = "File, Total Lines, Lines Of Code, Commented Lines, Docstring Lines, Empty Lines, Internal Dependencies, External Dependencies\n";
                
                // create method statistics results CSV
                var methodStatsStream = File.Create($"{projectDirectory}/results/method_stats.csv");
                var csvContentMethodStats = "Method Name, Relative Path, Parent Class, Cyclomatic Complexity, Total Lines, Lines Of Code, Commented Lines, Docstring Lines, Empty Lines, Total No. Of Subcalls, No. Of Local Subcalls\n";
                
                // map file dependencies
                foreach (var dir in project.FoldersList)
                {
                    foreach (var file in dir.FilesList)
                    {
                        var internalDepsPathList = "";
                        var externalDepsPathList = "";
                        foreach (var import in file.ImportsList)
                        {
                            // check if it's external or internal
                            var linkedImport = findInternalDependency(import, project.FoldersList);
                            if (linkedImport != null)
                            {
                                var dependency = new InternalDependency();
                                dependency.Name = import;
                                dependency.Source = linkedImport;
                                file.internalDependencies.Add(dependency);
                                internalDepsPathList += (dependency.Source.RelativePath + " | ");
                            }
                            else
                            {
                                var depedency = new ExternalDependency();
                                depedency.Name = import;
                                file.externalDependencies.Add(depedency);
                                externalDepsPathList += (depedency.Name + " | ");
                            }
                        }

                        if (internalDepsPathList.Length != 0)
                        {
                            internalDepsPathList = internalDepsPathList.Remove(internalDepsPathList.Length - 2);

                        }
                        
                        if (externalDepsPathList.Length != 0)
                        {
                            externalDepsPathList = externalDepsPathList.Remove(externalDepsPathList.Length - 2);

                        }
                        
                        // build file method stats CSV content
                        foreach (var method in file.MethodsList)
                        {
                            var parent = method.Parent != "*" ? method.Parent : "N/A";
                            var fullMethodName = method.RelativePath.Replace("/", ".").Replace(".py", ".") + method.Name;
                            csvContentMethodStats +=
                                $"{fullMethodName}, {method.RelativePath}, {parent}, {method.CyclomaticComplexity}, {method.Lines.LinesTotal}, {method.Lines.LinesCoded}, {method.Lines.LinesCommented}, {method.Lines.linesDocs}, {method.Lines.LinesEmpty}, {method.TotalNumberOfSubCalls}, {method.LocalSubCallsList.Count}\n";
                        }

                        // build class method stats CSV content
                        foreach (var cls in file.ClassesList)
                        {
                            foreach (var method in cls.MethodsList)
                            {
                                var parent = method.Parent != "*" ? method.Parent : "N/A";
                                var fullMethodName = method.RelativePath.Replace("/", ".").Replace(".py", ".") + $"{parent}." + method.Name;
                                csvContentMethodStats +=
                                    $"{fullMethodName}, {method.RelativePath}, {parent}, {method.CyclomaticComplexity}, {method.Lines.LinesTotal}, {method.Lines.LinesCoded}, {method.Lines.LinesCommented}, {method.Lines.linesDocs}, {method.Lines.LinesEmpty}, {method.TotalNumberOfSubCalls}, {method.LocalSubCallsList.Count}\n";
                            }
                        }
                        
                        // build file links CSV content
                        csvContentFileLinks += $"{file.RelativePath}, {file.Lines.LinesTotal}, {file.Lines.LinesCoded}, {file.Lines.LinesCommented}, {file.Lines.linesDocs}, {file.Lines.LinesEmpty}, {internalDepsPathList}, {externalDepsPathList}\n";
                    }
                }
                
                // write file links
                using (StreamWriter writer = new StreamWriter(fileLinksStream))
                {
                    writer.Write(csvContentFileLinks);
                }
                
                // write method stats
                using (StreamWriter writer = new StreamWriter(methodStatsStream))
                {
                    writer.Write(csvContentMethodStats);
                }
            }
            else
            {
                throw new ArgumentException("Invalid file path!");
            }
        }
    }
}