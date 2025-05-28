using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Controls;

namespace ITRequestApp
{
    public class DatabaseManager
    {
        private readonly string _connectionString;

        public DatabaseManager()
        {
            string dbPath = "requests.db";
            _connectionString = $"Data Source={dbPath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            if (!File.Exists("requests.db"))
            {
                SQLiteConnection.CreateFile("requests.db");
            }

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            var commands = new[]
            {
                @"CREATE TABLE IF NOT EXISTS Departments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL
                )",
                @"CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Password TEXT NOT NULL,
                    Role TEXT,
                    DepartmentId INTEGER NOT NULL,
                    FOREIGN KEY (DepartmentId) REFERENCES Departments(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS Requests (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Description TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    FilePath TEXT,
                    CreatedAt TEXT NOT NULL,
                    AssignedAdminId INTEGER,
                    CabinetNumber TEXT,
                    IsCompletedByUser BOOLEAN DEFAULT 0,
                    FOREIGN KEY (UserId) REFERENCES Users(Id),
                    FOREIGN KEY (AssignedAdminId) REFERENCES Users(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS Comments (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RequestId INTEGER NOT NULL,
                    UserId INTEGER NOT NULL,
                    Text TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (RequestId) REFERENCES Requests(Id),
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS AuditLogs (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    UserId INTEGER NOT NULL,
                    Action TEXT NOT NULL,
                    Details TEXT NOT NULL,
                    Timestamp TEXT NOT NULL,
                    FOREIGN KEY (UserId) REFERENCES Users(Id)
                )",
                @"CREATE TABLE IF NOT EXISTS Reviews (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    RequestId INTEGER NOT NULL,
                    AdminId INTEGER NOT NULL,
                    Text TEXT,
                    CreatedAt TEXT NOT NULL,
                    FOREIGN KEY (RequestId) REFERENCES Requests(Id),
                    FOREIGN KEY (AdminId) REFERENCES Users(Id)
                )"
            };

            foreach (var commandText in commands)
            {
                using var command = new SQLiteCommand(commandText, connection);
                command.ExecuteNonQuery();
            }

            var deptCommands = new[]
            {
                "INSERT OR IGNORE INTO Departments (Id, Name) VALUES (1, 'Отдел кадров')",
                "INSERT OR IGNORE INTO Departments (Id, Name) VALUES (2, 'Финансовый отдел')",
                "INSERT OR IGNORE INTO Departments (Id, Name) VALUES (3, 'IT-отдел')"
            };
            foreach (var cmd in deptCommands)
            {
                using var command = new SQLiteCommand(cmd, connection);
                command.ExecuteNonQuery();
            }

            var userCommands = new[]
            {
                $"INSERT OR IGNORE INTO Users (Id, Name, Password, Role, DepartmentId) VALUES (1, 'Иван Иванов', '{HashPassword("password1")}', NULL, 1)",
                $"INSERT OR IGNORE INTO Users (Id, Name, Password, Role, DepartmentId) VALUES (2, 'Мария Петрова', '{HashPassword("password2")}', NULL, 2)",
                $"INSERT OR IGNORE INTO Users (Id, Name, Password, Role, DepartmentId) VALUES (3, 'Админ Админов', '{HashPassword("admin")}', 'Admin', 3)"
            };
            foreach (var cmd in userCommands)
            {
                using var command = new SQLiteCommand(cmd, connection);
                command.ExecuteNonQuery();
            }
        }

        public List<User> GetUsers()
        {
            var users = new List<User>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("SELECT Id, Name, Password, Role, DepartmentId FROM Users", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                users.Add(new User
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    Password = reader.GetString(2),
                    Role = reader.IsDBNull(3) ? null : reader.GetString(3),
                    DepartmentId = reader.GetInt32(4)
                });
            }
            return users;
        }

        public List<Department> GetDepartments()
        {
            var departments = new List<Department>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("SELECT Id, Name FROM Departments", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                departments.Add(new Department
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                });
            }
            return departments;
        }

