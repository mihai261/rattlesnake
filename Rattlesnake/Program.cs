using System.Diagnostics;
using System.Text.Json;
using Rattlesnake.Models;

namespace Rattlesnake
{
    class Converter {         
        static void Main(string[] args)
        {
            ProcessStartInfo start = new ProcessStartInfo();
            string probePath = "../../../python_scripts/probe_link";
            start.FileName = "/usr/bin/python3";
            start.Arguments = string.Format("{0} {1}", probePath, args[0]);
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            using(Process process = Process.Start(start))
            {
                using(StreamReader reader = process.StandardOutput)
                {
                    string result = reader.ReadToEnd();
                    ProjectModel project = JsonSerializer.Deserialize<ProjectModel>(result);
                }
            }
            
        }
    }
}