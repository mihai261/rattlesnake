namespace Rattlesnake.LinkedModels;

public class ExternalDependencyRelation
{
    public NamedProjectComponent Destination { get; set; }
    public int NumberOfBaseDefinitions { get; set; }
    public int NumberOfMethodCalls { get; set; }
}