        public List<Request> GetRequests(string statusFilter = null, int? userId = null)
        {
            var requests = new List<Request>();
            try
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
                    connection.Open();
                    string query = @"
                SELECT r.Id, r.UserId, r.Description, r.Status, r.FilePath, r.CreatedAt,
                       u.Name AS UserName, d.Name AS DepartmentName,
                       r.AssignedAdminId, a.Name AS AssignedAdminName,
                       r.CabinetNumber, r.IsCompletedByUser
                FROM Requests r
                JOIN Users u ON r.UserId = u.Id
                JOIN Departments d ON u.DepartmentId = d.Id
                LEFT JOIN Users a ON r.AssignedAdminId = a.Id
                WHERE 1=1";

                    if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Все")
                    {
                        query += " AND r.Status = @Status";
                    }
                    if (userId.HasValue)
                    {
                        query += " AND r.UserId = @UserId";
                    }

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        if (!string.IsNullOrEmpty(statusFilter) && statusFilter != "Все")
                        {
                            command.Parameters.AddWithValue("@Status", statusFilter);
                        }
                        if (userId.HasValue)
                        {
                            command.Parameters.AddWithValue("@UserId", userId.Value);
                        }

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                requests.Add(new Request
                                {
                                    Id = reader.GetInt32(0),
                                    UserId = reader.GetInt32(1),
                                    Description = reader.GetString(2),
                                    Status = reader.GetString(3),
                                    FilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
                                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                                    UserName = reader.GetString(6),
                                    DepartmentName = reader.GetString(7),
                                    AssignedAdminId = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8),
                                    AssignedAdminName = reader.IsDBNull(9) ? "Не назначен" : reader.GetString(9),
                                    CabinetNumber = reader.IsDBNull(10) ? null : reader.GetString(10),
                                    IsCompletedByUser = reader.GetBoolean(11)
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Ошибка при получении заявок: {ex.Message}", ex);
            }
            return requests;
        }

