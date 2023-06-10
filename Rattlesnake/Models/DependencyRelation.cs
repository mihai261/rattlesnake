namespace Rattlesnake.Models;

public class DependencyRelation
{
    public FileModel Destination { get; set; }
    public int NumberOfBaseDefinitions { get; set; }
    public int NumberOfMethodCalls { get; set; }
}