using Commons.Music.Midi;
using MidiStatusDisplay.core;
using MidiStatusDisplay.core.Configuration;

namespace MidiStatusDisplay.CL;

public static class Program
{
	
	public static Log Log { get; } = new ConsoleLog();
	private static Controller? _controller = null;
	
	public static void Main(string[] args)
	{
		
		// Handle exceptions on all threads
		AppDomain.CurrentDomain.UnhandledException += (_, e) =>
		{
			LogError((Exception) e.ExceptionObject, "AppDomain");
		};

		// Handle exceptions in async Tasks
		TaskScheduler.UnobservedTaskException += (_, e) =>
		{
			LogError(e.Exception, "TaskScheduler");
			e.SetObserved();
		};
		
		Console.WriteLine("Midi Status Display CL");

		string configPath = ConfigLoader.DEFAULT_PATH;
		string profilePath = ProfileLoader.DEFAULT_PATH;

		for (int i = 0, l = args.Length; i < l; i++)
		{
			switch (args[i])
			{
				case "--help":
				case "-h":
					Console.WriteLine("Usage: midi-status-display-cl [--config <path>] [--profile <path>]");
					return;
				case "--config":
				case "-c":
					if (i + 1 < l)
					{
						configPath = args[i + 1];
						i++;
						break;
					}
					Console.WriteLine("Error: --config requires a path argument.");
					return;
				case "--profile":
				case "-p":
					if (i + 1 < l)
					{
						profilePath = args[i + 1];
						i++;
						break;
					}
					Console.WriteLine("Error: --config requires a path argument.");
					return;
			}
		}

		var config = ConfigLoader.LoadConfig(configPath);
		var profile = ProfileLoader.LoadProfile(profilePath);

		Device? midi = null;

		if (profile.Device == String.Empty && config.DefaultDevice == String.Empty)
		{
			do
			{
				midi = GetDevice();
			}
			while (midi == null);
		}
		
		AppDomain.CurrentDomain.ProcessExit += OnCurrentDomainOnProcessExit;
		_controller = new Controller(Log, profile, config, midi);

		_controller.Start();
		_controller.Dispose();
		_controller = null;
		
		Console.WriteLine("\nPress any key to exit...");
		Console.ReadKey();
	}
	
	static void LogError(Exception ex, string source)
	{
		string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
		string message = $"[{DateTime.Now}] Source: {source}\n{ex}\n\n";
		File.AppendAllText(logPath, message);
	}

	private static void OnCurrentDomainOnProcessExit(object? sender, EventArgs e)
	{
		_controller?.Dispose();
	}

	private static Device? GetDevice()
	{
		Console.Clear();
		Console.WriteLine("Outputs:");
		foreach (IMidiPortDetails dev in MidiAccessManager.Default.Outputs)
		{
			Console.WriteLine(dev.Id + " : " + dev.Name);
		}
		
		Console.WriteLine("Inputs:");
		foreach (IMidiPortDetails dev in MidiAccessManager.Default.Inputs)
		{
			Console.WriteLine(dev.Id + " : " + dev.Name);
		}

		string? line = Console.ReadLine();
		
		if (string.IsNullOrEmpty(line))
		{
			Log.Error("No device selected.");
			return null;
		}

		var device = Controller.GetDevice(Log, line);
		if (device != null) return device;
			
		Log.Error($"Device {line} not found.");
		return null;
	}
}