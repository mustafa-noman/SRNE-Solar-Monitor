namespace SolarPowerMonitor.Mobile;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = DashboardViewModel.Current;
    }
}
