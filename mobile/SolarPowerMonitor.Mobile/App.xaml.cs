using Microsoft.Extensions.DependencyInjection;

namespace SolarPowerMonitor.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		window.Created += async (_, _) => await DashboardViewModel.Current.StartAsync();
		window.Destroying += (_, _) => DashboardViewModel.Current.Stop();
		return window;
	}
}
