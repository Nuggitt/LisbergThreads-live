using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Api.Models;

public class ContentText
{
    [Key, MaxLength(80)]
    public string Key { get; set; } = string.Empty;
    [Required, MaxLength(2000)]
    public string Value { get; set; } = string.Empty;
    [Timestamp]
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
