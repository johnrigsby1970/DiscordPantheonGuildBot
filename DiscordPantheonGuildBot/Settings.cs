using Newtonsoft.Json;

namespace DiscordPantheonGuildBot;

internal class SettingsReader {
    public string? Token { get; set; }
    public string? Prefix { get; set; }

    public async Task ReadConfigJson() {
        string path = "config.json";
        if (!File.Exists(path)) {
            // Check if it's in the project directory if not found in current directory
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
        }
        
        if (!File.Exists(path)) {
            throw new FileNotFoundException($"config.json not found. Looked in: {Directory.GetCurrentDirectory()} and {AppDomain.CurrentDomain.BaseDirectory}");
        }
        
        using (StreamReader sr = new StreamReader(path)) {
            string json = await sr.ReadToEndAsync();
            if(string.IsNullOrWhiteSpace(json)) throw new Exception("config.json is empty.");
            Settings settings = JsonConvert.DeserializeObject<Settings>(json) ?? throw new InvalidOperationException();
            Token = settings.Token;
            Prefix = settings.Prefix;
        }
    }
}

internal sealed class Settings {
    public required string Token { get; set; }
    public required string Prefix { get; set; }
}