        public List<Comment> GetComments(int requestId)
        {
            var comments = new List<Comment>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                @"SELECT c.Id, c.RequestId, c.UserId, c.Text, c.CreatedAt, u.Name AS UserName
                  FROM Comments c
                  JOIN Users u ON c.UserId = u.Id
                  WHERE c.RequestId = @RequestId",
                connection);
            command.Parameters.AddWithValue("@RequestId", requestId);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                comments.Add(new Comment
                {
                    Id = reader.GetInt32(0),
                    RequestId = reader.GetInt32(1),
                    UserId = reader.GetInt32(2),
                    Text = reader.GetString(3),
                    CreatedAt = DateTime.Parse(reader.GetString(4)),
                    UserName = reader.GetString(5)
                });
            }
            return comments;
        }

        public List<AuditLog> GetAuditLogs()
        {
            var logs = new List<AuditLog>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("SELECT Id, UserId, Action, Details, Timestamp FROM AuditLogs", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                logs.Add(new AuditLog
                {
                    Id = reader.GetInt32(0),
                    UserId = reader.GetInt32(1),
                    Action = reader.GetString(2),
                    Details = reader.GetString(3),
                    Timestamp = DateTime.Parse(reader.GetString(4))
                });
            }
            return logs;
        }

        public void AddRequest(Request request)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "INSERT INTO Requests (UserId, Description, Status, FilePath, CreatedAt, AssignedAdminId, CabinetNumber, IsCompletedByUser) VALUES (@UserId, @Description, @Status, @FilePath, @CreatedAt, @AssignedAdminId, @CabinetNumber, @IsCompletedByUser)",
                connection);
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@Description", request.Description);
            command.Parameters.AddWithValue("@Status", request.Status);
            command.Parameters.AddWithValue("@FilePath", request.FilePath != null ? (object)request.FilePath : DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", request.CreatedAt.ToString("o"));
            command.Parameters.AddWithValue("@AssignedAdminId", request.AssignedAdminId.HasValue ? (object)request.AssignedAdminId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@CabinetNumber", request.CabinetNumber != null ? (object)request.CabinetNumber : DBNull.Value);
            command.Parameters.AddWithValue("@IsCompletedByUser", request.IsCompletedByUser ? 1 : 0);
            command.ExecuteNonQuery();
        }

        public void AddUser(User user)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "INSERT INTO Users (Name, Password, Role, DepartmentId) VALUES (@Name, @Password, @Role, @DepartmentId)",
                connection);
            command.Parameters.AddWithValue("@Name", user.Name);
            command.Parameters.AddWithValue("@Password", user.Password);
            command.Parameters.AddWithValue("@Role", (object?)user.Role ?? DBNull.Value);
            command.Parameters.AddWithValue("@DepartmentId", user.DepartmentId);
            command.ExecuteNonQuery();
        }

        public void DeleteUser(int userId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("DELETE FROM Users WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", userId);
            command.ExecuteNonQuery();
        }

        public void UpdateUsername(int userId, string newUsername)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("UPDATE Users SET Name = @Name WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", userId);
            command.Parameters.AddWithValue("@Name", newUsername);
            command.ExecuteNonQuery();
        }

        public void UpdatePassword(int userId, string newPassword)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("UPDATE Users SET Password = @Password WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", userId);
            command.Parameters.AddWithValue("@Password", newPassword);
            command.ExecuteNonQuery();
        }

        public void UpdateRequest(Request request)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "UPDATE Requests SET UserId = @UserId, Description = @Description, Status = @Status, FilePath = @FilePath, AssignedAdminId = @AssignedAdminId, CabinetNumber = @CabinetNumber, IsCompletedByUser = @IsCompletedByUser WHERE Id = @Id",
                connection);
            command.Parameters.AddWithValue("@Id", request.Id);
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@Description", request.Description);
            command.Parameters.AddWithValue("@Status", request.Status);
            command.Parameters.AddWithValue("@FilePath", request.FilePath != null ? (object)request.FilePath : DBNull.Value);
            command.Parameters.AddWithValue("@AssignedAdminId", request.AssignedAdminId.HasValue ? (object)request.AssignedAdminId.Value : DBNull.Value);
            command.Parameters.AddWithValue("@CabinetNumber", request.CabinetNumber != null ? (object)request.CabinetNumber : DBNull.Value);
            command.Parameters.AddWithValue("@IsCompletedByUser", request.IsCompletedByUser ? 1 : 0);
            command.ExecuteNonQuery();
        }

        public void UpdateRequestStatus(int requestId, string status)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("UPDATE Requests SET Status = @Status WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", requestId);
            command.Parameters.AddWithValue("@Status", status);
            command.ExecuteNonQuery();
        }

        public void DeleteRequest(int requestId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("DELETE FROM Requests WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", requestId);
            command.ExecuteNonQuery();
        }

        public void AssignAdminToRequest(int requestId, int adminId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "UPDATE Requests SET AssignedAdminId = @AdminId WHERE Id = @RequestId",
                connection);
            command.Parameters.AddWithValue("@RequestId", requestId);
            command.Parameters.AddWithValue("@AdminId", adminId);
            command.ExecuteNonQuery();
        }

        public void AddComment(Comment comment)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "INSERT INTO Comments (RequestId, UserId, Text, CreatedAt) VALUES (@RequestId, @UserId, @Text, @CreatedAt)",
                connection);
            command.Parameters.AddWithValue("@RequestId", comment.RequestId);
            command.Parameters.AddWithValue("@UserId", comment.UserId);
            command.Parameters.AddWithValue("@Text", comment.Text);
            command.Parameters.AddWithValue("@CreatedAt", comment.CreatedAt.ToString("o"));
            command.ExecuteNonQuery();
        }

        public void AddAuditLog(AuditLog log)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "INSERT INTO AuditLogs (UserId, Action, Details, Timestamp) VALUES (@UserId, @Action, @Details, @Timestamp)",
                connection);
            command.Parameters.AddWithValue("@UserId", log.UserId);
            command.Parameters.AddWithValue("@Action", log.Action);
            command.Parameters.AddWithValue("@Details", log.Details);
            command.Parameters.AddWithValue("@Timestamp", log.Timestamp.ToString("o"));
            command.ExecuteNonQuery();
        }

        public void UpdateRequestCompletion(int requestId, bool isCompleted)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand("UPDATE Requests SET IsCompletedByUser = @IsCompleted WHERE Id = @Id", connection);
            command.Parameters.AddWithValue("@Id", requestId);
            command.Parameters.AddWithValue("@IsCompleted", isCompleted ? 1 : 0);
            command.ExecuteNonQuery();
        }

        public void AddReview(Review review)
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                "INSERT INTO Reviews (RequestId, AdminId, Text, CreatedAt) VALUES (@RequestId, @AdminId, @Text, @CreatedAt)",
                connection);
            command.Parameters.AddWithValue("@RequestId", review.RequestId);
            command.Parameters.AddWithValue("@AdminId", review.AdminId);
            command.Parameters.AddWithValue("@Text", (object?)review.Text ?? DBNull.Value);
            command.Parameters.AddWithValue("@CreatedAt", review.CreatedAt.ToString("o"));
            command.ExecuteNonQuery();
        }

        public Dictionary<int, (int Week, int Month, int Year)> GetAdminStatistics()
        {
            var stats = new Dictionary<int, (int Week, int Month, int Year)>();
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            var command = new SQLiteCommand(
                @"SELECT AssignedAdminId,
                         SUM(CASE WHEN CreatedAt >= date('now', '-7 days') THEN 1 ELSE 0 END) as Week,
                         SUM(CASE WHEN CreatedAt >= date('now', '-1 month') THEN 1 ELSE 0 END) as Month,
                         SUM(CASE WHEN CreatedAt >= date('now', '-1 year') THEN 1 ELSE 0 END) as Year
                  FROM Requests
                  WHERE IsCompletedByUser = 1 AND AssignedAdminId IS NOT NULL
                  GROUP BY AssignedAdminId",
                connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                int adminId = reader.GetInt32(0);
                int week = reader.GetInt32(1);
                int month = reader.GetInt32(2);
                int year = reader.GetInt32(3);
                stats[adminId] = (week, month, year);
            }
            return stats;
        }

        public void BackupDatabase()
        {
            string backupPath = $"backup_{DateTime.Now:yyyyMMdd_HHmmss}.db";
            File.Copy("requests.db", backupPath);
        }

        public static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            var builder = new StringBuilder();
            foreach (byte b in bytes)
            {
                builder.Append(b.ToString("x2"));
            }
            return builder.ToString();
        }
    }
}