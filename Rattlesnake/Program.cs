using System.Linq.Expressions;
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
                    var matchedClassConstructor = superclassNamesMap.Keys.First(x => x.Name.Equals(obja.ClassName));
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

        private static List<MethodModel> ConvertFileMethods(RawFileModel rawFile, List<ClassModel> fileClasses)
        {
            var fileMethods = new List<MethodModel>();
            // map file methods
            foreach (var mth in rawFile.MethodsList)
            {
                MethodModel currentMethod = new MethodModel();
                currentMethod.Name = mth.Name;
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
                        currentMethod.SubCallsList.Add(fileMethod);
                    }
                }
            }
            
            // TODO: map file method subcalls to method from imports lists
            return fileMethods;
        }

        private static void ConvertClassMethods(RawFileModel rawFile, List<ClassModel> classes, List<MethodModel> fileMethods)
        {
            // map class methods
            foreach (var cls in rawFile.ClassesList)
            {
                var linkedClass = classes.Find(x => x.Name.Equals(cls.Name));
                if (linkedClass == null) continue;
                // map class methods
                foreach (var mth in cls.MethodsList)
                {
                    MethodModel currentMethod = new MethodModel();
                    currentMethod.Name = mth.Name;
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
                    
                    // map global method subcalls
                    foreach (var subcall in mth.SubCallsList)
                    {
                        var fileMethod = fileMethods.Find(x => x.Name.Equals(subcall));
                        if (fileMethod != null)
                        {
                            currentMethod.SubCallsList.Add(fileMethod);
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
                                            currentMethod.SubCallsList.Add(methodInstance);
                                        }
                                    }
                                }
                            }
                            
                            // check if method call uses a parameter
                            else if (Regex.IsMatch(subcall, @".*\..*"))
                            {
                                var argument = currentMethod.ArgumentsList.Find(x =>
                                    x.Name.Equals(Regex.Split(subcall, @".*\..*")[0]));
                                if (argument != null)
                                {
                                    if (argument.GetType() == typeof(ClassModel))
                                    {
                                        var methodInstance = ((ClassModel)argument.Annotation).MethodsList.Find(x =>
                                            x.Name.Equals(Regex.Split(subcall, @".*\..*")[1]));
                                        if (methodInstance != null)
                                        {
                                            currentMethod.SubCallsList.Add(methodInstance);
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
                    // check if method call uses a parameter
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
                                    currentMethod.SubCallsList.Add(methodInstance);
                                }
                            }
                        }
                    }
                }
            }
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
                    folder.RelativePath = rawFolder.RelativePath;
                    folder.FilesList = new List<FileModel>();
                    
                    foreach (var rawFile in rawFolder.FilesList)
                    {
                        FileModel file = new FileModel();
                        
                        List<ClassModel> classes = ConvertClassesWithBaseLinks(rawFile);
                        List<MethodModel> fileMethods = ConvertFileMethods(rawFile, classes);
                        ConvertClassMethods(rawFile, classes, fileMethods);
                        UpdateFileMethodSubcallsList(rawFile, classes, fileMethods);

                        file.Name = rawFile.Name;
                        file.ImportsList = rawFile.ImportsList;
                        file.Lines = JsonSerializer.Deserialize<LinesOfCode>(JsonSerializer.Serialize(rawFile.Lines));
                        file.MethodsList = fileMethods;
                        file.ClassesList = classes;
                        
                        folder.FilesList.Add(file);
                    }
                    
                    project.FoldersList.Add(folder);
                }

            }
            else
            {
                throw new ArgumentException("Invalid file path!");
            }
        }
    }
}