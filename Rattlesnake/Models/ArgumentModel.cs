using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ArgumentModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("annotation")]
    public String Annotation { get; set; }

    protected bool Equals(ArgumentModel other)
    {
        return Name == other.Name && Annotation == other.Annotation;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ArgumentModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Annotation);
    }
}