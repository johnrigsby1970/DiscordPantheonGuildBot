using System.Data;
using Microsoft.Data.Sqlite;
using DiscordPantheonGuildBot.Models;

namespace DiscordPantheonGuildBot.Data;

public class DatabaseService {
    private readonly string _connectionString;

    public DatabaseService(string dbPath = "guildbot.db") {
        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase() {
        // Delete the current database and start over if migration code is necessary.
        // Since we are changing core schemas significantly, we'll just drop all tables if a new required table is missing.
        using (var connection = new SqliteConnection(_connectionString)) {
            connection.Open();
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='Games';";
            var result = checkCommand.ExecuteScalar();
            if (result == null) {
                // Games table doesn't exist, start over
                var dropCommand = connection.CreateCommand();
                dropCommand.CommandText = @"
                    DROP TABLE IF EXISTS AttendanceRecords;
                    DROP TABLE IF EXISTS AttendanceSessions;
                    DROP TABLE IF EXISTS RoleMappings;
                    DROP TABLE IF EXISTS AllowedClasses;
                    DROP TABLE IF EXISTS UserStatuses;
                    DROP TABLE IF EXISTS Characters;
                ";
                dropCommand.ExecuteNonQuery();
            }
        }

        using var connection2 = new SqliteConnection(_connectionString);
        connection2.Open();

        var command = connection2.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS Games (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GuildId UNSIGNED BIG INT NOT NULL,
                Name TEXT NOT NULL,
                Description TEXT,
                WelcomeMessage TEXT,
                UNIQUE(GuildId, Name)
            );

            CREATE TABLE IF NOT EXISTS ChannelGameMappings (
                ChannelId UNSIGNED BIG INT PRIMARY KEY,
                GameId INTEGER NOT NULL,
                FOREIGN KEY(GameId) REFERENCES Games(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS Characters (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GuildId UNSIGNED BIG INT NOT NULL,
                GameId INTEGER NOT NULL,
                UserId UNSIGNED BIG INT NOT NULL,
                CharacterName TEXT NOT NULL,
                Class TEXT,
                Level INTEGER NOT NULL,
                UNIQUE(GameId, UserId, CharacterName),
                FOREIGN KEY(GameId) REFERENCES Games(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS UserStatuses (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GuildId UNSIGNED BIG INT NOT NULL,
                UserId UNSIGNED BIG INT NOT NULL,
                Status TEXT NOT NULL,
                UNIQUE(GuildId, UserId)
            );

            CREATE TABLE IF NOT EXISTS AllowedClasses (
                GameId INTEGER NOT NULL,
                ClassName TEXT NOT NULL,
                PRIMARY KEY(GameId, ClassName),
                FOREIGN KEY(GameId) REFERENCES Games(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS RoleMappings (
                GameId INTEGER NOT NULL,
                RoleId UNSIGNED BIG INT NOT NULL,
                Status TEXT NOT NULL,
                PRIMARY KEY(GameId, RoleId),
                FOREIGN KEY(GameId) REFERENCES Games(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AttendanceSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                GameId INTEGER NOT NULL,
                SessionName TEXT NOT NULL,
                Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                UNIQUE(GameId, SessionName),
                FOREIGN KEY(GameId) REFERENCES Games(Id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS AttendanceRecords (
                SessionId INTEGER NOT NULL,
                UserId UNSIGNED BIG INT NOT NULL,
                FOREIGN KEY(SessionId) REFERENCES AttendanceSessions(Id) ON DELETE CASCADE,
                PRIMARY KEY(SessionId, UserId)
            );
        ";
        command.ExecuteNonQuery();
    }

    private readonly string[] _defaultClasses = {
        "Bard", "Cleric", "Dire Lord", "Druid", "Enchanter", "Monk", "Necromancer",
        "Paladin", "Ranger", "Rogue", "Shaman", "Summoner", "Warrior", "Wizard"
    };

    public async Task<List<string>> GetAllowedClasses(int gameId) {
        var classes = new List<string>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT ClassName FROM AllowedClasses WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            classes.Add(reader.GetString(0));
        }

        if (classes.Count == 0) {
            // Seed defaults
            foreach (var cls in _defaultClasses) {
                await AddAllowedClass(gameId, cls);
                classes.Add(cls);
            }
        }

        return classes;
    }

    public async Task<bool> AddAllowedClass(int gameId, string className) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO AllowedClasses (GameId, ClassName) VALUES ($gameId, $className);";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$className", className);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> RemoveAllowedClass(int gameId, string className) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AllowedClasses WHERE GameId = $gameId AND ClassName = $className;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$className", className);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> ClearAllowedClasses(int gameId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AllowedClasses WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        return await command.ExecuteNonQueryAsync();
    }

    public async Task ResetAllowedClasses(int gameId) {
        await ClearAllowedClasses(gameId);
        // Re-seed defaults
        foreach (var cls in _defaultClasses) {
            await AddAllowedClass(gameId, cls);
        }
    }

    public async Task<bool> SetRoleMapping(int gameId, ulong roleId, string status) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO RoleMappings (GameId, RoleId, Status)
            VALUES ($gameId, $roleId, $status)
            ON CONFLICT(GameId, RoleId) DO UPDATE SET Status = $status;
        ";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$roleId", roleId);
        command.Parameters.AddWithValue("$status", status.ToLower());

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> RemoveRoleMapping(int gameId, ulong roleId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RoleMappings WHERE GameId = $gameId AND RoleId = $roleId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$roleId", roleId);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> ClearRoleMappings(int gameId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM RoleMappings WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<ulong, string>> GetRoleMappings(int gameId) {
        var mappings = new Dictionary<ulong, string>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT RoleId, Status FROM RoleMappings WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            mappings.Add((ulong)reader.GetInt64(0), reader.GetString(1));
        }

        return mappings;
    }

    public async Task<bool> AddCharacter(Character character) {
        try {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            command.CommandText = @"
            UPDATE Characters SET Class=$class, Level=$level 
            WHERE GuildId=$guildId AND GameId=$gameId AND UserId=$userId AND CharacterName=$characterName;
        ";

            command.Parameters.AddWithValue("$guildId", character.GuildId);
            command.Parameters.AddWithValue("$gameId", character.GameId);
            command.Parameters.AddWithValue("$userId", character.UserId);
            command.Parameters.AddWithValue("$characterName", character.CharacterName);
            command.Parameters.AddWithValue("$class", (object?)character.Class ?? DBNull.Value);
            command.Parameters.AddWithValue("$level", character.Level);

            int rowsAffected = await command.ExecuteNonQueryAsync();

            if (rowsAffected == 0) {
                command.CommandText = @"
            INSERT OR IGNORE INTO Characters (GuildId, GameId, UserId, CharacterName, Class, Level)
            VALUES ($guildId, $gameId, $userId, $characterName, $class, $level);
        ";
                rowsAffected = await command.ExecuteNonQueryAsync();
            }
        
            return rowsAffected > 0;
        }
        catch (Exception ex) {
            Console.WriteLine(ex);
            return false;
        }
        
    }

    public async Task<bool> UpdateCharacterLevel(int gameId, ulong userId, string name, int level) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE Characters 
            SET Level = $level 
            WHERE GameId = $gameId AND UserId = $userId AND CharacterName = $name;
        ";
        command.Parameters.AddWithValue("$level", level);
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$name", name);

        int rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> RemoveCharacter(int gameId, ulong userId, string name) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "DELETE FROM Characters WHERE GameId = $gameId AND UserId = $userId AND CharacterName = $name;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$name", name);

        int rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<int> RemoveAllCharacters(int gameId, ulong userId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Characters WHERE GameId = $gameId AND UserId = $userId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$userId", userId);

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<List<Character>> GetUserCharacters(int gameId, ulong userId) {
        var characters = new List<Character>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Characters WHERE GameId = $gameId AND UserId = $userId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$userId", userId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            characters.Add(new Character {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                GameId = reader.GetInt32(2),
                UserId = (ulong)reader.GetInt64(3),
                CharacterName = reader.GetString(4),
                Class = reader.IsDBNull(5) ? null : reader.GetString(5),
                Level = reader.GetInt32(6)
            });
        }

        return characters;
    }

    public async Task<List<Character>> GetCharactersByName(int gameId, string name) {
        var characters = new List<Character>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Characters WHERE GameId = $gameId AND CharacterName = $name;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$name", name);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            characters.Add(new Character {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                GameId = reader.GetInt32(2),
                UserId = (ulong)reader.GetInt64(3),
                CharacterName = reader.GetString(4),
                Class = reader.IsDBNull(5) ? null : reader.GetString(5),
                Level = reader.GetInt32(6)
            });
        }

