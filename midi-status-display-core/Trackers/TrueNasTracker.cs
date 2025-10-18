using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MidiStatusDisplay.core.Configuration;

namespace MidiStatusDisplay.core.Trackers;

public class TrueNasTracker : ITracker
{
	private readonly HttpClient _httpClient;
	private Log _log = core.Log.None;
	
	public long LastUpdate { get; set; }
	
	private long LastQueryTime = DateTimeOffset.Now.ToUnixTimeSeconds() - 60;

	public Dictionary<string, Graph> Graphs { get; } = new();

	public TrueNasTracker(ITrueNasConfig config)
	{
		_httpClient = new HttpClient();
		_httpClient.BaseAddress = new Uri(config.TrueNasUrl.TrimEnd('/'));
		_httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.TrueNasApiKey}");
		_httpClient.Timeout = TimeSpan.FromMilliseconds(config.Timeout);
	}

	public interface ITrueNasConfig : ITrackerTimeout
	{
		string TrueNasUrl { get; set; }
		string TrueNasApiKey { get; set; }

		bool IsTruNasConfigured() => !string.IsNullOrEmpty(TrueNasApiKey) && !string.IsNullOrEmpty(TrueNasUrl);
	}
	
	public async Task CalculateAverages()
	{
		var jsonBody = "{\"graphs\":[{\"name\":\"interface\",\"identifier\":\"enp2s0\"}],\"reporting_query\":{\"page\":1,\"start\":" + LastQueryTime + ",\"aggregate\":true}}";

		var request = new HttpRequestMessage(HttpMethod.Post, "/api/v2.0/reporting/get_data");
		request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
		HttpResponseMessage? response;

		try
		{
			response = await _httpClient.SendAsync(request);
		}
		catch (TaskCanceledException e)
		{
			if (e.InnerException is TimeoutException)
			{
				return;
			}
			throw;
		}
		
		
		response.EnsureSuccessStatusCode();
		
		string json = await response.Content.ReadAsStringAsync();
		
		request?.Dispose();
		response?.Dispose();

		CalculateAverages(json);
	}
	
	private void CalculateAverages(string json)
	{
		// Deserialize the JSON array of GraphResponse
		var graphs = JsonSerializer.Deserialize<List<GraphResponse>>(json);
		
		if (graphs == null || graphs.Count == 0)
		{
			_log.Warn("TrueNas: No graphs found in the response.");
			return;
		}

		LastQueryTime = graphs[0].End;

		foreach (var graph in graphs)
		{
			_log.Debug($"TrueNas: Metric: {graph.Name} ({graph.Identifier})");

			if (graph.Data.Length == 0)
			{
				_log.Warn("TrueNas: No data points available.");
				continue;
			}

			bool hasTime = graph.Legend.Length > 0 && graph.Legend[0] == "time";
			int length;
			int lengthB = graph.Legend.Length;
			int start = 0;
			if (hasTime)
			{
				length = graph.Legend.Length - 1;
				start = 1;
				graph.Legend = graph.Legend.Skip(1).ToArray();
			}
			else
			{
				length = graph.Legend.Length;
			}

			double[] sums = new double[length];
			int count = 0;

			foreach (var point in graph.Data)
			{
				for (int i = start; i < lengthB; i++)
				{
					sums[i-start] += point[i];
				}
				count++;
			}

			if (count > 0)
			{

				if (!Graphs.TryGetValue(graph.Name, out Graph? g))
				{
					Graphs[graph.Name] = g = new Graph(length, graph.Legend);
				}
				
				g.UpdateAggregations(graph.Aggregations);
				for (int i = 0; i < length; i++)
				{
					g.Aggregations[i].Average = (float)(sums[i] / count);
				}
				
				Log(graph.Name, g);
			}
			else
			{
				_log.Warn("TrueNas: No valid data points to calculate average.");
			}
		}
	}
	
	private class GraphResponse
	{
		[JsonPropertyName("name")]
		public required string Name { get; set; }

		[JsonPropertyName("identifier")]
		public required string Identifier { get; set; }

		[JsonPropertyName("data")]
		public required float[][] Data { get; set; }

		[JsonPropertyName("legend")]
		public required string[] Legend { get; set; }

		[JsonPropertyName("start")]
		public long Start { get; set; }

		[JsonPropertyName("end")]
		public long End { get; set; }

		[JsonPropertyName("aggregations")]
		public required Aggregations Aggregations { get; set; }
	}

	public class Aggregations
	{
		[JsonPropertyName("min")]
		public required Dictionary<string, float> Min { get; set; }

		[JsonPropertyName("mean")]
		public required Dictionary<string, float> Mean { get; set; }

		[JsonPropertyName("max")]
		public required Dictionary<string, float> Max { get; set; }
	}
	
	public class Aggregation
	{
		public float Min { get; set; }
		public float Mean { get; set; }
		public float Max { get; set; }
		public float Average { get; set; }
		
		public override string ToString() 
		{
			return $"Min: {Min:F}, Mean: {Mean:F}, Max: {Max:F}, Average: {Average:F}";
		}
	}

	public class Graph
	{
		public readonly string[] Legend;
		public readonly Aggregation[] Aggregations;

		public Graph(int lenght, IReadOnlyCollection<string> legend)
		{
			Legend = legend.ToArray();
			Aggregations = new Aggregation[lenght];

			for (int i = 0; i < lenght; i++)
			{
				Aggregations[i] = new();
			}
		}
		
		public void UpdateAggregations(Aggregations aggregations)
		{
			for (int i = 0; i < Aggregations.Length; i++)
			{
				Aggregations[i].Min = aggregations.Min[Legend[i]];
				Aggregations[i].Mean = aggregations.Mean[Legend[i]];
				Aggregations[i].Max = aggregations.Max[Legend[i]];
			}
		}
	}

	[Conditional("DEBUG")]
	public void Log(string key)
	{
		var g = Graphs[key];
		Log(key, g);
	}
	
	[Conditional("DEBUG")]
	public void Log(string key, Graph g)
	{
		switch (key)
		{
			case "interface":
				_log.Debug(g.Legend[0] + ": " + g.Aggregations[0] + " kbit/s");
				_log.Debug(g.Legend[1] + ": " + g.Aggregations[1] + " kbit/s");
				break;
			default:
				for (int i = 0; i < g.Legend.Length; i++)
				{
					_log.Debug(g.Legend[i] + ": " + g.Aggregations[i]);
				}
				break;
		}
	}

	public void Update(Tracker tracker)
	{
		CalculateAverages().Wait();
	}
	
	public void Display(Device device, Tracker tracker)
	{
		if (Graphs.Count == 0) return;

		foreach (var trackerItem in tracker.Items)
		{
			switch (trackerItem.StatKey)
			{
				case "interface":
					var networkUsage = Graphs["interface"];
					//Log("interface", networkUsage);
					if (trackerItem.StatValue.Length == "receive".Length)
					{
						DrawAggregationBarX(device, networkUsage.Aggregations[0], (byte)trackerItem.PosX);
					}
					else
					{
						DrawAggregationBarX(device, networkUsage.Aggregations[1], (byte)trackerItem.PosX);
					}
					break;
				default: throw new ArgumentException($"Unknown stat key: {trackerItem.StatKey}");
			}
		}
	}
	
	private static void DrawAggregationBarX(Device device, Aggregation ag, byte x)
	{
		const float barScale = 0.000125f; // scale from kilobit to megabyte.
		device.DrawBarX((byte)(ag.Max * barScale), x, 7);
		device.DrawBarX((byte)(ag.Average * barScale), x, 15, false);
		device.DrawBarX((byte)(ag.Min * barScale), x, 23, false);
	}
	
	public void Init(Controller controller, Tracker tracker)
	{
		LastQueryTime = DateTimeOffset.Now.ToUnixTimeSeconds() - tracker.UpdateInterval/1000;
		_log = controller.Log;
	}
}