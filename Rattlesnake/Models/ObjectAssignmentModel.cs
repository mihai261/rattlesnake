using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ObjectAssignmentModel
{
    public String VariableName { get; set; }
    public ClassModel Type { get; set; }
}