        return characters;
    }

    public async Task<List<Character>> GetRoster(int gameId, string? orderBy = null, string? filterClass = null,
        int? minLevel = null, int? maxLevel = null) {
        var characters = new List<Character>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        var query = "SELECT * FROM Characters WHERE GameId = $gameId";

        if (!string.IsNullOrEmpty(filterClass)) {
            query += " AND Class = $filterClass";
            command.Parameters.AddWithValue("$filterClass", filterClass);
        }

        if (minLevel.HasValue) {
            query += " AND Level >= $minLevel";
            command.Parameters.AddWithValue("$minLevel", minLevel.Value);
        }

        if (maxLevel.HasValue) {
            query += " AND Level <= $maxLevel";
            command.Parameters.AddWithValue("$maxLevel", maxLevel.Value);
        }

        if (orderBy == "class") {
            query += " ORDER BY Class ASC, CharacterName ASC";
        }
        else if (orderBy == "level") {
            query += " ORDER BY Level DESC, CharacterName ASC";
        }
        else {
            query += " ORDER BY CharacterName ASC";
        }

        command.CommandText = query;
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            characters.Add(new Character {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                GameId = reader.GetInt32(2),
                UserId = (ulong)reader.GetInt64(3),
                CharacterName = reader.GetString(4),
                Class = reader.IsDBNull(5) ? null : reader.GetString(5),
                Level = reader.GetInt32(6)
            });
        }

        return characters;
    }

    public async Task<int> RemoveUserCharacters(int gameId, ulong userId) {
        return await RemoveAllCharacters(gameId, userId);
    }

    public async Task<int> CleanRoster(int gameId, List<ulong> memberIds) {
        if (memberIds.Count == 0) return 0;

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            $"DELETE FROM Characters WHERE GameId = $gameId AND UserId NOT IN ({string.Join(",", memberIds)});";
        command.Parameters.AddWithValue("$gameId", gameId);

        return await command.ExecuteNonQueryAsync();
    }

    public async Task<string?> GetUserStatus(ulong guildId, ulong userId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM UserStatuses WHERE GuildId = $guildId AND UserId = $userId;";
        command.Parameters.AddWithValue("$guildId", guildId);
        command.Parameters.AddWithValue("$userId", userId);

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    public async Task<bool> SetUserStatus(ulong guildId, ulong userId, string status) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO UserStatuses (GuildId, UserId, Status)
            VALUES ($guildId, $userId, $status)
            ON CONFLICT(GuildId, UserId) DO UPDATE SET Status = $status;
        ";
        command.Parameters.AddWithValue("$guildId", guildId);
        command.Parameters.AddWithValue("$userId", userId);
        command.Parameters.AddWithValue("$status", status.ToLower());

        int rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<bool> CreateAttendanceSession(int gameId, string sessionName, List<ulong> userIds) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try {
            var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "INSERT INTO AttendanceSessions (GameId, SessionName) VALUES ($gameId, $sessionName); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("$gameId", gameId);
            command.Parameters.AddWithValue("$sessionName", sessionName);

            var sessionId = (long)(await command.ExecuteScalarAsync())!;

            foreach (var userId in userIds) {
                var recordCommand = connection.CreateCommand();
                recordCommand.Transaction = transaction;
                recordCommand.CommandText =
                    "INSERT INTO AttendanceRecords (SessionId, UserId) VALUES ($sessionId, $userId);";
                recordCommand.Parameters.AddWithValue("$sessionId", sessionId);
                recordCommand.Parameters.AddWithValue("$userId", userId);
                await recordCommand.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
            return true;
        }
        catch {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> AddUserToAttendanceSession(int gameId, string sessionName, ulong userId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try {
            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id FROM AttendanceSessions WHERE GameId = $gameId AND SessionName = $sessionName;";

            command.Parameters.AddWithValue("$gameId", gameId);
            command.Parameters.AddWithValue("$sessionName", sessionName);
            var result = await command.ExecuteScalarAsync();
            int? sessionId = result != null ? Convert.ToInt32(result) : null;

            if (sessionId.HasValue == false) return false;

            var recordCommand = connection.CreateCommand();
            recordCommand.Transaction = transaction;
            recordCommand.CommandText =
                "INSERT INTO AttendanceRecords (SessionId, UserId) VALUES ($sessionId, $userId);";
            recordCommand.Parameters.AddWithValue("$sessionId", sessionId);
            recordCommand.Parameters.AddWithValue("$userId", userId);
            await recordCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> RemoveUserFromAttendanceSession(int gameId, string sessionName, ulong userId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = connection.BeginTransaction();
        try {
            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id FROM AttendanceSessions WHERE GameId = $gameId AND SessionName = $sessionName;";

            command.Parameters.AddWithValue("$gameId", gameId);
            command.Parameters.AddWithValue("$sessionName", sessionName);
            var result = await command.ExecuteScalarAsync();
            int? sessionId = result != null ? Convert.ToInt32(result) : null;

            if (sessionId.HasValue == false) return false;

            var recordCommand = connection.CreateCommand();
            recordCommand.Transaction = transaction;
            recordCommand.CommandText = "DELETE FROM AttendanceRecords WHERE SessionId=$sessionId AND UserId=$userId;";
            recordCommand.Parameters.AddWithValue("$sessionId", sessionId);
            recordCommand.Parameters.AddWithValue("$userId", userId);
            await recordCommand.ExecuteNonQueryAsync();

            await transaction.CommitAsync();
            return true;
        }
        catch {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<List<(string Name, DateTime Timestamp)>> GetAttendanceSessions(int gameId) {
        var sessions = new List<(string Name, DateTime Timestamp)>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT SessionName, Timestamp FROM AttendanceSessions WHERE GameId = $gameId ORDER BY Timestamp DESC;";
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            sessions.Add((reader.GetString(0), reader.GetDateTime(1)));
        }

        return sessions;
    }

    public async Task<bool> DeleteAttendanceSession(int gameId, string sessionName) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM AttendanceSessions WHERE GameId = $gameId AND SessionName = $sessionName;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$sessionName", sessionName);

        int rowsAffected = await command.ExecuteNonQueryAsync();
        return rowsAffected > 0;
    }

    public async Task<List<ulong>> GetAttendanceSessionDetails(int gameId, string sessionName) {
        var userIds = new List<ulong>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT UserId FROM AttendanceRecords 
            WHERE SessionId = (SELECT Id FROM AttendanceSessions WHERE GameId = $gameId AND SessionName = $sessionName);
        ";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$sessionName", sessionName);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            userIds.Add((ulong)reader.GetInt64(0));
        }

        return userIds;
    }

    // New Game Management Methods
    public async Task<int> CreateGame(ulong guildId, string name) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Games (GuildId, Name) VALUES ($guildId, $name); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$guildId", guildId);
        command.Parameters.AddWithValue("$name", name);

        try {
            var result = await command.ExecuteScalarAsync();
            return result != null ? Convert.ToInt32(result) : -1;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19) {
            // UNIQUE constraint failed
            return -2;
        }
    }
    
    public async Task<bool> UpdateGameName(int gameId, string name) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "Update Games SET name=$name WHERE Id=$gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);
        command.Parameters.AddWithValue("$name", name);

        try {
            int rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (SqliteException) {
            return false;
        }
    }

    public async Task<Game?> GetGame(ulong guildId, int gameId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games WHERE GuildId = $guildId AND Id = $gameId;";
        command.Parameters.AddWithValue("$guildId", guildId);
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync()) {
            return new Game {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                WelcomeMessage = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }

        return null;
    }

    public async Task<Game?> GetGameByName(ulong guildId, string name) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games WHERE GuildId = $guildId AND Name = $name COLLATE NOCASE;";
        command.Parameters.AddWithValue("$guildId", guildId);
        command.Parameters.AddWithValue("$name", name);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync()) {
            return new Game {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                WelcomeMessage = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }

        return null;
    }

    public async Task<List<Game>> ListGames(ulong guildId) {
        var games = new List<Game>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT * FROM Games WHERE GuildId = $guildId;";
        command.Parameters.AddWithValue("$guildId", guildId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            games.Add(new Game {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                WelcomeMessage = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return games;
    }

    public async Task<bool> UpdateGameDescription(int gameId, string? description) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Games SET Description = $description WHERE Id = $gameId;";
        command.Parameters.AddWithValue("$description", (object?)description ?? DBNull.Value);
        command.Parameters.AddWithValue("$gameId", gameId);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> UpdateGameWelcomeMessage(int gameId, string? welcomeMessage) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Games SET WelcomeMessage = $welcomeMessage WHERE Id = $gameId;";
        command.Parameters.AddWithValue("$welcomeMessage", (object?)welcomeMessage ?? DBNull.Value);
        command.Parameters.AddWithValue("$gameId", gameId);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteGame(int gameId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Games WHERE Id = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> AssignChannelToGame(ulong channelId, int gameId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText = "INSERT INTO ChannelGameMappings (ChannelId, GameId) VALUES ($channelId, $gameId);";
        command.Parameters.AddWithValue("$channelId", channelId);
        command.Parameters.AddWithValue("$gameId", gameId);

        try {
            int rows = await command.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (SqliteException) {
            return false;
        }
    }

    public async Task<bool> UnassignChannel(ulong channelId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ChannelGameMappings WHERE ChannelId = $channelId;";
        command.Parameters.AddWithValue("$channelId", channelId);

        int rows = await command.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<int> UnassignAllChannelsFromGame(int gameId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ChannelGameMappings WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        return await command.ExecuteNonQueryAsync();
    }
    public async Task<bool> CanConnect() {
        try {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            if (connection.State == ConnectionState.Open) {
                return true;
            }
            return false;
        }
        catch (Exception) {
            return false;
        }
    }
    public async Task<Game?> GetGameByChannel(ulong channelId) {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT g.* FROM Games g
            JOIN ChannelGameMappings cgm ON g.Id = cgm.GameId
            WHERE cgm.ChannelId = $channelId;
        ";
        command.Parameters.AddWithValue("$channelId", channelId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync()) {
            return new Game {
                Id = reader.GetInt32(0),
                GuildId = (ulong)reader.GetInt64(1),
                Name = reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                WelcomeMessage = reader.IsDBNull(4) ? null : reader.GetString(4)
            };
        }

        return null;
    }

    public async Task<List<ulong>> GetChannelsByGame(int gameId) {
        var channelIds = new List<ulong>();
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT ChannelId FROM ChannelGameMappings WHERE GameId = $gameId;";
        command.Parameters.AddWithValue("$gameId", gameId);

        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) {
            channelIds.Add((ulong)reader.GetInt64(0));
        }

        return channelIds;
    }
}