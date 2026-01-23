namespace DiscordPantheonGuildBot.Models;

public class Game {
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? WelcomeMessage { get; set; }
}
