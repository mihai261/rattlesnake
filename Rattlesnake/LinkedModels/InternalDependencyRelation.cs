namespace Rattlesnake.LinkedModels;

public class InternalDependencyRelation
{
    public FileModel Destination { get; set; }
    public int NumberOfBaseDefinitions { get; set; }
    public int NumberOfMethodCalls { get; set; }
}