namespace Plan_Cabinet
{
    public partial class App : Application
    {
        public App()
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense("Ngo9BigBOggjHTQxAR8/V1NCaF5cXmZCf1FpRmJGdld5fUVHYVZUTXxaS00DNHVRdkdnWXlfd3VURmReVUV2XEs=");
            InitializeComponent();
            MainPage = new AppShell();
        }

        protected override Window CreateWindow(IActivationState activationState)
        {
            var window = base.CreateWindow(activationState);

#if WINDOWS
            window.Created += (s, e) =>
            {
                // Get the native window handler
                var mauiWindow = s as Microsoft.Maui.Controls.Window;
                var platformWindow = mauiWindow.Handler.PlatformView as Microsoft.UI.Xaml.Window;

                // Ensure the standard title bar is visible
                platformWindow.ExtendsContentIntoTitleBar = false;

                // Set the window to a maximized state with the standard title bar controls
                var appWindow = platformWindow.AppWindow;
                appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Overlapped);

                // Maximize the window after setting the presenter
                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter overlappedPresenter)
                {
                    overlappedPresenter.Maximize();
                }
            };
#endif

            return window;
        }
    }
}