using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace LisbergThreads.Client.Components.Shared;

public partial class InlineTextEditor : ComponentBase
{
    [Parameter] public string Text { get; set; } = string.Empty;
    [Parameter] public string? DisplayText { get; set; }
    [Parameter] public string Label { get; set; } = "tekst";
    [Parameter] public bool Multiline { get; set; }
    [Parameter] public bool Enabled { get; set; } = true;
    [Parameter] public int MaxLength { get; set; } = 2000;
    [Parameter] public string InputMode { get; set; } = "text";
    [Parameter, EditorRequired] public Func<string, Task<string?>> OnSave { get; set; } = null!;

    private readonly string inputId = $"inline-{Guid.NewGuid():N}";
    private ElementReference input;
    private bool editing, saving, focusInput;
    private string draft = string.Empty;
    private string? error;

    private void BeginEdit()
    {
        draft = Text;
        error = null;
        editing = true;
        focusInput = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (focusInput)
        {
            focusInput = false;
            await input.FocusAsync();
        }
    }

    private void Cancel() { editing = false; error = null; }

    private async Task HandleKey(KeyboardEventArgs args)
    {
        if (saving) return;
        if (args.Key == "Escape") Cancel();
        else if (args.Key == "Enter") await SaveAsync();
    }

    private void HandleMultilineKey(KeyboardEventArgs args)
    {
        if (!saving && args.Key == "Escape") Cancel();
    }

    private async Task SaveAsync()
    {
        if (saving) return;
        saving = true;
        error = null;
        try
        {
            error = await OnSave(draft);
            if (error is null) editing = false;
        }
        catch (Exception)
        {
            error = "Ændringen kunne ikke bekræftes. Genindlæs siden for at kontrollere, om den blev gemt.";
        }
        finally { saving = false; }
    }
}
