namespace SolarPowerMonitor.Mobile;

public partial class HistoryPage : ContentPage
{
    public HistoryPage()
    {
        InitializeComponent();
        BindingContext = DashboardViewModel.Current;
    }
}
