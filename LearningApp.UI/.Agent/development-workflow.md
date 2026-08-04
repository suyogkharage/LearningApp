# Development Workflow

1. Run `Database/001_create_schema.sql`.
2. Run `Database/002_seed_data.sql`.
3. Start Seq on `http://localhost:5341` if structured log collection is desired.
4. Run `dotnet restore` for each solution.
5. Build `LearningApp.Api.sln`.
6. Build `LearningApp.UI/LearningApp.UI.sln`.
7. Start the API, then start the UI Server.
8. Login with `admin@learning.local` and `Password1!`.
