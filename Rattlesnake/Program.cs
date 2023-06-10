using System.Text.Json;
using System.Text.RegularExpressions;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake
{
    class Converter {

        private static List<ClassModel> MapClasses(RawFileModel rawFile, String relativePath) 
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

        private static void MapInternalClassBases(List<ClassModel> mappedClasses,
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
                                                dependecyRelations.Add(new DependencyRelation()
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
                                                    dependecyRelations.Add(new DependencyRelation()
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

        private static void MapExternalClassBases(List<ClassModel> mappedClasses,
            List<ExternalDependency> externalDependencies, List<RawFolderModel> projectFolders,
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

        private static List<MethodModel> MapFileMethods(RawFileModel rawFile, String relativePath, List<ClassModel> mappedClasses)
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

                // map arguments
                foreach (var methodArg in mth.ArgumentsList)
                {
                    ArgumentModel mappedArg = new ArgumentModel
                    {
                        Name = methodArg.Name
                    };
                    
                    // see if there's a matching class instance
                    var matchedArgType = mappedClasses.Find(x => x.Name.Equals(methodArg.Annotation.Replace("self.", "")));
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

        private static void MapClassMethods(RawFileModel rawFile, String relativePath, List<ClassModel> mappedClasses, List<MethodModel> fileMethods)
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

                    // map arguments
                    foreach (var methodArg in mth.ArgumentsList)
                    {
                        ArgumentModel mappedArg = new ArgumentModel();
                        mappedArg.Name = methodArg.Name;
                        // see if there's a matching class instance
                        var matchedArgType = mappedClasses.Find(x => x.Name.Equals(methodArg.Annotation.Replace("self.", "")));
                        // if not matching instance, just use string names
                        mappedArg.Annotation = matchedArgType != null ? matchedArgType : methodArg.Annotation;
                
                        currentMethod.ArgumentsList.Add(mappedArg);
                    }
                    
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
                            
                            // check if method call uses a parameter or is a static call
                            else if (Regex.IsMatch(subcall, @".*\..*"))
                            {
                                // parameter use?
                                var argument = currentMethod.ArgumentsList.Find(x =>
                                    x.Name.Equals(Regex.Split(subcall, @"\.")[0]));
                                if (argument != null)
                                {
                                    if (argument.GetType() == typeof(ClassModel))
                                    {
                                        var methodInstance = ((ClassModel)argument.Annotation).MethodsList.FirstOrDefault(x =>
                                            x.Name.Equals(Regex.Split(subcall, @"\.")[1]), null);
                                        if (methodInstance != null)
                                        {
                                            mappedSubcalls.Add(subcall);
                                            currentMethod.LocalSubCallsList.Add(methodInstance);
                                        }
                                    }
                                }

                                // static call?
                                else
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

        private static void UpdateFileMethodsSubcallsListWithLocalCalls(RawFileModel rawFile, List<ClassModel> mappedClasses, List<MethodModel> fileMethods)
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
                        var tokenizedCallString = Regex.Split(subcall, @"\.");
                        var argument = currentMethod.ArgumentsList.Find(x =>
                            x.Name.Equals(tokenizedCallString[0]));
                        if (argument != null)
                        {
                            if (argument.Annotation.GetType() == typeof(ClassModel))
                            {
                                var methodInstance = ((ClassModel)argument.Annotation).MethodsList.FirstOrDefault(x =>
                                    x.Name.Equals(Regex.Split(subcall, @"\.")[1]), null);
                                if (methodInstance != null)
                                {
                                    mappedSubcalls.Add(subcall);
                                    currentMethod.LocalSubCallsList.Add(methodInstance);
                                }
                            }
                        }
                        
                        else
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
                }
                
                // remove all mapped methods from raw model to speed up further mapping attempts
                rawFileMethod.SubCallsList.RemoveAll(x => mappedSubcalls.Contains(x));
            }
        }

        private static void UpdateFileMethodsSubcallsListWithInternalCalls(List<MethodModel> fileMethods,
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
                                    dependecyRelations.Add(new DependencyRelation()
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
                                                dependecyRelations.Add(new DependencyRelation()
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
                                                dependecyRelations.Add(new DependencyRelation()
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
                                                    dependecyRelations.Add(new DependencyRelation()
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

        private static void UpdateFileMethodsSubcallsListWithExternalCalls(List<MethodModel> fileMethods, 
            List<RawFolderModel> rawFolders, List<ExternalDependency> externalDependencies, List<ExternalNamedEntity> importedNames)
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

        private static void UpdateClassMethodsSubcallsWithInternalCalls(List<ClassModel> mappedClasses,
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
                                        dependecyRelations.Add(new DependencyRelation()
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
                                                    dependecyRelations.Add(new DependencyRelation()
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
                                                    dependecyRelations.Add(new DependencyRelation()
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
                                                        dependecyRelations.Add(new DependencyRelation()
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
                                                        dependecyRelations.Add(new DependencyRelation()
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

        private static void UpdateClassMethodsSubcallsWithExternalCalls(List<ClassModel> mappedClasses,
            List<RawFolderModel> rawFolders, List<ExternalDependency> externalDependencies,
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


        private static (ProjectComponent?, ClassModel?) FindInternalDependency(String importString, List<FolderModel> projectFolders)
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
                        List<ClassModel> classes = MapClasses(rawFile, file.RelativePath);
                        List<MethodModel> fileMethods = MapFileMethods(rawFile, file.RelativePath, classes);
                        MapClassMethods(rawFile, file.RelativePath, classes, fileMethods);
                        UpdateFileMethodsSubcallsListWithLocalCalls(rawFile, classes, fileMethods);

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
                            var (linkedImport, linkedClass) = FindInternalDependency(import, project.FoldersList);
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
                                var depedency = new ExternalDependency();
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
                        MapInternalClassBases(file.ClassesList, file.InternalDependencies.ToList(), rawProject.FoldersList);
                        
                        // update bases list with instances from other external modules
                        MapExternalClassBases(file.ClassesList, file.ExternalDependencies.ToList(), rawProject.FoldersList, file.ImportedExternalNames);
                        
                        // update file method subcalls to include references to methods from other internal modules
                        UpdateFileMethodsSubcallsListWithInternalCalls(file.MethodsList, rawProject.FoldersList, file.InternalDependencies.ToList(), file.ImportedClassesList);
                        
                        // update file method subcalls to include references to external names
                        UpdateFileMethodsSubcallsListWithExternalCalls(file.MethodsList, rawProject.FoldersList, file.ExternalDependencies.ToList(), file.ImportedExternalNames);
                        
                        // update class methods subcalls for each class in the current file
                        UpdateClassMethodsSubcallsWithInternalCalls(file.ClassesList, rawProject.FoldersList, file.InternalDependencies.ToList(), file.ImportedClassesList);
                        UpdateClassMethodsSubcallsWithExternalCalls(file.ClassesList, rawProject.FoldersList, file.ExternalDependencies.ToList(), file.ImportedExternalNames);

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