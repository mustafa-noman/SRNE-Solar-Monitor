using Android.App;
using Android.App.Job;
using Android.Content;

namespace SolarPowerMonitor.Mobile.Services;

public static class TelemetryBackgroundScheduler
{
    public const int JobId = 24001;

    public static void ApplySettings()
    {
        var context = Android.App.Application.Context;
        var scheduler = (JobScheduler?)context.GetSystemService(Context.JobSchedulerService);
        if (scheduler is null) return;

        if (!MonitorSettings.BackgroundEnabled)
        {
            scheduler.Cancel(JobId);
            return;
        }

        var serviceClass = Java.Lang.Class.FromType(typeof(TelemetryJobService))
            ?? throw new InvalidOperationException("Background collector service type is unavailable.");
        var component = new ComponentName(context, serviceClass);
        var builder = new JobInfo.Builder(JobId, component);
        _ = builder.SetRequiredNetworkType(NetworkType.Any);
        _ = builder.SetPersisted(true);
        _ = builder.SetPeriodic((long)TimeSpan.FromHours(MonitorSettings.BackgroundIntervalHours).TotalMilliseconds);
        var job = builder.Build()
            ?? throw new InvalidOperationException("Background collector job could not be created.");
        _ = scheduler.Schedule(job);
    }
}
