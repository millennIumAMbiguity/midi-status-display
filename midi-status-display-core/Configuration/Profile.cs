using MidiStatusDisplay.core.Trackers;

namespace MidiStatusDisplay.core.Configuration;

public class Profile
{
	public string? Device { get; init; } = string.Empty;
	public Tracker[] Items { get; init; } = [];
	
	public bool UsingTracker(TrackerTypes trackerTypes)
	{
		return Items.Any(item => item.TrackerTypes == trackerTypes);
	}
}

public class Tracker
{
	public TrackerTypes TrackerTypes { get; init; }
	public int UpdateInterval { get; set; } = 60000;
	public required ProfileItem[] Items { get; set; }

	[NonSerialized]
	public ITracker? TrackerObject;
}

public class ProfileItem
{
	public string? StatKey { get; set; }
	public string? StatValue { get; set; }
	public Mode Mode { get; set; } = Mode.Default;
	public byte[] Colors { get; set; } = [];
	public int PosX { get; set; } = 0;
	public int PosY { get; set; } = 0;
	public int Size { get; set; } = 1;
	public float Scale { get; set; } = 1;
	public Direction Direction { get; set; } = Direction.X;
}

public enum TrackerTypes : byte
{
	Ping,
	Regex,
	Jellyfin,
	TrueNas,
}

public enum Mode : byte
{
	Default,
	BarX,
	BarY,
}

public enum Direction : byte
{
	X,
	Y
}