using System.Text.Json;
using System.Text.RegularExpressions;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake.Mappers;

public class ClassMapper
{
    public static List<ClassModel> MapClasses(RawFileModel rawFile, String relativePath) 
        {
            var baseClassesNamesMap = new Dictionary <ClassModel, List<string>> ();
            var linkedClasses = new List<ClassModel>();

            // create instances for each class in file (not linked to superclass)
            foreach (var cls in rawFile.ClassesList)
            {
                ClassModel currentClass = new ClassModel();
                currentClass.Name = cls.Name;
                currentClass.RelativePath = relativePath;
                currentClass.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(cls.Lines));
                baseClassesNamesMap.Add(currentClass,
                    cls.SuperClassesList != null ? new List<String>(cls.SuperClassesList) : new List<string>());

                // can only reference classes already defined when assigning, so this should work
                foreach (var obja in cls.ObjectAssignments)
                {
                    var matchedClassInstance = baseClassesNamesMap.Keys.FirstOrDefault(x => x.Name.Equals(obja.ClassName), null);
                    if (matchedClassInstance != null)
                    {
                        ObjectAssignmentModel assignment = new ObjectAssignmentModel
                        {
                            VariableName = obja.VariableName,
                            Type = matchedClassInstance
                        };
                        currentClass.ObjectAssignments.Add(assignment);
                    }
                }
            }

            // replace superclass names with actual instances form the known classes list
            foreach (var clsLink in baseClassesNamesMap)
            {
                ClassModel currentClass = clsLink.Key;
                foreach (var baseClassName in clsLink.Value)
                {
                    foreach (var key in baseClassesNamesMap.Keys)
                    {
                        if (key.Name.Equals(baseClassName))
                        {
                            // // remove mapped base class from raw list to speed up the next mapping process
                            // rawFile.ClassesList.Find(x => x.Name == currentClass.Name).LocalSuperClassesList
                            //     .Remove(baseClassName);
                            currentClass.LocalSuperClassesList.Add(key);
                        }
                    }
                }
                linkedClasses.Add(currentClass);
            }

