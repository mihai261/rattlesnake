using System.Text.Json;
using System.Text.RegularExpressions;
using Rattlesnake.Mappers;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake
{
    class Converter{

        static void Main(string[] args)
        {
            string path = args[0];
            if (File.Exists(path))
            {
                string result = File.OpenText(path).ReadToEnd();
                RawProjectModel rawProject = JsonSerializer.Deserialize<RawProjectModel>(result);

                ProjectModel project = new ProjectModel();
                project.Name = rawProject.Name;

                foreach (var rawFolder in rawProject.FoldersList)
                {
                    FolderModel folder = new FolderModel();
                    folder.RelativePath = rawFolder.RelativePath + "/";
                    folder.DirectoryName = rawFolder.DirectoryName;

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

                        // map classes&methods and their local dependencies 
                        List<ClassModel> classes = ClassMapper.MapClasses(rawFile, file.RelativePath);
                        List<MethodModel> fileMethods = MethodMapper.MapFileMethods(rawFile, file.RelativePath, classes);
                        MethodMapper.MapClassMethods(rawFile, file.RelativePath, classes, fileMethods);
                        MethodMapper.UpdateFileMethodsSubcallsListWithLocalCalls(rawFile, classes, fileMethods);

                        file.ImportsList = rawFile.ImportsList;
                        file.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(rawFile.Lines));
                        file.MethodsList = fileMethods;
                        file.ClassesList = classes;

                        foreach (var cls in file.ClassesList)
                        {
                            cls.ContainingFile = file;
                        }

                        foreach (var mth in file.MethodsList)
                        {
                            mth.ContainingFile = file;
                        }
                        
                        folder.FilesList.Add(file);
                    }
                    
                    project.FoldersList.Add(folder);
                }
                
                // create results directory if it does not exists
                var projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
                Directory.CreateDirectory($"{projectDirectory}/results");
                
                // create file stats results CSV
                var fileStatsStream = File.Create($"{projectDirectory}/results/file_stats.csv");
                var csvContentFileStats = "File, Total Lines, Lines Of Code, Commented Lines, Docstring Lines, Empty Lines, Internal Dependencies, External Dependencies\n";
                
                // create file links results CSV
                var fileLinksStream = File.Create($"{projectDirectory}/results/file_links.csv");
                var csvContentFileLinks = "Source, Destination, No. Of Bases Definitions, No. Of Method Calls\n";
                    
                // create method statistics results CSV
                var methodStatsStream = File.Create($"{projectDirectory}/results/method_stats.csv");
                var csvContentMethodStats = "Method Name, Relative Path, Parent Class, Cyclomatic Complexity, Total Lines, Lines Of Code, Commented Lines, Docstring Lines, Empty Lines, Total No. Of Subcalls, No. Of Local Subcalls, No. Of Internal Subcalls, No. Of External Subcalls\n";
                
                // create class statistics results CSV
                var classStatsStream = File.Create($"{projectDirectory}/results/class_stats.csv");
                var csvContentClassStats = "Class Name, Relative Path, Total Cyclomatic Complexity, Local Bases, Internal Bases, External Bases, Total Lines, Lines Of Code, Commented Lines, Docstring Lines, Empty Lines\n";

                // map non-local dependencies
                foreach (var dir in project.FoldersList)
                {
                    foreach (var file in dir.FilesList)
                    {
                        var internalDepsPathList = "";
                        var internalDepsPathSet = new HashSet<string>();
                        var externalDepsPathList = "";
                        var externalDepsPathSet = new HashSet<string>();
                        // map imports 
                        foreach (var import in file.ImportsList)
                        {
                            // check if it's external or internal
                            var (linkedImport, linkedClass) = DependencyMapper.FindInternalDependency(import, project.FoldersList);
                            if (linkedImport != null)
                            {
                                var dependency = new InternalDependency();
                                dependency.Name = import;
                                dependency.Source = linkedImport;
                                file.InternalDependencies.Add(dependency);
                                var alteredPath = dependency.Source.RelativePath;
                                if (alteredPath.StartsWith($"{project.Name}"))
                                {
                                    alteredPath = alteredPath.Substring(project.Name.Length);
                                }
                                internalDepsPathSet.Add(alteredPath);

                                if (linkedClass != null)
                                {
                                    file.ImportedClassesList.Add(linkedClass);
                                }
                            }
                            else
                            {
                                var depedency = new NamedProjectComponent();
                                depedency.Name = import;
                                file.ExternalDependencies.Add(depedency);

                                if (Regex.IsMatch(import, @".*.\.*"))
                                {
                                    var importedName = import.Substring(import.LastIndexOf(".") + 1);
                                    file.ImportedExternalNames.Add(new ExternalNamedEntity()
                                    {
                                        Name = importedName,
                                        Provider = depedency
                                    });
                                }
                                
                                externalDepsPathSet.Add(depedency.Name);
                            }
                        }

                        foreach (var dep in internalDepsPathSet)
                        {
                            internalDepsPathList += $"{dep} | ";
                        }

                        if (internalDepsPathList.Length != 0)
                        {
                            internalDepsPathList = internalDepsPathList.Remove(internalDepsPathList.Length - 2);

                        }

                        foreach (var dep in externalDepsPathSet)
                        {
                            externalDepsPathList += $"{dep} | ";
                        }
                        
                        if (externalDepsPathList.Length != 0)
                        {
                            externalDepsPathList = externalDepsPathList.Remove(externalDepsPathList.Length - 2);

                        }

                        // update bases list with class instances from other internal modules
                        ClassMapper.MapInternalClassBases(file.ClassesList, file.InternalDependencies.ToList(), rawProject.FoldersList);
                        
                        // update bases list with instances from other external modules
                        ClassMapper.MapExternalClassBases(file.ClassesList, file.ExternalDependencies.ToList(), rawProject.FoldersList, file.ImportedExternalNames);
                        
                        // update file method subcalls to include references to methods from other internal modules
                        MethodMapper.UpdateFileMethodsSubcallsListWithInternalCalls(file.MethodsList, rawProject.FoldersList, file.InternalDependencies.ToList(), file.ImportedClassesList);
                        
                        // update file method subcalls to include references to external names
                        MethodMapper.UpdateFileMethodsSubcallsListWithExternalCalls(file.MethodsList, rawProject.FoldersList, file.ExternalDependencies.ToList(), file.ImportedExternalNames);
                        
                        // update class methods subcalls for each class in the current file
                        MethodMapper.UpdateClassMethodsSubcallsWithInternalCalls(file.ClassesList, rawProject.FoldersList, file.InternalDependencies.ToList(), file.ImportedClassesList);
                        MethodMapper.UpdateClassMethodsSubcallsWithExternalCalls(file.ClassesList, rawProject.FoldersList, file.ExternalDependencies.ToList(), file.ImportedExternalNames);

                        // build file classes stats CSV content
                        foreach (var cls in file.ClassesList)
                        {
                            var fullClassName = cls.RelativePath.Replace("/", ".").Replace(".py", ".") + cls.Name;
                            
                            var totalComplexity = 0;
                            foreach (var method in cls.MethodsList)
                            {
                                totalComplexity += method.CyclomaticComplexity;
                            }

                            var localBasesNamesListStr = "";
                            foreach (var spcls in cls.LocalSuperClassesList)
                            {
                                localBasesNamesListStr += spcls.RelativePath.Replace("/", ".").Replace(".py", ".") + spcls.Name + " | ";
                            }

                            if (localBasesNamesListStr.Length != 0)
                            {
                                localBasesNamesListStr = localBasesNamesListStr.Remove(localBasesNamesListStr.Length - 2);
                            }
                            
                            var internalBasesNamesListStr = "";
                            foreach (var spcls in cls.InternalSuperClassesList)
                            {
                                internalBasesNamesListStr += spcls.RelativePath.Replace("/", ".").Replace(".py", ".") + spcls.Name + " | ";
                            }

                            if (internalBasesNamesListStr.Length != 0)
                            {
                                internalBasesNamesListStr = internalBasesNamesListStr.Remove(internalBasesNamesListStr.Length - 2);
                            }
                            
                            var externalBasesNamesListStr = "";
                            foreach (var spcls in cls.ExternalSuperClassesList)
                            {
                                externalBasesNamesListStr += $"{spcls.Provider.Name}.{spcls.Name} | ";
                            }
                            
                            if (externalBasesNamesListStr.Length != 0)
                            {
                                externalBasesNamesListStr = externalBasesNamesListStr.Remove(externalBasesNamesListStr.Length - 2);
                            }

                            csvContentClassStats +=
                                $"{fullClassName}, {cls.RelativePath.Substring(project.Name.Length)}, {totalComplexity}, {localBasesNamesListStr}, {internalBasesNamesListStr}, {externalBasesNamesListStr}, {cls.Lines.LinesTotal}, {cls.Lines.LinesCoded}, {cls.Lines.LinesCommented}, {cls.Lines.linesDocs}, {cls.Lines.LinesEmpty}\n";
                        }
                        
                        // build file method stats CSV content
                        foreach (var method in file.MethodsList)
                        {
                            var parent = method.Parent != "*" ? method.Parent : "N/A";
                            var fullMethodName = method.RelativePath.Replace("/", ".").Replace(".py", ".") + method.Name;
                            csvContentMethodStats +=
                                $"{fullMethodName}, {method.RelativePath.Substring(project.Name.Length)}, {parent}, {method.CyclomaticComplexity}, {method.Lines.LinesTotal}, {method.Lines.LinesCoded}, {method.Lines.LinesCommented}, {method.Lines.linesDocs}, {method.Lines.LinesEmpty}, {method.TotalNumberOfSubCalls}, {method.LocalSubCallsList.Count}, {method.InternalSubCallsList.Count}, {method.ExternalSubCallsList.Count}\n";
                        }
                        
                        // build class method stats CSV content
                        foreach (var cls in file.ClassesList)
                        {
                            foreach (var method in cls.MethodsList)
                            {
                                var parent = method.Parent != "*" ? method.Parent : "N/A";
                                var fullMethodName = method.RelativePath.Replace("/", ".").Replace(".py", ".") + $"{parent}." + method.Name;
                                csvContentMethodStats +=
                                    $"{fullMethodName}, {method.RelativePath.Substring(project.Name.Length)}, {parent}, {method.CyclomaticComplexity}, {method.Lines.LinesTotal}, {method.Lines.LinesCoded}, {method.Lines.LinesCommented}, {method.Lines.linesDocs}, {method.Lines.LinesEmpty}, {method.TotalNumberOfSubCalls}, {method.LocalSubCallsList.Count}, {method.InternalSubCallsList.Count},  {method.ExternalSubCallsList.Count}\n";
                            }
                        }
                        
                        // build file stats CSV content
                        csvContentFileStats += $"{file.RelativePath.Substring(project.Name.Length)}, {file.Lines.LinesTotal}, {file.Lines.LinesCoded}, {file.Lines.LinesCommented}, {file.Lines.linesDocs}, {file.Lines.LinesEmpty}, {internalDepsPathList}, {externalDepsPathList}\n";
                        
                        // build file links CSV content
                        foreach (var relation in file.DependencyRelations)
                        {
                            csvContentFileLinks +=
                                $"{file.RelativePath.Substring(project.Name.Length)}, {relation.Destination.RelativePath.Substring(project.Name.Length)}, {relation.NumberOfBaseDefinitions}, {relation.NumberOfMethodCalls}\n";
                        }
                        
                        foreach (var relation in file.ExternalDependencyRelations)
                        {
                            csvContentFileLinks +=
                                $"{file.RelativePath.Substring(project.Name.Length)}, {relation.Destination.Name}, {relation.NumberOfBaseDefinitions}, {relation.NumberOfMethodCalls}\n";
                        }
                    }
                }
                
                // write file stats
                using (StreamWriter writer = new StreamWriter(fileStatsStream))
                {
                    writer.Write(csvContentFileStats);
                }
                
                // write file relations
                using (StreamWriter writer = new StreamWriter(fileLinksStream))
                {
                    writer.Write(csvContentFileLinks);
                }
                
                // write method stats
                using (StreamWriter writer = new StreamWriter(methodStatsStream))
                {
                    writer.Write(csvContentMethodStats);
                }
                
                // write class stats
                using (StreamWriter writer = new StreamWriter(classStatsStream))
                {
                    writer.Write(csvContentClassStats);
                }
            }
            else
            {
                throw new ArgumentException("Invalid file path!");
            }
        }
    }
}