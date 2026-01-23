namespace DiscordPantheonGuildBot.Models;

public class RosterEntry {
    public required string CharacterName { get; set; }
    public string? Class { get; set; }
    public string? Level { get; set; }
    public required string Member { get; set; }
}