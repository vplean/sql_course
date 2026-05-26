using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;

namespace angularapp2.server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DbController : ControllerBase
    {
        private readonly string? _connectionString;

        public DbController(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString)) { conn.Open(); }
                return Ok(new { text = "Подключение к MySQL успешно установлено и стабильно!" });
            }
            catch (Exception ex) { return BadRequest(new { text = $"Ошибка: {ex.Message}" }); }
        }

        [HttpGet("toggle-role")]
        public IActionResult ToggleRole()
        {
            return Ok(new { currentRole = "Admin", text = "C# успешно переключил роль!" });
        }

        // === 1. ПОЛУЧИТЬ СПИСОК РЕЙСОВ ИЗ БД ===
        [HttpGet("routes")]
        public IActionResult GetRoutes()
        {
            var routes = new List<object>();
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new MySqlCommand("SELECT route_id, route_name, price FROM routes", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            routes.Add(new
                            {
                                id = reader["route_id"].ToString(),
                                name = $"{reader["route_name"]} | {reader["price"]} BYN"
                            });
                        }
                    }
                }
                return Ok(routes);
            }
            catch (Exception)
            {
                // Заглушка, если таблицы routes еще нет
                return Ok(new[] {
                    new { id = "1", name = "Минск-Гродно (Заглушка БД) | 25.00 BYN" }
                });
            }
        }

        // === 2. РЕГИСТРАЦИЯ ПАССАЖИРА ===
        [HttpPost("register")]
        public IActionResult Register([FromBody] RegisterDto data)
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    string sql = "INSERT INTO passengers (first_name, last_name, birthday, stat, route_id) VALUES (@f, @l, @b, 'active', @r); SELECT LAST_INSERT_ID();";
                    int newPassengerId = 0;
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@f", data.FirstName);
                        cmd.Parameters.AddWithValue("@l", data.LastName);
                        cmd.Parameters.AddWithValue("@b", data.Birthday);
                        cmd.Parameters.AddWithValue("@r", data.RouteId);
                        newPassengerId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    string tfaCode = new Random().Next(100000, 999999).ToString();
                    using (MySqlCommand procCmd = new MySqlCommand("VerifyClientStep1", conn))
                    {
                        procCmd.CommandType = System.Data.CommandType.StoredProcedure;
                        procCmd.Parameters.AddWithValue("p_id", newPassengerId);
                        procCmd.Parameters.AddWithValue("p_code", tfaCode);
                        procCmd.ExecuteNonQuery();
                    }

                    return Ok(new
                    {
                        message = $"Пассажир {data.LastName} добавлен (ID: {newPassengerId}).",
                        code = tfaCode,
                        passengerId = newPassengerId
                    });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Ошибка БД: {ex.Message}" });
            }
        }

        // === 3. ПОДТВЕРЖДЕНИЕ 2FA ===
        [HttpPost("confirm-2fa")]
        public IActionResult Confirm2FA([FromBody] ConfirmDto data)
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    using (MySqlCommand procCmd = new MySqlCommand("VerifyClientStep2", conn))
                    {
                        procCmd.CommandType = System.Data.CommandType.StoredProcedure;
                        procCmd.Parameters.AddWithValue("p_id", data.PassengerId);
                        procCmd.Parameters.AddWithValue("p_code", data.Code);
                        procCmd.ExecuteNonQuery();
                    }
                    return Ok(new { message = "Код подтвержден! Доступ открыт." });
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = $"Ошибка проверки: {ex.Message}" });
            }
        }

        // === 4. ДИНАМИЧЕСКАЯ ЗАГРУЗКА ТАБЛИЦ ===
        [HttpGet("table/{tableName}")]
        public IActionResult GetTableData(string tableName)
        {
            var rows = new List<Dictionary<string, object>>();
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new MySqlCommand($"SELECT * FROM {tableName} LIMIT 50", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var row = new Dictionary<string, object>();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                row[reader.GetName(i)] = reader.GetValue(i)?.ToString() ?? "";
                            }
                            rows.Add(row);
                        }
                    }
                }
                return Ok(rows);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // === МЕТОД: ПОЛУЧИТЬ СПИСОК ВСЕХ ТАБЛИЦ ===
        [HttpGet("tables-list")]
        public IActionResult GetTablesList()
        {
            var tables = new List<string>();
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    var cmd = new MySqlCommand("SHOW TABLES", conn);
                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read()) tables.Add(reader.GetString(0));
                    }
                }
                return Ok(tables);
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }

        // === МЕТОД: СОХРАНИТЬ ИЗМЕНЕНИЯ В БД ===
        [HttpPost("save-table/{tableName}")]
        public IActionResult SaveTable(string tableName, [FromBody] List<Dictionary<string, string>> rows)
        {
            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    conn.Open();
                    foreach (var row in rows)
                    {
                        if (row.Count == 0) continue;

                        // Берем первую колонку в качестве Primary Key (например, passenger_id)
                        var pkCol = row.Keys.First();
                        var pkVal = row[pkCol];

                        var setClauses = new List<string>();
                        using var cmd = new MySqlCommand();
                        cmd.Connection = conn;

                        // Динамически строим запрос UPDATE
                        foreach (var kvp in row)
                        {
                            if (kvp.Key == pkCol) continue; // Ключ не обновляем
                            setClauses.Add($"{kvp.Key} = @{kvp.Key}");
                            cmd.Parameters.AddWithValue($"@{kvp.Key}", kvp.Value ?? "");
                        }

                        cmd.Parameters.AddWithValue($"@{pkCol}", pkVal);
                        cmd.CommandText = $"UPDATE {tableName} SET {string.Join(", ", setClauses)} WHERE {pkCol} = @{pkCol}";
                        cmd.ExecuteNonQuery();
                    }
                }
                return Ok(new { message = "Изменения сохранены!" });
            }
            catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
        }
    }

    public class RegisterDto
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Birthday { get; set; } = "";
        public string RouteId { get; set; } = "1";
    }

    public class ConfirmDto
    {
        public int PassengerId { get; set; }
        public string Code { get; set; } = "";
    }
}