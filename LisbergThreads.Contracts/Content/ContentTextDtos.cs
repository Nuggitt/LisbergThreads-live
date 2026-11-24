using System.ComponentModel.DataAnnotations;

namespace LisbergThreads.Contracts.Content;

public sealed record ContentTextDto(string Key, string Value, string? RowVersionBase64);

public sealed record UpdateContentTextDto(
    [Required, StringLength(2000)] string Value,
    string? RowVersionBase64);
