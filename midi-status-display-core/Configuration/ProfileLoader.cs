using Newtonsoft.Json;

namespace MidiStatusDisplay.core.Configuration;

public static class ProfileLoader
{
	public const string DEFAULT_PATH = "profile.json";
	public static Profile LoadProfile(string filePath = DEFAULT_PATH)
	{
		if (!File.Exists(filePath))
		{
			File.WriteAllText(filePath, JsonConvert.SerializeObject(new Profile(), Formatting.Indented));
		}

		var json = File.ReadAllText(filePath);
		return JsonConvert.DeserializeObject<Profile>(json) ?? new Profile();
	}
}