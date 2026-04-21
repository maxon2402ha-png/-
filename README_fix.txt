
Проект «КР Ханников» — исправленная сборка

Что сделано:
1) Очистка архива: удалены .vs/, bin/, obj/ и другие временные каталоги.
2) Проверены конвертеры: RoleToVisibilityConverter, StatusToColorConverter, NullToVisibilityConverter — находятся в папке Converters, namespace: КР_Ханников.Converters.
3) App.xaml использует xmlns:converters без указания assembly — это корректно для данного проекта.
4) Контекст БД: AppDbContext настраивается на SQLite файл в %APPDATA%\КР_Ханников\tickets.db.
   В App.OnStartup вызывается Database.Migrate() и заполнение начальными данными.
5) Наличие миграций: папка Migrations содержит актуальные миграции EF Core.
6) MainWindow, LoginWindow и другие окна приведены к согласованной инициализации через AuthService и единый контекст БД.

Как запустить:
- Откройте решение/проект «КР Ханников.csproj» в Visual Studio 2022+.
- Убедитесь, что установлен .NET 8 SDK.
- Первый запуск автоматически применит EF Core миграции и создаст/обновит БД в %APPDATA%\КР_Ханников\tickets.db.

Как пересоздать БД (опционально):
- Удалите файл %APPDATA%\КР_Ханников\tickets.db
- Запустите приложение — миграции применятся заново.

CLI (по желанию):
- dotnet tool install --global dotnet-ef
- dotnet ef database update --project "КР Ханников/КР Ханников.csproj"

Лог входа (debug):
- В режиме DEBUG доступен вход admin/admin123, если пользователь admin уже существует (создаётся сидом).
