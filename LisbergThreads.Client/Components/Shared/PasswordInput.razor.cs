using Microsoft.AspNetCore.Components;

namespace LisbergThreads.Client.Components.Shared
{
    public partial class PasswordInput
    {
        [Parameter]
        public string Value { get; set; } = string.Empty;

        [Parameter]
        public EventCallback<string> ValueChanged { get; set; }

        [Parameter]
        public string Autocomplete { get; set; } = "current-password";

        private bool ShowPassword { get; set; }

        private void TogglePasswordVisibility()
        {
            ShowPassword = !ShowPassword;
        }

        private async Task HandleInput(ChangeEventArgs e)
        {
            Value = e.Value?.ToString() ?? string.Empty;
            await ValueChanged.InvokeAsync(Value);
        }
    }
}
