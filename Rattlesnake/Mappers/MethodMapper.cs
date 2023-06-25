using System.Text.Json;
using System.Text.RegularExpressions;
using Rattlesnake.LinkedModels;
using Rattlesnake.RawModels;

namespace Rattlesnake.Mappers;

public class MethodMapper
{
    public static List<MethodModel> MapFileMethods(RawFileModel rawFile, String relativePath, List<ClassModel> mappedClasses)
        {
            var fileMethods = new List<MethodModel>();
            
            // map file methods
            foreach (var mth in rawFile.MethodsList)
            {
                MethodModel currentMethod = new MethodModel
                {
                    RelativePath = relativePath,
                    Name = mth.Name,
                    TotalNumberOfSubCalls = mth.SubCallsList.Count,
                    CyclomaticComplexity = mth.CyclomaticComplexity,
                    Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(mth.Lines)),
                    Parent = mth.Parent,
                    DecoratorsList = mth.DecoratorsList,
                };

                fileMethods.Add(currentMethod);
            }
            
            // map file method subcalls to other file methods
            foreach (var mth in rawFile.MethodsList)
            {
                var currentMethod = fileMethods.Find(x => x.Name.Equals(mth.Name));
                if (currentMethod == null) continue;
                var mappedSubcalls = new List<string>();
                foreach (var subcall in mth.SubCallsList)
                {
                    var fileMethod = fileMethods.Find(x => x.Name.Equals(subcall));
                    if (fileMethod != null)
                    {
                        mappedSubcalls.Add(subcall);
                        currentMethod.LocalSubCallsList.Add(fileMethod);
                    }
                }

                // clear mapped subcalls from raw object to speed up next mapping attempt
                mth.SubCallsList.RemoveAll(x => mappedSubcalls.Contains(x));
            }

