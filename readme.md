# Discord Pantheon Guild Bot

A comprehensive Discord bot designed for multi-game guild management, character tracking, and attendance logging. Built with C# and .NET 10 using the DSharpPlus library. Generally built around and to work with Pantheon Rise of the Fallen, this is generic enough to manage other games.

## Features

- **Multi-Game Support**: Manage multiple games within a single Discord server.
- **Guild Roster Management**: Track guild members, their status, and roles.
- **Character Tracking**: Users can register and update their characters (name, class, level).
- **Attendance Logging**: Log attendance based on online status, voice channel presence, or specific text channels.
- **Role Mapping**: Map Discord roles to internal guild statuses for authorization and management.
- **Class Configuration**: Customizable character classes per game.
- **Channel Assignment**: Assign specific Discord channels to games for contextual commands.

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A Discord Bot Token (from the [Discord Developer Portal](https://discord.com/developers/applications))

### Installation

1. Clone the repository.
2. Navigate to the project directory: `cd DiscordPantheonGuildBot/DiscordPantheonGuildBot`
3. Update `config.json` with your bot token and desired prefix:
   ```json
   {
     "token": "YOUR_BOT_TOKEN",
     "prefix": "!"
   }
   ```
4. Build and run the project:
   ```bash
   dotnet run
   ```

## Configuration

The bot uses a SQLite database (`guildbot.db` by default) to store all data. Configuration is handled through `config.json` for basic bot settings and in-game commands for guild-specific settings.

## Command Reference

### Guild Commands
- `!add <name> [class] [level]` - Register a character.
- `!update <name> [class] [level]` - Update character details.
- `!level <name> [level]` - Update character level.
- `!remove <name>` - Removes character or all characters if name not provided.
- `!list` - List characters for Discord member.
- `!setroster` - An officer level command to update a pinned roster message and associated attachments.
- `!showroster` - Display the guild roster.
- `!promote <member> <role>` - Promote a Discord member within the guild to a specified level, member, officer, leader.
- `!removeeuser <member>` - a leader level command to remove all characters for a Discord member.
- `!cleanroster <member>` - a leader level command to remove all characters that are not owned by a current Discord member.

### Game Management
- `!game create <name>` - Create a new game entry.
- `!game delete <name>` - Create a new game entry.
- `!game assignchannel` - Assign the current channel to a game.
- `!game unassignchannel` - Assign the specified or current channel to a game.
- `!game unassignallchannels <name>` - Assign the specified or current channel to a game.
- `!game listchannels <name>` - Create a new game entry.
- `!game list` - List all managed games.

### Attendance
- `!attendance log <name> <scope> [channel]` - Log attendance for a session.
- `!attendance show <name>` - View attendance for a specific session.

### Configuration
- `!config addclass <className>` - Add a character class to the current game.
- `!config maprole <role> <status>` - Map a Discord role to a guild status.

*Note: Use `!help` (if available) or explore the `Commands` folder for a full list of commands.*

## Technologies Used

- **Language**: C# 14
- **Framework**: .NET 10
- **Library**: [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus)
- **Database**: SQLite (via `DatabaseService`)

## License

This project is licensed under the terms of the `LICENSE.txt` file included in the repository.
