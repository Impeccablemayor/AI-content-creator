# Local Development Configuration

1. Copy the repository-root `.env.example` to `.env` if `.env` does not already exist.
2. For the Groq MVP, set `AI__Provider=Groq`, `AI__Model=openai/gpt-oss-20b`, and `AI__BaseUrl=https://api.groq.com/openai/v1/`.
3. Set `AI__ApiKey` to your local Groq API key. Keep the key only in `.env` and do not commit that file.
4. Set `AI__TimeoutSeconds=45` or another value between 1 and 120 seconds.
5. Start the API normally with `dotnet run --project apps/api/api.csproj`.

Groq, OpenRouter, and OpenAI use the same OpenAI-compatible provider implementation. To switch to OpenRouter, change `AI__Provider=OpenRouter`, set `AI__Model` to a model ID supported by OpenRouter structured outputs, set `AI__BaseUrl=https://openrouter.ai/api/v1/`, and replace `AI__ApiKey` with your OpenRouter key. No application/business code changes are needed.

OpenAI remains supported with `AI__Provider=OpenAI`, an OpenAI model, and an OpenAI API key. Gemini remains supported with `AI__Provider=Gemini`, a Gemini model, and a Gemini API key; `AI__BaseUrl` is used by the OpenAI-compatible providers.

The API loads the repository-root `.env` only in the Development environment.
In Development, values in `.env` override inherited process environment
variables with the same names. A configured model and key do not select a
provider; set `AI__Provider=Groq` in `.env`. Production deployments should
provide all AI settings through their normal environment or secret-management
system instead of using `.env`.