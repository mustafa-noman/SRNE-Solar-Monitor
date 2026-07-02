using Microsoft.Extensions.DependencyInjection;

using SolarPowerMonitor.Mobile.Services;

namespace SolarPowerMonitor.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		UserAppTheme = AppTheme.Dark;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		var window = new Window(new AppShell());
		window.Created += async (_, _) =>
		{
			await MonitorSettings.InitializeAsync();
			TelemetryBackgroundScheduler.ApplySettings();
			await DashboardViewModel.Current.StartAsync();
		};
		window.Resumed += async (_, _) => await DashboardViewModel.Current.StartAsync();
		window.Stopped += (_, _) => DashboardViewModel.Current.Stop();
		window.Destroying += (_, _) => DashboardViewModel.Current.Stop();
		return window;
	}
}
