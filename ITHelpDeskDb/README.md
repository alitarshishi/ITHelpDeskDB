# IT HelpDesk DB

A simple IT helpdesk web application built with ASP.NET Core (Razor Pages) and Entity Framework Core.

This project provides basic ticketing features for submitting, assigning, and tracking IT support tickets. It also includes API controllers for integration and services for email, export, and AI-assisted parsing.

Key features
- Create and manage support tickets
- Assign tickets to IT agents and managers
- Activity logs for ticket history
- Export tickets and activity logs to Excel
- Password reset with token support

Tech stack
- .NET 10 (ASP.NET Core Razor Pages)
- Entity Framework Core
- ClosedXML for Excel exports

Prerequisites
- .NET 10 SDK installed
- Visual Studio 2022/2026 or Visual Studio Code

Quick start
1. Clone the repository:

   git clone https://github.com/alitarshishi/ITHelpDeskDB.git

2. Configure settings:
   - Copy and edit appsettings.json (or use user secrets) to set database connection string and any email/AI settings.

3. Restore and run:

   dotnet restore
   dotnet ef database update   # if EF migrations are used and configured
   dotnet run --project ITHelpDeskDb

4. Open the app in a browser (usually https://localhost:5001 or the URL shown in the console).

APIs
The project includes API controllers under Controllers/Api for programmatic access to tickets, users, notifications, exports, and more. See source files for route details.

Contributing
- Feel free to open issues or pull requests.

License
- See the repository for license details.
