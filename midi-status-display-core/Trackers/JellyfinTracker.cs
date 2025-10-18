using System.Text.Json;
using MidiStatusDisplay.core.Configuration;

namespace MidiStatusDisplay.core.Trackers;

public class JellyfinTracker : ITracker
{
	private readonly HttpClient _httpClient;
	private Log _log = Log.None;
	private readonly IJellyfinConfig _config;
	
	private int _lastCount = -1;
	public int ActiveUserCount { get; private set; }
	public long LastUpdate { get; set; }
	

	public JellyfinTracker(IJellyfinConfig config)
	{
		_config = config;
		_httpClient = new HttpClient();
		_httpClient.BaseAddress = new Uri(_config.JellyfinUrl.TrimEnd('/'));
		_httpClient.DefaultRequestHeaders.Add("X-Emby-Token", _config.JellyfinApiKey);
		_httpClient.Timeout = TimeSpan.FromMilliseconds(config.Timeout);
	}

	public async Task<int> GetActiveUserCountAsync()
	{
		var request = new HttpRequestMessage(HttpMethod.Get, "/Sessions");

		HttpResponseMessage response;
		try
		{
			response = await _httpClient.SendAsync(request);
		}
		// timeout
		catch (TaskCanceledException e)
		{
			if (e.InnerException is TimeoutException)
			{
				return 0;
			}
			throw;
		}
		
		response.EnsureSuccessStatusCode();

		var content = await response.Content.ReadAsStringAsync();
		var json = JsonDocument.Parse(content);
		
		request.Dispose();
		response.Dispose();

		var activeSessions = json.RootElement.EnumerateArray();

		int userCount = 0;
		var cutoffTime = DateTime.Now.AddMilliseconds(-_config.JellyfinActiveUserTime);

		foreach (var session in activeSessions)
		{
			if (!session.TryGetProperty("LastActivityDate", out JsonElement lastActivityElement))
			{
				continue;
			}

			if (DateTime.TryParse(lastActivityElement.GetString(), out DateTime lastActivityUtc))
			{
				if (lastActivityUtc >= cutoffTime)
				{
					userCount++;
				}
			}
		}

		ActiveUserCount = userCount;
		_log.Debug($"Jellyfin active user count: {userCount}");
		return userCount;
	}
	
	public interface IJellyfinConfig : ITrackerTimeout
	{
		string JellyfinUrl { get; set; }
		string JellyfinApiKey { get; set; }
		int JellyfinActiveUserTime { get; set; }
		
		public bool IsJellyfinConfigured() => string.IsNullOrEmpty(JellyfinUrl) && string.IsNullOrEmpty(JellyfinApiKey);
	}

	public void Update(Tracker tracker)
	{
		GetActiveUserCountAsync().Wait();
	}

	public void Display(Device device, Tracker tracker)
	{
		foreach (var trackerItem in tracker.Items)
		{
			switch (trackerItem.StatKey)
			{
				case "active_users":
					if (_lastCount != ActiveUserCount)
					{
						_lastCount = ActiveUserCount;
						device.DrawBarX((byte)ActiveUserCount, (byte)trackerItem.PosX);
					}
					break;
				default: throw new ArgumentException($"Unknown stat key: {trackerItem.StatKey}");
			}
		}
	}

	public void Init(Controller controller, Tracker tracker)
	{
		_log = controller.Log;
	}
}