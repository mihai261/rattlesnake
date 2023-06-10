namespace Rattlesnake.Models;

public class ExternalDependencyRelation
{
    public ExternalDependency Destination { get; set; }
    public int NumberOfBaseDefinitions { get; set; }
    public int NumberOfMethodCalls { get; set; }
}