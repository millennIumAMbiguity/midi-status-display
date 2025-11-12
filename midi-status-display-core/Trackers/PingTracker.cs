using System.Diagnostics;
using System.Net.Sockets;
using MidiStatusDisplay.core.Configuration;

namespace MidiStatusDisplay.core.Trackers;

public class PingTracker : ITracker
{
	public long LastUpdate { get; set; }
	private readonly HttpClient _httpClient = new HttpClient();
	private Log _log = Log.None;

	public PingTracker(IPingConfig config)
	{
		_httpClient.Timeout = TimeSpan.FromMilliseconds(config.PingTimeout);
	}
	
	public async Task PingAsync(Tracker tracker)
	{
		int taskId = -1;
		foreach (var item in tracker.Items)
		{
			try
			{
				var task = _httpClient.GetAsync(item.StatKey);
				taskId = task.Id;
				var a = await task;
				LogDebug(item.StatKey!, a);
				item.Size = a.IsSuccessStatusCode ? 1 : 0;
			}
			catch (HttpRequestException e)
			{
				if (e.InnerException is SocketException se)
				{
					if (HandleSocketException(item, se)) throw;
				}
				else
				{
					//_log.Debug($"Ping {item.StatKey} - Failed: {e.Message}");
					item.Size = 0;
					throw;
				}

			}
			catch (SocketException se)
			{
				if (HandleSocketException(item, se)) throw;
			}
			// expected error: The request was canceled due to the configured HttpClient.
			catch (TaskCanceledException e) when (e.InnerException is TimeoutException)
			{
				//_log.Debug($"Ping {item.StatKey} - Failed: {e.Message}");
				item.Size = 0;
			}
		}
	}

	private bool HandleSocketException(ProfileItem item, SocketException e)
	{
		switch (e.ErrorCode)
		{
			case 10060:
				_log.Debug("The connection attempt timed out, or the connected host has failed to respond.");
				item.Size = 0;
				break;
			case 10061:
				_log.Debug("The remote host is actively refusing a connection.");
				item.Size = 0;
				break;
			case 10065:
				_log.Debug("The operation failed because the remote host is down.");
				item.Size = 0;
				break;
			default:
				_log.Error("SocketException code: " + e.ErrorCode);
				return true;
		}

		return false;
	}

	[Conditional("DEBUG")]
	private async void LogDebug(string key, HttpResponseMessage msg)
	{
		try
		{
			_log.Debug(msg.IsSuccessStatusCode ? $"Ping {key} - Success" : $"Ping {key} - Failed: {await msg.Content.ReadAsStringAsync()}");
		}
		catch (Exception e)
		{
			_log.Debug(e.ToString());
		}
	}

	public void Update(Tracker tracker)
	{
		PingAsync(tracker).Wait();
	}
	
	public void Display(Device device, Tracker tracker)
	{
		for (var i = 0; i < tracker.Items.Length; i++)
		{
			var item = tracker.Items[i];
			device.Draw((byte)item.PosX, (byte)item.PosY, item.Colors[item.Size]);
		}
	}
	
	public void Init(Controller controller, Tracker tracker)
	{
		_log = controller.Log;
		foreach (var item in tracker.Items)
		{
			if (string.IsNullOrEmpty(item.StatKey)) throw new NullReferenceException();
			if (item.Colors.Length == 0) item.Colors = [0, 3];
			if (item.Colors.Length == 1) item.Colors = [0, item.Colors[0]];
			item.Size = 0;
		}
	}

	public interface IPingConfig {
		int PingTimeout { get; set; }
	}
}