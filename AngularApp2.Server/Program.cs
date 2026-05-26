using MySql.Data.MySqlClient;

var builder = WebApplication.CreateBuilder(args);

// Добавление контроллеров в контейнер сервисов
builder.Services.AddControllers();

// Настройка Swagger для тестирования API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Настройка отдачи статических файлов Angular-клиента напрямую
app.UseDefaultFiles();
app.UseStaticFiles();

// Настройка Swagger в режиме разработки (Development)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ИСПРАВЛЕНО: Закомментировали перенаправление на HTTPS во избежание 
// предупреждения "Failed to determine the https port for redirect" в консоли
// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
