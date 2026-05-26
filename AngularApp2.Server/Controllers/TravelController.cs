using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace AngularApp2.Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TravelController : ControllerBase
    {

        // ВАЖНО: Укажи свои данные подключения к MySQL
        private string connString = "Server=localhost;Database=avto;Uid=root;Pwd=password666;";

        [HttpGet("routes")]
        public IActionResult GetRoutes()
        {
            var list = new List<string>();
            using var conn = new MySqlConnection(connString);
            conn.Open();
            var cmd = new MySqlCommand("SELECT id, route_name, price FROM routes", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add($"{reader["id"]} | {reader["route_name"]} | {reader["price"]} BYN");
            }
            return Ok(list);
        }

        [HttpPost("register")]
        public IActionResult Register([FromBody] Passenger p)
        {
            using var conn = new MySqlConnection(connString);
            conn.Open();
            var cmd = new MySqlCommand("INSERT INTO passengers (first_name, last_name) VALUES (@f, @l)", conn);
            cmd.Parameters.AddWithValue("@f", p.FirstName);
            cmd.Parameters.AddWithValue("@l", p.LastName);
            cmd.ExecuteNonQuery();
            return Ok(new { message = "Успешно!" });
        }
    }

    public class Passenger { public string FirstName { get; set; } public string LastName { get; set; } }
}
