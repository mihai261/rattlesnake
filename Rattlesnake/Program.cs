using System.Diagnostics;
using System.Text.Json;
using Rattlesnake.Models;

namespace Rattlesnake
{
    class Converter {         
        static void Main(string[] args)
        {
            string path = args[0];
            if (File.Exists(path))
            {
                string result = File.OpenText(path).ReadToEnd();
                ProjectModel project = JsonSerializer.Deserialize<ProjectModel>(result);
            }
            else
            {
                throw new ArgumentException("Invalid file path!");
            }
        }
    }
}