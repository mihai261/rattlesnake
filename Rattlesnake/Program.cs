using System.Text.Json;
using Rattlesnake.Models;
using Rattlesnake.RawModels;

namespace Rattlesnake
{
    class Converter {

        private static List<ClassModel> LinkSuperClasses(RawFileModel rawFile)
        {
            var superclassNamesMap = new Dictionary <ClassModel, List<string>> ();
            var linkedClasses = new List<ClassModel>();

            // create instances for each class in file (not linked to superclass)
            foreach (var cls in rawFile.ClassesList)
            {
                ClassModel currentClass = new ClassModel();
                currentClass.Name = cls.Name;
                superclassNamesMap.Add(currentClass, (cls.SuperClassesList != null ? cls.SuperClassesList : new List<string>()));
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
                        }
                    }
            
                    linkedClass.MethodsList.Add(currentMethod);
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
                
                
                foreach (var folder in rawProject.FoldersList)
                {
                    foreach (var file in folder.FilesList)
                    {
                        List<ClassModel> classes = LinkSuperClasses(file);
                        List<MethodModel> fileMethods = ConvertFileMethods(file, classes);
                        ConvertClassMethods(file, classes, fileMethods);
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