import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css'],
  standalone: false
})
export class AppComponent implements OnInit {
  currentRole: string = 'Пассажир';

  dbRoutes: any[] = [];
  dbTableData: any[] = [];
  dbTablesList: string[] = []; // НОВОЕ: список всех таблиц из БД
  currentTableName: string = 'passengers'; // Таблица по умолчанию

  currentPassengerId: number = 0;
  generatedTfaCode: string = '';

  constructor(private http: HttpClient) { }

  ngOnInit() {
    // 1. Грузим рейсы
    this.http.get<any[]>('/api/db/routes').subscribe({
      next: (data) => { this.dbRoutes = data; },
      error: (err) => { console.error('Ошибка рейсов. Убедись, что C# запущен!', err); }
    });

    // 2. Грузим все названия таблиц для админа
    this.http.get<string[]>('/api/db/tables-list').subscribe({
      next: (data) => {
        this.dbTablesList = data;
        if (data.length > 0) this.currentTableName = data[0];
      },
      error: (err) => { console.error('Ошибка таблиц:', err); }
    });
  }

  get isAdmin(): boolean { return this.currentRole === 'Администратор'; }

  toggleRole() {
    this.http.get<any>('/api/db/toggle-role').subscribe({
      next: (res) => {
        this.currentRole = res.currentRole === 'Admin' ? 'Администратор' : 'Пассажир';
        if (this.isAdmin && this.dbTablesList.length > 0) {
          this.loadSelectedTable(this.currentTableName);
        }
      }
    });
  }

  registerPassenger(routeId: string, firstName: string, lastName: string, birth: string) {
    if (!routeId) {
      alert("Ошибка: Рейс не выбран! Убедитесь, что рейсы загрузились из БД.");
      return;
    }
    if (!firstName || !lastName || !birth) {
      alert("Пожалуйста, заполните Имя, Фамилию и Дату рождения!");
      return;
    }

    const payload = { routeId, firstName, lastName, birthday: birth };

    this.http.post<any>('/api/db/register', payload).subscribe({
      next: (response) => {
        this.currentPassengerId = response.passengerId;
        this.generatedTfaCode = response.code;
        alert(`${response.message}\nВам отправлен код 2FA: ${response.code}`);
      },
      error: (err) => { alert("Ошибка при записи в БД: " + (err.error?.message || err.message)); }
    });
  }

  confirm2FA(inputCode: string) {
    if (this.currentPassengerId === 0) { alert("Сначала зарегистрируйтесь!"); return; }

    this.http.post<any>('/api/db/confirm-2fa', { passengerId: this.currentPassengerId, code: inputCode }).subscribe({
      next: (res) => { alert(res.message); },
      error: (err) => { alert("Ошибка: " + (err.error?.message || err.message)); }
    });
  }

  // === АДМИНКА: ЗАГРУЗКА И СОХРАНЕНИЕ ===
  loadSelectedTable(tableName: string) {
    this.currentTableName = tableName;
    this.http.get<any[]>(`/api/db/table/${tableName}`).subscribe({
      next: (data) => { this.dbTableData = data; },
      error: (err) => { alert("Ошибка загрузки таблицы: " + (err.error?.message || err.message)); }
    });
  }

  // Обновляет значение ячейки в памяти Angular при вводе
  updateCellValue(row: any, col: string, event: any) {
    row[col] = event.target.value;
  }

  // Отправляет измененную таблицу в C#
  saveGridChanges() {
    if (this.dbTableData.length === 0) return;

    this.http.post(`/api/db/save-table/${this.currentTableName}`, this.dbTableData).subscribe({
      next: () => { alert('Успех: Изменения сохранены в MySQL!'); },
      error: (err) => { alert('Ошибка сохранения: ' + (err.error?.message || err.message)); }
    });
  }

  getObjectKeys(obj: any): string[] { return obj ? Object.keys(obj) : []; }
}
