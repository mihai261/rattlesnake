using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class MethodModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("lines")]
    public LinesOfCode Lines { get; set; }
    
    [JsonPropertyName("complexity")]
    public int CyclomaticComplexity { get; set; }
    
    [JsonPropertyName("parent")]
    public String Parent { get; set; }
    
    [JsonPropertyName("decorators")]
    public List<String> DecoratorsList { get; set; }
    
    [JsonPropertyName("args")]
    public List<ArgumentModel> ArgumentsList { get; set; }
    
    [JsonPropertyName("sub_calls")]
    public List<MethodModel> SubCallsList { get; set; }

    public MethodModel()
    {
        ArgumentsList = new List<ArgumentModel>();
        DecoratorsList = new List<string>();
        SubCallsList = new List<MethodModel>();
    }

    protected bool Equals(MethodModel other)
    {
        return Name == other.Name && Parent == other.Parent && DecoratorsList.SequenceEqual(other.DecoratorsList)
                                  && ArgumentsList.SequenceEqual(other.ArgumentsList) 
                                  && SubCallsList.SequenceEqual(other.SubCallsList);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((MethodModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Parent, DecoratorsList, ArgumentsList, SubCallsList);
    }
}