using System;
using System.IO;
using Microsoft.Data.Sqlite;

namespace OOPWPFProject
{
    public static class DatabaseManager
    {
        private static readonly string DataDir = "Data";
        private static readonly string DbPath = Path.Combine(DataDir, "orders.db");
        public static string ConnectionString => $"Data Source={DbPath};";

        public static void Initialize()
        {
            if (!Directory.Exists(DataDir))
                Directory.CreateDirectory(DataDir);

            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();

                new SqliteCommand(@"
                    CREATE TABLE IF NOT EXISTS Users (
                        Id           INTEGER PRIMARY KEY AUTOINCREMENT,
                        Login        TEXT NOT NULL UNIQUE,
                        PasswordHash TEXT NOT NULL,
                        Role         TEXT NOT NULL
                    );", conn).ExecuteNonQuery();

                new SqliteCommand(@"
                    CREATE TABLE IF NOT EXISTS Orders (
                        Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                        Type            TEXT NOT NULL,
                        ProductName     TEXT NOT NULL,
                        Quantity        INTEGER NOT NULL,
                        Price           REAL NOT NULL,
                        DeliveryAddress TEXT,
                        TrackingNumber  TEXT,
                        StoreLocation   TEXT,
                        PickupTime      TEXT,
                        OrderState      TEXT NOT NULL DEFAULT 'Нове',
                        AssignedCourier TEXT
                    );", conn).ExecuteNonQuery();

                new SqliteCommand(@"
                    CREATE TABLE IF NOT EXISTS Logs (
                        Id          INTEGER PRIMARY KEY AUTOINCREMENT,
                        Timestamp   TEXT NOT NULL,
                        Action      TEXT NOT NULL,
                        Description TEXT NOT NULL,
                        UserLogin   TEXT
                    );", conn).ExecuteNonQuery();

                long count = (long)new SqliteCommand(
                    "SELECT COUNT(*) FROM Users;", conn).ExecuteScalar();

                if (count == 0)
                {
                    new SqliteCommand($@"
                        INSERT INTO Users (Login, PasswordHash, Role) VALUES
                        ('admin',    '{HashPassword("admin123")}', 'Admin'),
                        ('operator', '{HashPassword("oper123")}',  'Operator');",
                        conn).ExecuteNonQuery();
                }
            }
        }

        public static string HashPassword(string password)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        public static (bool success, string role) Login(string login, string password)
        {
            using (var conn = new SqliteConnection(ConnectionString))
            {
                conn.Open();
                var cmd = new SqliteCommand(
                    "SELECT Role FROM Users WHERE Login=@l AND PasswordHash=@p;", conn);
                cmd.Parameters.AddWithValue("@l", login);
                cmd.Parameters.AddWithValue("@p", HashPassword(password));

                var result = cmd.ExecuteScalar();
                return result != null
                    ? (true, result.ToString())
                    : (false, null);
            }
        }
    }

    public static class Logger
    {
        private static readonly string _logPath = Path.Combine("Data", "log.txt");
        public static string CurrentUser { get; set; } = "system";

        public static void Log(string action, string description)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string line = $"[{timestamp}] [{CurrentUser}] {action}: {description}";

            try
            {
                using (var writer = new StreamWriter(_logPath, append: true))
                    writer.WriteLine(line);
            }
            catch { }

            try
            {
                using (var conn = new SqliteConnection(DatabaseManager.ConnectionString))
                {
                    conn.Open();
                    var cmd = new SqliteCommand(@"
                        INSERT INTO Logs (Timestamp, Action, Description, UserLogin)
                        VALUES (@t, @a, @d, @u);", conn);
                    cmd.Parameters.AddWithValue("@t", timestamp);
                    cmd.Parameters.AddWithValue("@a", action);
                    cmd.Parameters.AddWithValue("@d", description);
                    cmd.Parameters.AddWithValue("@u", CurrentUser);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { }
        }
    }
}