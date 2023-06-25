using System.Text.Json.Serialization;

namespace Rattlesnake.RawModels;

public class RawLinesOfCode
{
    [JsonPropertyName("lines_total")]
    public int LinesTotal { get; set; }
    
    [JsonPropertyName("lines_code")]
    public int LinesCoded { get; set; }
    
    [JsonPropertyName("lines_commented")]
    public int LinesCommented { get; set; }
    
    [JsonPropertyName("lines_docs")]
    public int LinesDocs { get; set; }
    
    [JsonPropertyName("lines_empty")]
    public int LinesEmpty { get; set; }
}