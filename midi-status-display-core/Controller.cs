using System.Diagnostics;
using Commons.Music.Midi;
using MidiStatusDisplay.core.Configuration;
using MidiStatusDisplay.core.Trackers;

namespace MidiStatusDisplay.core;

public class Controller : IDisposable
{
	private static JellyfinTracker? _jellyfinTracker = null;
	private static TrueNasTracker? _trueNasTracker = null;
	private static PingTracker? _pingTracker = null;
	private bool _notDisposed = true;
	public bool Running { get; private set; } = false;

	public readonly Log Log;

	private readonly Profile _profile;
	private readonly Device? _device;
	private readonly AppConfig _config;
	
	private ITracker GetTracker(TrackerTypes type)
	{
		return (type switch
		{
			TrackerTypes.Ping => _pingTracker,
			TrackerTypes.Regex => throw new NotImplementedException(),
			TrackerTypes.Jellyfin => _jellyfinTracker,
			TrackerTypes.TrueNas => _trueNasTracker,
			_ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
		})!;
	}

	public Controller(Log? log, Profile profile, AppConfig? config, string device) : this(log, profile, config, GetDevice(log ?? Log.None, device)) { }

	public static Device? GetDevice(Log log, string device)
	{
		if (int.TryParse(device, out int selection))
		{
			var dev = GetDeviceFromId(log, selection);
			if (dev != null)
			{
				return dev;
			}
		}

		return GetDeviceFromName(log, device);
	}
	
	private static Device? GetDeviceFromName(Log log, string deviceName)
	{
		foreach (IMidiPortDetails dev in MidiAccessManager.Default.Inputs)
		{
			if (dev.Name.Length < deviceName.Length) continue;
			if (dev.Name.Substring(0, deviceName.Length).Equals(deviceName, StringComparison.OrdinalIgnoreCase))
			{
				log.Info($"Selected device: {dev.Name}");
				return Device.Start(dev, log);
			}
		}

		return null;
	}
	
	private static Device? GetDeviceFromId(Log log, string deviceId)
	{
		if (int.TryParse(deviceId, out int selection))
		{
			var dev  = MidiAccessManager.Default.Inputs.ElementAt(selection);
			if (deviceId == dev.Id)
			{
				log.Info($"Selected device: {dev.Name}");
				return Device.Start(dev, log);
			}
		}

		foreach (IMidiPortDetails dev in MidiAccessManager.Default.Inputs)
		{
			if (dev.Id.Length != deviceId.Length) continue;
			if (dev.Id == deviceId)
			{
				log.Info($"Selected device: {dev.Name}");
				return Device.Start(dev, log);
			}
		}

		return null;
	}
	
	private static Device? GetDeviceFromId(Log log, int deviceId)
	{
		if (MidiAccessManager.Default.Inputs.Count() > deviceId && deviceId >= 0)
		{
			var dev = MidiAccessManager.Default.Inputs.ElementAt(deviceId);
			{
				log.Info($"Selected device: {dev.Name}");
				return Device.Start(dev, log);
			}
		}

		return null;
	}

	public Controller(Log? log, Profile profile, AppConfig? config = null, Device? device = null)
	{
		Log = log ?? Log.None;
		_config = config ?? ConfigLoader.LoadConfig();
		_profile = profile;
		
		if (device == null && _profile.Device != String.Empty && _config.DefaultDevice != String.Empty)
		{
			string deviceName = _profile.Device ?? _config.DefaultDevice;
			int deviceNameLength = deviceName.Length;
			foreach (IMidiPortDetails dev in MidiAccessManager.Default.Inputs)
			{
				if (dev.Name.Length < deviceNameLength) continue;
				if (dev.Name.Substring(0, deviceNameLength).Equals(deviceName, StringComparison.OrdinalIgnoreCase))
				{
					Log.Info($"Selected device: {dev.Name}");
					_device = Device.Start(dev, log);
					break;
				}
			}
		}
		else
		{
			throw new NullReferenceException("Device cannot be null, please provide a valid MIDI device.");
		}
		
		if (_profile.UsingTracker(TrackerTypes.Ping))     _pingTracker     = new PingTracker    (_config);
		if (_profile.UsingTracker(TrackerTypes.Jellyfin)) _jellyfinTracker = new JellyfinTracker(_config);
		if (_profile.UsingTracker(TrackerTypes.TrueNas))  _trueNasTracker  = new TrueNasTracker (_config);
		
		foreach (var item in _profile.Items)
		{
			Log.Debug($"Initializing tracker: {item.TrackerTypes}");
			item.TrackerObject = GetTracker(item.TrackerTypes);
			item.TrackerObject.Init(this, item);
			item.TrackerObject.LastUpdate = - item.UpdateInterval - 100;
		}
	}
	
	public void Start()
	{

		if (_profile.UsingTracker(TrackerTypes.Ping))     _pingTracker     = new PingTracker    (_config);
		if (_profile.UsingTracker(TrackerTypes.Jellyfin)) _jellyfinTracker = new JellyfinTracker(_config);
		if (_profile.UsingTracker(TrackerTypes.TrueNas))  _trueNasTracker  = new TrueNasTracker (_config);
		
		if (_device == null)
		{
			throw new NullReferenceException("Device cannot be null, please provide a valid MIDI device.");
		}
		
		Log.Info("Controller started.");
		Running = true;
		_notDisposed = true;

		while (!_device.Connected)
		{
			Thread.Sleep(1);
		}

		var sw = new Stopwatch();
		sw.Start();
		
		Thread.GetDomain().DomainUnload += (sender, args) => { _notDisposed = false; };
		
		while (_notDisposed)
		{
			bool wasUpdated = false;
			long nextUpdate = long.MaxValue;

			foreach (var item in _profile.Items)
			{
				var tracker = item.TrackerObject!;
				long elapsed = sw.ElapsedMilliseconds;
				if (tracker.LastUpdate + item.UpdateInterval < elapsed)
				{
					tracker.LastUpdate = elapsed;
					Log.Debug("Updating tracker: " + tracker.GetType().Name);
					tracker.Update(item);
					tracker.Display(_device, item);
					wasUpdated = true;
				}
				
				nextUpdate = Math.Min(nextUpdate, tracker.LastUpdate + item.UpdateInterval);
				
			}
			
			if (wasUpdated) _device.Update();
			
			int sleepTime = Math.Max(100, (int)(nextUpdate - sw.ElapsedMilliseconds + 1));
			Log.Debug("Next Update in " + sleepTime + " ms");
			Thread.Sleep(sleepTime);
		}
		
		_device?.Dispose();

		Log.Info("Controller stopped.");
		Running = false;
	}
	
	public void Stop()
	{
		_notDisposed = false;
	}

	public void Dispose()
	{
		_notDisposed = false;
		_device?.Dispose();
		Running = false;
	}
}