            return linkedClasses;
        }

        public static void MapInternalClassBases(List<ClassModel> mappedClasses,
            List<InternalDependency> internalDependencies, List<RawFolderModel> projectFolders)
        {
            foreach (var dir in projectFolders)
            {
                foreach (var file in dir.FilesList)
                {
                    foreach (var cls in file.ClassesList)
                    {
                        var mappedClassInstance = mappedClasses.Find(x => x.Name == cls.Name);
                        if (mappedClassInstance == null) continue;
                        
                        var containingFile = mappedClassInstance.ContainingFile;
                        var dependecyRelations = containingFile.DependencyRelations;

                        foreach (var baseClassName in cls.SuperClassesList)
                        {
                            if (Regex.IsMatch(baseClassName, @".*\..*"))
                            {
                                var moduleName = Regex.Split(baseClassName, @"\.")[0];
                                
                                // see if import of matching internal module exists 
                                var dependency = internalDependencies.Find(x => x.Name == moduleName);
                                if (dependency != null)
                                {
                                    if (dependency.Source.GetType() == typeof(FileModel))
                                    {
                                        var fileDependency = (FileModel)dependency.Source;
                                        var baseClassInstance = fileDependency.ClassesList.Find(x =>
                                            x.Name == Regex.Split(baseClassName, @"\.")[1]);
                                        if (baseClassInstance != null)
                                        {
                                            mappedClassInstance.InternalSuperClassesList.Add(baseClassInstance);
                                            var registeredDependency =
                                                dependecyRelations.FirstOrDefault(
                                                    x => x.Destination.Equals(fileDependency), null);
                                            if (registeredDependency != null)
                                            {
                                                registeredDependency.NumberOfBaseDefinitions += 1;
                                            }
                                            else
                                            {
                                                dependecyRelations.Add(new InternalDependencyRelation()
                                                {
                                                    Destination = fileDependency,
                                                    NumberOfBaseDefinitions = 1,
                                                    NumberOfMethodCalls = 0
                                                });
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // if dependency is like from module1.module2 import module3, then split it by tokens
                                    dependency =  internalDependencies.Find(x => Regex.Split(x.Name, @"\.").Last() == moduleName);
                                    if (dependency != null)
                                    {
                                        if (dependency.Source.GetType() == typeof(FileModel))
                                        {
                                            var fileDependency = (FileModel)dependency.Source;
                                            var baseClassInstance = fileDependency.ClassesList.Find(x =>
                                                x.Name == Regex.Split(baseClassName, @"\.")[1]);
                                            if (baseClassInstance != null)
                                            {
                                                mappedClassInstance.InternalSuperClassesList.Add(baseClassInstance);
                                                var registeredDependency =
                                                    dependecyRelations.FirstOrDefault(
                                                        x => x.Destination.Equals(fileDependency), null);
                                                if (registeredDependency != null)
                                                {
                                                    registeredDependency.NumberOfBaseDefinitions += 1;
                                                }
                                                else
                                                {
                                                    dependecyRelations.Add(new InternalDependencyRelation()
                                                    {
                                                        Destination = fileDependency,
                                                        NumberOfBaseDefinitions = 1,
                                                        NumberOfMethodCalls = 0
                                                    });
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void MapExternalClassBases(List<ClassModel> mappedClasses,
            List<NamedProjectComponent> externalDependencies, List<RawFolderModel> projectFolders,
            List<ExternalNamedEntity> importedNames)
        {
            foreach (var dir in projectFolders)
            {
                foreach (var file in dir.FilesList)
                {
                    foreach (var cls in file.ClassesList)
                    {
                        var mappedClassInstance = mappedClasses.Find(x => x.Name == cls.Name);
                        if (mappedClassInstance == null) continue;
                        
                        var containingFile = mappedClassInstance.ContainingFile;
                        var dependecyRelations = containingFile.ExternalDependencyRelations;


                        foreach (var baseClassName in cls.SuperClassesList)
                        {
                            // see if base name is referencing a directly imported name (e.g. could be method or class init function)
                            var matchingImportedClassName =
                                importedNames.Find(x => x.Name == baseClassName);
                            if (matchingImportedClassName != null)
                            {
                                mappedClassInstance.ExternalSuperClassesList.Add(new ExternalNamedEntity()
                                {
                                    Name = matchingImportedClassName.Name,
                                    Provider = matchingImportedClassName.Provider
                                });
                                
                                var registeredDependency =
                                    dependecyRelations.FirstOrDefault(
                                        x => x.Destination.Equals(matchingImportedClassName.Provider), null);
                                if (registeredDependency != null)
                                {
                                    registeredDependency.NumberOfBaseDefinitions += 1;
                                }
                                else
                                {
                                    dependecyRelations.Add(new ExternalDependencyRelation()
                                    {
                                        Destination = matchingImportedClassName.Provider,
                                        NumberOfBaseDefinitions = 1,
                                        NumberOfMethodCalls = 0
                                    });
                                }
                            }
                            
                            if (Regex.IsMatch(baseClassName, @".*\..*"))
                            {
                                var moduleName = Regex.Split(baseClassName, @"\.")[0];

                                // see if import of matching external module exists 
                                var dependency = externalDependencies.Find(x => x.Name == moduleName);
                                if (dependency != null)
                                {
                                    mappedClassInstance.ExternalSuperClassesList.Add(new ExternalNamedEntity()
                                    {
                                        Name = Regex.Split(baseClassName, @"\.")[1],
                                        Provider = dependency
                                    });
                                    
                                    var registeredDependency =
                                        dependecyRelations.FirstOrDefault(
                                            x => x.Destination.Equals(dependency), null);
                                    if (registeredDependency != null)
                                    {
                                        registeredDependency.NumberOfBaseDefinitions += 1;
                                    }
                                    else
                                    {
                                        dependecyRelations.Add(new ExternalDependencyRelation()
                                        {
                                            Destination = dependency,
                                            NumberOfBaseDefinitions = 1,
                                            NumberOfMethodCalls = 0
                                        });
                                    }
                                }
                                
                                // maybe dependency if brought like from x import y; base = y.Class1 => check for that too
                                dependency = externalDependencies.Find(x =>
                                    Regex.IsMatch(x.Name, @".\." + moduleName + "$"));
                                if (dependency != null)
                                {
                                    mappedClassInstance.ExternalSuperClassesList.Add(new ExternalNamedEntity()
                                    {
                                        Name = Regex.Split(baseClassName, @"\.")[1],
                                        Provider = dependency
                                    });
                                    
                                    var registeredDependency =
                                        dependecyRelations.FirstOrDefault(
                                            x => x.Destination.Equals(dependency), null);
                                    if (registeredDependency != null)
                                    {
                                        registeredDependency.NumberOfBaseDefinitions += 1;
                                    }
                                    else
                                    {
                                        dependecyRelations.Add(new ExternalDependencyRelation()
                                        {
                                            Destination = dependency,
                                            NumberOfBaseDefinitions = 1,
                                            NumberOfMethodCalls = 0
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

}