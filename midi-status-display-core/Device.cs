using Commons.Music.Midi;

namespace MidiStatusDisplay.core;

public class Device : IDisposable
{
	public IMidiOutput? Output { get; private set; }
	public IMidiInput? Input { get; private set; }
	
	public bool Connected { get; private set; }
	
	private protected Log Log;
	
	public virtual void Dispose()
	{
		Output?.CloseAsync().Wait();
		Output?.Dispose();
		Input?.CloseAsync().Wait();
		Input?.Dispose();
	}

	public Device(string deviceId, Log? log = null)
	{
		Log = log ?? Log.None;
		Start(deviceId);
	}

	private void Start(string deviceId)
	{
		MidiAccessManager.Default.OpenInputAsync(deviceId).ContinueWith(task =>
		{
			Input = task.Result;
			Input.MessageReceived += OnInputOnMessageReceived;
			Log.Info($"Input device {Input.Details.Name} opened successfully.");
			OnDeviceInputConnected();

			string outId = MidiAccessManager.Default.Outputs.First(d => d.Name == Input.Details.Name).Id;
			
			MidiAccessManager.Default.OpenOutputAsync(outId).ContinueWith(task =>
			{
				Output = task.Result;
				OnDeviceOutputConnected();
				Connected = true;
				Log.Info($"Output device {Output.Details.Name} opened successfully.");
			});
		});
	}

	private protected virtual void OnDeviceInputConnected()
	{
		
	}
	
	private protected virtual void OnDeviceOutputConnected()
	{
		
	}

	public void Send(params byte[] value)
	{
		Output.Send(value, 0 , value.Length, 0);
	}

	public void Send(byte[] value, int lenght) => 
		Output.Send(value, 0 , lenght, 0);

	private protected virtual void OnInputOnMessageReceived(object? sender, MidiReceivedEventArgs e)
	{
		if (e.Length - e.Start > 1)
		{
			Log.Info($"MIDI Input: {DwToHex(e.Data[e.Start])} {DwToHex(e.Data[e.Start + 1])}");
		}
		else
		{
			Log.Info($"MIDI Input: {DwToHex(e.Data[e.Start])}");
		}
	}
	
	private protected static string DwToHex(int dw) => dw.ToString("X").PadLeft(4, '0');
	
	public static Device Start(IMidiPortDetails device, Log? log = null)
	{
		switch (device.Name)
		{
			case "Launchpad Pro":
				return new Devices.NovationLaunchpadPro(device.Id, log);
			default:
				return new Device(device.Id, log);
		}
	}
	
	public virtual void DrawBarX(byte value, byte x, byte color, bool clear = true) { }
	public virtual void DrawBarX(byte value, byte x, bool clear = true) { }
	public virtual void Draw(byte x, byte y, byte color) { }

	public virtual void Update() { }
}