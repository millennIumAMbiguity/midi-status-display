using Newtonsoft.Json;

namespace MidiStatusDisplay.core.Configuration;

public static class ConfigLoader
{
	public const string DEFAULT_PATH = "config.json";
	public static AppConfig LoadConfig(string filePath = DEFAULT_PATH)
	{
		if (!File.Exists(filePath))
		{
			File.WriteAllText(filePath, JsonConvert.SerializeObject(new AppConfig(), Formatting.Indented));
		}

		var json = File.ReadAllText(filePath);
		return JsonConvert.DeserializeObject<AppConfig>(json) ?? new AppConfig();
	}
}