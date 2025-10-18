using MidiStatusDisplay.core.Trackers;

namespace MidiStatusDisplay.core.Configuration;

public class AppConfig : JellyfinTracker.IJellyfinConfig, TrueNasTracker.ITrueNasConfig, PingTracker.IPingConfig
{
	public string DefaultDevice { get; set; } = "";
	public int Timeout { get; set; } = 2000;
	public string JellyfinUrl { get; set; } = "";
	public string JellyfinApiKey { get; set; } = "";
	public int JellyfinActiveUserTime { get; set; } = 5000;
	public string TrueNasApiKey { get; set; } = "";
	public string TrueNasUrl { get; set; } = "";

	public int PingTimeout { get; set; } = 5000;
}

public interface ITrackerTimeout
{
	public int Timeout { get; set; }
}