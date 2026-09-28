# Local Development Configuration

1. Copy the repository-root `.env.example` to `.env`.
2. Add your local OpenAI API key to `AI__ApiKey` in `.env`.
3. Do not commit `.env`; the repository-root `.gitignore` excludes it.
4. Start the API normally with `dotnet run --project apps/api/api.csproj`.

The API loads the repository-root `.env` only in the Development environment.
Existing process or container environment variables take precedence over values
from `.env`. Production deployments should provide secrets through their normal
environment or secret-management system instead of using `.env`.