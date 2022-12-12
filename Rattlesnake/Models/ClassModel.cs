using System.Text.Json.Serialization;

namespace Rattlesnake.Models;

public class ClassModel
{
    [JsonPropertyName("name")]
    public String Name { get; set; }
    
    [JsonPropertyName("methods")]
    public List<MethodModel> MethodsList { get; set; }
    
    [JsonIgnore]
    public List<ClassModel> SuperClassesList { get; set; }

    public ClassModel()
    {
        MethodsList = new List<MethodModel>();
        SuperClassesList = new List<ClassModel>();
    }

    protected bool Equals(ClassModel other)
    {
        return Name == other.Name && MethodsList.SequenceEqual(other.MethodsList) 
                                  && SuperClassesList.SequenceEqual(other.SuperClassesList);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((ClassModel)obj);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, MethodsList, SuperClassesList);
    }
}