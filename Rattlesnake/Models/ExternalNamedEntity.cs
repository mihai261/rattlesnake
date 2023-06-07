namespace Rattlesnake.Models;

public class ExternalNamedEntity
{
    public string Name { get; set; }
    public ExternalDependency Provider { get; set; }
}