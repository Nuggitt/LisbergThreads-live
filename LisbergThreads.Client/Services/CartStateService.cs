namespace LisbergThreads.Client.Services
{
    public class CartStateService
    {
        public event Action? OnChange;
        public event Action? OnOpenSidebar;

        public void NotifyChanged()
        {
            OnChange?.Invoke();
        }

        public void OpenSidebar()
        {
            OnOpenSidebar?.Invoke();
        }
    }
}