            return fileMethods;
        }

        public static void MapClassMethods(RawFileModel rawFile, String relativePath, List<ClassModel> mappedClasses, List<MethodModel> fileMethods)
        {
            foreach (var cls in rawFile.ClassesList)
            {
                var linkedClass = mappedClasses.Find(x => x.Name.Equals(cls.Name));
                if (linkedClass == null) continue;
                
                // add default __init__ method if none is defined
                if (cls.MethodsList.Find(x => x.Name.Equals("__init__")) == null)
                {
                    // see if we can infer the init method definition from a base class
                    if (linkedClass.LocalSuperClassesList.Count != 0)
                    {
                        // check that the first mapped base class is the same as the first raw subclass
                        if (linkedClass.LocalSuperClassesList[0].Name.Equals(cls.SuperClassesList[0]))
                        {
                            var baseClass = mappedClasses.Find(x => x.Name == cls.SuperClassesList[0]);
                            if (baseClass != null)
                            {
                                // all classes should have an init method so (theoretically) no null-pointer exception
                                linkedClass.MethodsList.Add(baseClass.MethodsList.FirstOrDefault(x => x.Name == "__init__", null));
                            }
                        }
                    }
                    
                    // if no definition, set the stats for the default init method
                    else
                    {
                        MethodModel initMethod = new MethodModel
                        {
                            IsDefault = true,
                            ContainingFile = linkedClass.ContainingFile,
                            Name = "__init__",
                            TotalNumberOfSubCalls = 0,
                            RelativePath = relativePath,
                            CyclomaticComplexity = 1,
                            Lines = new LinesOfCode(),
                            Parent = cls.Name
                        };
                        linkedClass.MethodsList.Add(initMethod);
                    }
                }
                
                // map class methods
                foreach (var mth in cls.MethodsList)
                {
                    mth.SubCallsList.RemoveAll(x => x == "error_parsing_in_Attr_node");

                    MethodModel currentMethod = new MethodModel
                    {
                        Name = mth.Name,
                        ContainingFile = linkedClass.ContainingFile,
                        TotalNumberOfSubCalls = mth.SubCallsList.Count,
                        RelativePath = relativePath,
                        CyclomaticComplexity = mth.CyclomaticComplexity,
                        Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(mth.Lines)),
                        Parent = mth.Parent,
                        DecoratorsList = mth.DecoratorsList
                    };

                    List<string> mappedSubcalls = new List<string>();

                    // map method subcalls
                    foreach (var subcall in mth.SubCallsList)
                    {
                        // check if call is to a class' init method
                        var potentialClassNameMatch = mappedClasses.Find(x => x.Name.Equals(subcall));
                        if (potentialClassNameMatch != null)
                        {
                            mappedSubcalls.Add(subcall);
                            currentMethod.LocalSubCallsList.Add(potentialClassNameMatch.MethodsList.FirstOrDefault(x => x.Name == "__init__", null));
                            continue;
                        }
                        
                        // check file methods
                        var fileMethod = fileMethods.Find(x => x.Name.Equals(subcall));
                        if (fileMethod != null)
                        {
                            mappedSubcalls.Add(subcall);
                            currentMethod.LocalSubCallsList.Add(fileMethod);
                            continue;
                        }
                        
                        // check if method is defined within this class (redundant)
                        var parentClass = mappedClasses.Find(x => x.Name.Equals(mth.Parent));
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
                                        var methodInstance = assignment.Type.MethodsList.FirstOrDefault(x => x.Name.Equals(methodName), null);
                                        if (methodInstance != null)
                                        {
                                            mappedSubcalls.Add(subcall);
                                            currentMethod.LocalSubCallsList.Add(methodInstance);
                                        }
                                    }
                                }
                            }
                            
                            // check if method call is a static call
                            else if (Regex.IsMatch(subcall, @".*\..*"))
                            {
                                // static call?
                                var classInstance = mappedClasses.Find(x =>
                                        x.Name.Equals(Regex.Split(subcall, @"\.")[0]));
                                    
                                if (classInstance != null)
                                {
                                    var methodInstance = classInstance.MethodsList.FirstOrDefault(x =>
                                        x.Name.Equals(Regex.Split(subcall, @"\.")[1]), null);
                                    if (methodInstance != null)
                                    {
                                        mappedSubcalls.Add(subcall);
                                        currentMethod.LocalSubCallsList.Add(methodInstance);
                                    }
                                }
                            }
                        }
                    }
                    
                    mth.SubCallsList.RemoveAll(x => mappedSubcalls.Contains(x));

                    linkedClass.MethodsList.Add(currentMethod);
                }
                
                // go through the remaining methods again and see if there are subcalls to other methods defined in parent class
                foreach (var mappedMethod in linkedClass.MethodsList)
                {
                    // check method is part of this class (redundant)
                    if (mappedMethod.Parent == linkedClass.Name)
                    {
                        var rawMethod = cls.MethodsList.Find(x => x.Name == mappedMethod.Name);
                        if (rawMethod == null) continue;
                        foreach (var subcall in rawMethod.SubCallsList)
                        {
                            if (Regex.IsMatch(subcall, @"self/..*"))
                            {
                                var tokenizedSubcall = Regex.Split(subcall, @"\.");
                                // if subcall is split in self and another string, it's a possible match
                                if (tokenizedSubcall.Length == 2)
                                {
                                    var calledMethodName = tokenizedSubcall[1];
                                    var calledMethodInstance =
                                        linkedClass.MethodsList.FirstOrDefault(x => x.Name == calledMethodName, null);
                                    if (calledMethodInstance != null)
                                    {
                                        linkedClass.MethodsList.Add(calledMethodInstance);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void UpdateFileMethodsSubcallsListWithLocalCalls(RawFileModel rawFile, List<ClassModel> mappedClasses, List<MethodModel> fileMethods)
        {
            foreach (var rawFileMethod in rawFile.MethodsList)
            {
                var currentMethod = fileMethods.Find(x => x.Name.Equals(rawFileMethod.Name));
                if (currentMethod == null) continue;

                List<string> mappedSubcalls = new List<string>();

                foreach (var subcall in rawFileMethod.SubCallsList)
                {
                    // check if call is to a class' init method
                    var matchingClassInstance = mappedClasses.Find(x => x.Name.Equals(subcall));
                    if (matchingClassInstance != null)
                    {
                        mappedSubcalls.Add(subcall);
                        currentMethod.LocalSubCallsList.Add(matchingClassInstance.MethodsList.FirstOrDefault(x => x.Name == "__init__", null));
                        continue;
                    }
                    
                    // check if method call uses a parameter or is a static call
                    if (Regex.IsMatch(subcall, @".*\..*"))
                    {
                        var classInstance = mappedClasses.Find(x =>
                            x.Name.Equals(Regex.Split(subcall, @"\.")[0]));
                                
                        if (classInstance != null)
                        {
                            var methodInstance = classInstance.MethodsList.FirstOrDefault(x =>
                                x.Name.Equals(Regex.Split(subcall, @"\.")[1]), null);
                            if (methodInstance != null)
                            {                       
                                mappedSubcalls.Add(subcall);
                                currentMethod.LocalSubCallsList.Add(methodInstance);
                            }
                        }
                    }
                }
                
                // remove all mapped methods from raw model to speed up further mapping attempts
                rawFileMethod.SubCallsList.RemoveAll(x => mappedSubcalls.Contains(x));
            }
        }

        public static void UpdateFileMethodsSubcallsListWithInternalCalls(List<MethodModel> fileMethods,
            List<RawFolderModel> rawFolders, List<InternalDependency> internalDependencies, List<ClassModel> importedClasses)
        {
            foreach (var dir in rawFolders)
            {
                foreach (var rawFile in dir.FilesList)
                {
                    foreach (var rawMethod in rawFile.MethodsList)
                    {
                        var mappedMethod = fileMethods.Find(x => x.Name == rawMethod.Name);
                        if (mappedMethod == null) continue;

                        var containingFile = mappedMethod.ContainingFile;
                        var dependecyRelations = containingFile.DependencyRelations;
                        
                        foreach (var subcall in rawMethod.SubCallsList)
                        {
                            // see if call is to an (directly) imported class' init method
                            var matchingImportedClassInstance =
                                importedClasses.Find(x => x.Name.Equals(subcall));
                            if (matchingImportedClassInstance != null)
                            {
                                mappedMethod.InternalSubCallsList.Add(
                                    matchingImportedClassInstance.MethodsList.FirstOrDefault(
                                        x => x.Name == "__init__",
                                        null));
                                
                                var registeredDependency =
                                    dependecyRelations.FirstOrDefault(
                                        x => x.Destination.Equals(matchingImportedClassInstance.ContainingFile), null);
                                if (registeredDependency != null)
                                {
                                    registeredDependency.NumberOfMethodCalls += 1;
                                }
                                else
                                {
                                    dependecyRelations.Add(new InternalDependencyRelation()
                                    {
                                        Destination = matchingImportedClassInstance.ContainingFile,
                                        NumberOfBaseDefinitions = 0,
                                        NumberOfMethodCalls = 1
                                    });
                                }
                            }
                            
                            
                            
                            if (Regex.IsMatch(subcall, @".*\..*"))
                            {
                                var moduleName = Regex.Split(subcall, @"\.")[0];

                                // see if import of matching internal module exists 
                                var dependency = internalDependencies.Find(x => x.Name == moduleName);
                                if (dependency != null)
                                {
                                    if (dependency.Source.GetType() == typeof(FileModel))
                                    {
                                        var fileDependency = (FileModel)dependency.Source;

                                        // check file methods list
                                        var subcallInstance = fileDependency.MethodsList.Find(x =>
                                            x.Name == Regex.Split(subcall, @"\.")[1]);
                                        if (subcallInstance != null)
                                        {
                                            mappedMethod.InternalSubCallsList.Add(subcallInstance);
                                            
                                            var registeredDependency =
                                                dependecyRelations.FirstOrDefault(
                                                    x => x.Destination.Equals(matchingImportedClassInstance.ContainingFile), null);
                                            if (registeredDependency != null)
                                            {
                                                registeredDependency.NumberOfMethodCalls += 1;
                                            }
                                            else
                                            {
                                                dependecyRelations.Add(new InternalDependencyRelation()
                                                {
                                                    Destination = matchingImportedClassInstance.ContainingFile,
                                                    NumberOfBaseDefinitions = 0,
                                                    NumberOfMethodCalls = 1
                                                });
                                            }
                                            
                                            continue;
                                        }

                                        // check if it's a call to init method of a class
                                        var matchingClassInstance =
                                            fileDependency.ClassesList.Find(x => x.Name.Equals(subcall));
                                        if (matchingClassInstance != null)
                                        {
                                            mappedMethod.InternalSubCallsList.Add(
                                                matchingClassInstance.MethodsList.FirstOrDefault(
                                                    x => x.Name == "__init__",
                                                    null));
                                            
                                            var registeredDependency =
                                                dependecyRelations.FirstOrDefault(
                                                    x => x.Destination.Equals(matchingImportedClassInstance.ContainingFile), null);
                                            if (registeredDependency != null)
                                            {
                                                registeredDependency.NumberOfMethodCalls += 1;
                                            }
                                            else
                                            {
                                                dependecyRelations.Add(new InternalDependencyRelation()
                                                {
                                                    Destination = matchingImportedClassInstance.ContainingFile,
                                                    NumberOfBaseDefinitions = 0,
                                                    NumberOfMethodCalls = 1
                                                });
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    // if dependency is like from module1.module2 import module3, then split it by tokens
                                    dependency = internalDependencies.Find(x =>
                                        Regex.Split(x.Name, @"\.").Last() == moduleName);
                                    if (dependency != null)
                                    {
                                        if (dependency.Source.GetType() == typeof(FileModel))
                                        {
                                            var fileDependency = (FileModel)dependency.Source;
                                            var subcallInstance = fileDependency.MethodsList.Find(x =>
                                                x.Name == Regex.Split(subcall, @"\.")[1]);
                                            if (subcallInstance != null)
                                            {
                                                mappedMethod.InternalSubCallsList.Add(subcallInstance);
                                                
                                                var registeredDependency =
                                                    dependecyRelations.FirstOrDefault(
                                                        x => x.Destination.Equals(matchingImportedClassInstance.ContainingFile), null);
                                                if (registeredDependency != null)
                                                {
                                                    registeredDependency.NumberOfMethodCalls += 1;
                                                }
                                                else
                                                {
                                                    dependecyRelations.Add(new InternalDependencyRelation()
                                                    {
                                                        Destination = matchingImportedClassInstance.ContainingFile,
                                                        NumberOfBaseDefinitions = 0,
                                                        NumberOfMethodCalls = 1
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

        public static void UpdateFileMethodsSubcallsListWithExternalCalls(List<MethodModel> fileMethods, 
            List<RawFolderModel> rawFolders, List<NamedProjectComponent> externalDependencies, List<ExternalNamedEntity> importedNames)
        {
            foreach (var dir in rawFolders)
            {
                foreach (var rawFile in dir.FilesList)
                {
                    foreach (var rawMethod in rawFile.MethodsList)
                    {
                        var mappedMethod = fileMethods.Find(x => x.Name == rawMethod.Name);
                        if (mappedMethod == null) continue;
                        
                        var containingFile = mappedMethod.ContainingFile;
                        var dependecyRelations = containingFile.ExternalDependencyRelations;

                        foreach (var subcall in rawMethod.SubCallsList)
                        {
                            // see if call is to a directly imported name (e.g. could be method or class init function)
                            var matchingImportedClassName =
                                importedNames.Find(x => x.Name == subcall);
                            if (matchingImportedClassName != null)
                            {
                                mappedMethod.ExternalSubCallsList.Add(
                                    new ExternalNamedEntity()
                                    {
                                        Name = matchingImportedClassName.Name,
                                        Provider = matchingImportedClassName.Provider
                                    });
                                
                                var registeredDependency =
                                    dependecyRelations.FirstOrDefault(
                                        x => x.Destination.Equals(matchingImportedClassName.Provider), null);
                                if (registeredDependency != null)
                                {
                                    registeredDependency.NumberOfMethodCalls += 1;
                                }
                                else
                                {
                                    dependecyRelations.Add(new ExternalDependencyRelation()
                                    {
                                        Destination = matchingImportedClassName.Provider,
                                        NumberOfBaseDefinitions = 0,
                                        NumberOfMethodCalls = 1
                                    });
                                }
                            }



                            if (Regex.IsMatch(subcall, @".*\..*"))
                            {
                                var moduleName = Regex.Split(subcall, @"\.")[0];

                                // see if import of matching external module exists 
                                var dependency = externalDependencies.Find(x => x.Name == moduleName);
                                if (dependency != null)
                                {
                                    mappedMethod.ExternalSubCallsList.Add(
                                        new ExternalNamedEntity()
                                        {
                                            Name = Regex.Split(subcall, @"\.")[1],
                                            Provider = dependency
                                        });
                                    
                                    var registeredDependency =
                                        dependecyRelations.FirstOrDefault(
                                            x => x.Destination.Equals(dependency), null);
                                    if (registeredDependency != null)
                                    {
                                        registeredDependency.NumberOfMethodCalls += 1;
                                    }
                                    else
                                    {
                                        dependecyRelations.Add(new ExternalDependencyRelation()
                                        {
                                            Destination = dependency,
                                            NumberOfBaseDefinitions = 0,
                                            NumberOfMethodCalls = 1
                                        });
                                    }
                                }
                                
                                // maybe dependency if brought like from x import y; base = y.Class1 => check for that too
                                dependency = externalDependencies.Find(x =>
                                    Regex.IsMatch(x.Name, @".\." + moduleName + "$"));
                                if (dependency != null)
                                {
                                    mappedMethod.ExternalSubCallsList.Add(new ExternalNamedEntity()
                                    {
                                        Name = Regex.Split(subcall, @"\.")[1],
                                        Provider = dependency
                                    });
                                    
                                    var registeredDependency =
                                        dependecyRelations.FirstOrDefault(
                                            x => x.Destination.Equals(dependency), null);
                                    if (registeredDependency != null)
                                    {
                                        registeredDependency.NumberOfMethodCalls += 1;
                                    }
                                    else
                                    {
                                        dependecyRelations.Add(new ExternalDependencyRelation()
                                        {
                                            Destination = dependency,
                                            NumberOfBaseDefinitions = 0,
                                            NumberOfMethodCalls = 1
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void UpdateClassMethodsSubcallsWithInternalCalls(List<ClassModel> mappedClasses,
            List<RawFolderModel> rawFolders, List<InternalDependency> internalDependencies, List<ClassModel> importedClasses)
        {
            foreach (var dir in rawFolders)
            {
                foreach (var rawFile in dir.FilesList)
                {
                    foreach (var rawClass in rawFile.ClassesList)
                    {
                        var mappedClassInstance = mappedClasses.Find(x => rawClass.Name == x.Name);
                        if (mappedClassInstance == null) continue;

                        foreach (var rawMethod in rawClass.MethodsList)
                        {
                            var mappedMethod = mappedClassInstance.MethodsList.FirstOrDefault(x => x.Name == rawMethod.Name, null);
                            if (mappedMethod == null) continue;
                            
                            var containingFile = mappedClassInstance.ContainingFile;
                            var dependecyRelations = containingFile.DependencyRelations;

                            foreach (var subcall in rawMethod.SubCallsList)
                            {
                                // see if call is to an (directly) imported class' init method
                                var matchingImportedClassInstance =
                                    importedClasses.Find(x => x.Name.Equals(subcall));
                                if (matchingImportedClassInstance != null)
                                {
                                    mappedMethod.InternalSubCallsList.Add(
                                        matchingImportedClassInstance.MethodsList.FirstOrDefault(
                                            x => x.Name == "__init__",
                                            null));
                                    
                                    var registeredDependency =
                                        dependecyRelations.FirstOrDefault(
                                            x => x.Destination.Equals(matchingImportedClassInstance.ContainingFile), null);
                                    if (registeredDependency != null)
                                    {
                                        registeredDependency.NumberOfMethodCalls += 1;
                                    }
                                    else
                                    {
                                        dependecyRelations.Add(new InternalDependencyRelation()
                                        {
                                            Destination = matchingImportedClassInstance.ContainingFile,
                                            NumberOfBaseDefinitions = 0,
                                            NumberOfMethodCalls = 1
                                        });
                                    }
                                }



                                if (Regex.IsMatch(subcall, @".*\..*"))
                                {
                                    var moduleName = Regex.Split(subcall, @"\.")[0];

                                    // see if import of matching internal module exists 
                                    var dependency = internalDependencies.Find(x => x.Name == moduleName);
                                    if (dependency != null)
                                    {
                                        if (dependency.Source.GetType() == typeof(FileModel))
                                        {
                                            var fileDependency = (FileModel)dependency.Source;

                                            // check file methods list
                                            var subcallInstance = fileDependency.MethodsList.Find(x =>
                                                x.Name == Regex.Split(subcall, @"\.")[1]);
                                            if (subcallInstance != null)
                                            {
                                                mappedMethod.InternalSubCallsList.Add(subcallInstance);
                                                
                                                var registeredDependency =
                                                    dependecyRelations.FirstOrDefault(
                                                        x => x.Destination.Equals(subcallInstance.ContainingFile), null);
                                                if (registeredDependency != null)
                                                {
                                                    registeredDependency.NumberOfMethodCalls += 1;
                                                }
                                                else
                                                {
                                                    dependecyRelations.Add(new InternalDependencyRelation()
                                                    {
                                                        Destination = subcallInstance.ContainingFile,
                                                        NumberOfBaseDefinitions = 0,
                                                        NumberOfMethodCalls = 1
                                                    });
                                                }
                                                
                                                continue;
                                            }

                                            // check if it's a call to init method of a class
                                            var matchingClassInstance =
                                                fileDependency.ClassesList.Find(x => x.Name.Equals(subcall));
                                            if (matchingClassInstance != null)
                                            {
                                                mappedMethod.InternalSubCallsList.Add(
                                                    matchingClassInstance.MethodsList.FirstOrDefault(
                                                        x => x.Name == "__init__",
                                                        null));
                                                
                                                var registeredDependency =
                                                    dependecyRelations.FirstOrDefault(
                                                        x => x.Destination.Equals(matchingClassInstance.ContainingFile), null);
                                                if (registeredDependency != null)
                                                {
                                                    registeredDependency.NumberOfMethodCalls += 1;
                                                }
                                                else
                                                {
                                                    dependecyRelations.Add(new InternalDependencyRelation()
                                                    {
                                                        Destination = matchingClassInstance.ContainingFile,
                                                        NumberOfBaseDefinitions = 0,
                                                        NumberOfMethodCalls = 1
                                                    });
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // if dependency is like from module1.module2 import module3, then split it by tokens
                                        dependency = internalDependencies.Find(x =>
                                            Regex.Split(x.Name, @"\.").Last() == moduleName);
                                        if (dependency != null)
                                        {
                                            if (dependency.Source.GetType() == typeof(FileModel))
                                            {
                                                var fileDependency = (FileModel)dependency.Source;
                                                var subcallInstance = fileDependency.MethodsList.Find(x =>
                                                    x.Name == Regex.Split(subcall, @"\.")[1]);
                                                if (subcallInstance != null)
                                                {
                                                    mappedMethod.InternalSubCallsList.Add(subcallInstance);
                                                    
                                                    var registeredDependency =
                                                        dependecyRelations.FirstOrDefault(
                                                            x => x.Destination.Equals(subcallInstance.ContainingFile), null);
                                                    if (registeredDependency != null)
                                                    {
                                                        registeredDependency.NumberOfMethodCalls += 1;
                                                    }
                                                    else
                                                    {
                                                        dependecyRelations.Add(new InternalDependencyRelation()
                                                        {
                                                            Destination = matchingImportedClassInstance.ContainingFile,
                                                            NumberOfBaseDefinitions = 0,
                                                            NumberOfMethodCalls = 1
                                                        });
                                                    }
                                                    
                                                    continue;
                                                }
                                                
                                                // check if it's a call to init method of a class
                                                var matchingClassInstance =
                                                    fileDependency.ClassesList.Find(x => x.Name.Equals(Regex.Split(subcall, @"\.")[1]));
                                                if (matchingClassInstance != null)
                                                {
                                                    mappedMethod.InternalSubCallsList.Add(
                                                        matchingClassInstance.MethodsList.FirstOrDefault(
                                                            x => x.Name == "__init__",
                                                            null));
                                                    
                                                    var registeredDependency =
                                                        dependecyRelations.FirstOrDefault(
                                                            x => x.Destination.Equals(matchingClassInstance.ContainingFile), null);
                                                    if (registeredDependency != null)
                                                    {
                                                        registeredDependency.NumberOfMethodCalls += 1;
                                                    }
                                                    else
                                                    {
                                                        dependecyRelations.Add(new InternalDependencyRelation()
                                                        {
                                                            Destination = matchingClassInstance.ContainingFile,
                                                            NumberOfBaseDefinitions = 0,
                                                            NumberOfMethodCalls = 1
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
        }

        public static void UpdateClassMethodsSubcallsWithExternalCalls(List<ClassModel> mappedClasses,
            List<RawFolderModel> rawFolders, List<NamedProjectComponent> externalDependencies,
            List<ExternalNamedEntity> importedNames)
        {
            foreach (var dir in rawFolders)
            {
                foreach (var rawFile in dir.FilesList)
                {
                    foreach (var rawClass in rawFile.ClassesList)
                    {
                        var mappedClassInstance = mappedClasses.Find(x => rawClass.Name == x.Name);
                        if (mappedClassInstance == null) continue;
                        
                        var containingFile = mappedClassInstance.ContainingFile;
                        var dependecyRelations = containingFile.ExternalDependencyRelations;

                        foreach (var rawMethod in rawClass.MethodsList)
                        {
                            var mappedMethod =
                                mappedClassInstance.MethodsList.FirstOrDefault(x => x.Name == rawMethod.Name, null);
                            if (mappedMethod == null) continue;

                            foreach (var subcall in rawMethod.SubCallsList)
                            {
                                // see if call is to a directly imported name (e.g. could be method or class init function)
                                var matchingImportedClassName =
                                    importedNames.Find(x => x.Name == subcall);
                                if (matchingImportedClassName != null)
                                {
                                    mappedMethod.ExternalSubCallsList.Add(
                                        new ExternalNamedEntity()
                                        {
                                            Name = matchingImportedClassName.Name,
                                            Provider = matchingImportedClassName.Provider
                                        });
                                    
                                    var registeredDependency =
                                        dependecyRelations.FirstOrDefault(
                                            x => x.Destination.Equals(matchingImportedClassName.Provider), null);
                                    if (registeredDependency != null)
                                    {
                                        registeredDependency.NumberOfMethodCalls += 1;
                                    }
                                    else
                                    {
                                        dependecyRelations.Add(new ExternalDependencyRelation()
                                        {
                                            Destination = matchingImportedClassName.Provider,
                                            NumberOfBaseDefinitions = 0,
                                            NumberOfMethodCalls = 1
                                        });
                                    }
                                }



                                if (Regex.IsMatch(subcall, @".*\..*"))
                                {
                                    var moduleName = Regex.Split(subcall, @"\.")[0];

                                    // see if import of matching external module exists 
                                    var dependency = externalDependencies.Find(x => x.Name == moduleName);
                                    if (dependency != null)
                                    {
                                        mappedMethod.ExternalSubCallsList.Add(
                                            new ExternalNamedEntity()
                                            {
                                                Name = Regex.Split(subcall, @"\.")[1],
                                                Provider = dependency
                                            });
                                        
                                        
                                        var registeredDependency =
                                            dependecyRelations.FirstOrDefault(
                                                x => x.Destination.Equals(dependency), null);
                                        if (registeredDependency != null)
                                        {
                                            registeredDependency.NumberOfMethodCalls += 1;
                                        }
                                        else
                                        {
                                            dependecyRelations.Add(new ExternalDependencyRelation()
                                            {
                                                Destination = dependency,
                                                NumberOfBaseDefinitions = 0,
                                                NumberOfMethodCalls = 1
                                            });
                                        }
                                    }
                                    
                                    // maybe dependency if brought like from x import y; base = y.Class1 => check for that too
                                    dependency = externalDependencies.Find(x =>
                                        Regex.IsMatch(x.Name, @".\." + moduleName + "$"));
                                    if (dependency != null)
                                    {
                                        mappedMethod.ExternalSubCallsList.Add(new ExternalNamedEntity()
                                        {
                                            Name = Regex.Split(subcall, @"\.")[1],
                                            Provider = dependency
                                        });
                                        
                                        var registeredDependency =
                                            dependecyRelations.FirstOrDefault(
                                                x => x.Destination.Equals(dependency), null);
                                        if (registeredDependency != null)
                                        {
                                            registeredDependency.NumberOfMethodCalls += 1;
                                        }
                                        else
                                        {
                                            dependecyRelations.Add(new ExternalDependencyRelation()
                                            {
                                                Destination = dependency,
                                                NumberOfBaseDefinitions = 0,
                                                NumberOfMethodCalls = 1
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