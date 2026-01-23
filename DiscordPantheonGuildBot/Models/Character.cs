namespace DiscordPantheonGuildBot.Models;

public class Character {
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public int GameId { get; set; }
    public ulong UserId { get; set; }
    public required string CharacterName { get; set; }
    public string? Class { get; set; }
    public int Level { get; set; }
}
