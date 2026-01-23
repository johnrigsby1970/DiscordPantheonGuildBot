namespace DiscordPantheonGuildBot.Models;

public class UserStatus {
    public int Id { get; set; }
    public ulong GuildId { get; set; }
    public ulong UserId { get; set; }
    public required string Status { get; set; } // member, officer, leader
}
