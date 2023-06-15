using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class InternalProjectComponent : NamedProjectComponent
{
    public String RelativePath { get; set; }
}