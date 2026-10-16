# Local Development Configuration

1. Copy the repository-root `.env.example` to `.env` if `.env` does not already exist.
2. For the Groq MVP, set `AI__Provider=Groq`, `AI__Model=openai/gpt-oss-20b`, and `AI__BaseUrl=https://api.groq.com/openai/v1/`.
3. Set `AI__ApiKey` to your local Groq API key. Keep the key only in `.env` and do not commit that file.
4. Set `AI__TimeoutSeconds=45` or another value between 1 and 120 seconds.
5. Start the API normally with `dotnet run --project apps/api/api.csproj`, or start the full local stack from the repository root with `npm run dev` (see below).

## Run the full local stack

From the repository root, run:

```powershell
npm run dev
```

This starts the web app, API, and scheduled-post/analytics worker together in
one terminal. Press **Ctrl+C** to stop all three. The web app is available at
`http://localhost:3000` and the API at `http://localhost:5025`. Make sure the
configured PostgreSQL and Redis services are running first; this command does
not start those infrastructure services. The web dependencies must already be
installed in `apps/web` (`npm install` there if `node_modules` is missing).

## Configure LinkedIn OAuth locally

LinkedIn connection is optional. Without provider credentials, the API correctly
returns `SOCIAL_PLATFORM_NOT_CONFIGURED` when a user starts the OAuth flow.

1. In the [LinkedIn Developer Portal](https://www.linkedin.com/developers/), create or select your app and note its Client ID and Client Secret. Enable the LinkedIn OpenID Connect sign-in product for account identity. If you also want to publish posts, request the LinkedIn sharing permission (`w_member_social`) and wait for any required product approval.
2. Copy the commented `Social__LinkedIn__...` example keys from the repository-root `.env.example` into your ignored repository-root `.env` file. Fill in your own Client ID and Client Secret; never put the secret in source control or send it in chat.
3. Set the callback URL to the API callback, not the web frontend. With the default HTTP launch profile, use `http://localhost:5025/api/v1/social/linkedin/callback`. Register that exact URL in the LinkedIn app's OAuth 2.0 settings and set the identical value in `Social__LinkedIn__RedirectUri`. If the LinkedIn portal requires HTTPS, run the API on its HTTPS launch profile or use a trusted HTTPS tunnel, then register and configure the exact same HTTPS callback URL.
4. Set scopes as indexed environment-style keys, for example `Social__LinkedIn__Scopes__0=openid`, `Social__LinkedIn__Scopes__1=profile`, and `Social__LinkedIn__Scopes__2=email`. Use only scopes enabled for your LinkedIn app. Add `Social__LinkedIn__Scopes__3=w_member_social` only if the app has the sharing product/permission. The app requests the configured scopes during consent.
5. Stop and restart the API so it loads the new Development `.env` values. Confirm it starts without configuration validation errors.
6. Sign in to Mayorx, open **Connections**, and select **Connect LinkedIn**. Review and accept the LinkedIn consent screen. The provider returns the browser to the API callback; after successful consent, return to Connections and reload the account list to see the connected account.

The API does not store LinkedIn credentials in the web app. The Client Secret is read by the API from local Development configuration; OAuth access/refresh tokens are stored server-side encrypted after a successful callback. A connect attempt may still be rejected by LinkedIn if the redirect URI is not an exact match, the requested product/scope is not enabled, or the app is not approved for that permission.

Groq, OpenRouter, and OpenAI use the same OpenAI-compatible provider implementation. To switch to OpenRouter, change `AI__Provider=OpenRouter`, set `AI__Model` to a model ID supported by OpenRouter structured outputs, set `AI__BaseUrl=https://openrouter.ai/api/v1/`, and replace `AI__ApiKey` with your OpenRouter key. No application/business code changes are needed.

OpenAI remains supported with `AI__Provider=OpenAI`, an OpenAI model, and an OpenAI API key. Gemini remains supported with `AI__Provider=Gemini`, a Gemini model, and a Gemini API key; `AI__BaseUrl` is used by the OpenAI-compatible providers.

The API loads the repository-root `.env` only in the Development environment.
In Development, values in `.env` override inherited process environment
variables with the same names. A configured model and key do not select a
provider; set `AI__Provider=Groq` in `.env`. Production deployments should
provide all AI settings through their normal environment or secret-management
system instead of using `.env`.