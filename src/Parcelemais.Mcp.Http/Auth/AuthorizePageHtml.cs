using System.Net;

namespace Parcelemais.Mcp.Http.Auth;

internal static class AuthorizePageHtml
{
    public static string Render(string clientId, string redirectUri, string codeChallenge, string? state, string? scope, string? resource, string? error, string selectedEnvironment)
    {
        var errorHtml = error is null ? "" : $"""<p class="error">{Esc(error)}</p>""";
        var stagingChecked = selectedEnvironment == "Staging" ? "checked" : "";
        var productionChecked = selectedEnvironment == "Staging" ? "" : "checked";

        return $$"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>Parcele+ — Conectar</title>
              <style>
                *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
                body {
                  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
                  background: #f5f6fb;
                  display: flex; align-items: center; justify-content: center;
                  min-height: 100vh; padding: 1rem;
                }
                .card { background: #fff; border-radius: 12px; box-shadow: 0 4px 24px rgba(26,21,184,0.08); padding: 2.5rem 2rem; max-width: 440px; width: 100%; }
                .logo { font-size: 1.5rem; font-weight: 700; text-align: center; margin-bottom: 0.5rem; color: #1A15B8; }
                h1 { font-size: 1.25rem; font-weight: 700; text-align: center; color: #1a1a1a; margin-bottom: 0.25rem; }
                .subtitle { font-size: 0.875rem; color: #666; text-align: center; margin-bottom: 1.75rem; }
                label { display: block; font-size: 0.8125rem; font-weight: 600; color: #333; margin-bottom: 0.375rem; margin-top: 1rem; }
                label:first-of-type { margin-top: 0; }
                input[type="text"], input[type="password"] {
                  width: 100%; padding: 0.625rem 0.875rem; border: 1.5px solid #ddd; border-radius: 8px;
                  font-size: 0.9375rem; outline: none; transition: border-color 0.15s;
                }
                input:focus { border-color: #3A36F5; }
                .error { background: #fff0f0; color: #c0392b; border: 1px solid #f5c6c6; border-radius: 6px; padding: 0.625rem 0.875rem; font-size: 0.875rem; margin-bottom: 1rem; }
                button {
                  display: block; width: 100%; margin-top: 1.5rem; padding: 0.75rem;
                  background: #3A36F5; color: #fff; font-size: 1rem; font-weight: 600; border: none; border-radius: 8px;
                  cursor: pointer; transition: background 0.15s;
                }
                button:hover { background: #1A15B8; }
                .env-badge { display: inline-block; font-size: 0.75rem; font-weight: 600; padding: 0.125rem 0.5rem; border-radius: 999px; background: #eef; color: #1A15B8; margin-left: 0.375rem; }
                .help { font-size: 0.8125rem; color: #888; text-align: center; margin-top: 1.25rem; }
              </style>
            </head>
            <body>
              <div class="card">
                <div class="logo">Parcele+</div>
                <h1>Conectar assistente de IA</h1>
                <p class="subtitle">Informe as credenciais do Parcele+ pra autorizar o acesso do seu assistente de IA.</p>
                {{errorHtml}}
                <form method="POST" action="/authorize" autocomplete="off">
                  <input type="hidden" name="client_id" value="{{Esc(clientId)}}" />
                  <input type="hidden" name="redirect_uri" value="{{Esc(redirectUri)}}" />
                  <input type="hidden" name="code_challenge" value="{{Esc(codeChallenge)}}" />
                  <input type="hidden" name="state" value="{{Esc(state ?? "")}}" />
                  <input type="hidden" name="scope" value="{{Esc(scope ?? "")}}" />
                  <input type="hidden" name="resource" value="{{Esc(resource ?? "")}}" />
                  <label for="client_id_input">Client ID</label>
                  <input type="text" id="client_id_input" name="parcelemais_client_id" required autofocus />
                  <label for="client_secret_input">Client Secret</label>
                  <input type="password" id="client_secret_input" name="parcelemais_client_secret" required />
                  <label>Ambiente</label>
                  <div style="display:flex; gap:1rem; margin-top:0.375rem;">
                    <label style="display:flex; align-items:center; gap:0.375rem; font-weight:400;">
                      <input type="radio" name="environment" value="Production" {{productionChecked}} style="width:auto;" /> Produção
                    </label>
                    <label style="display:flex; align-items:center; gap:0.375rem; font-weight:400;">
                      <input type="radio" name="environment" value="Staging" {{stagingChecked}} style="width:auto;" /> Staging
                    </label>
                  </div>
                  <button type="submit">Autorizar</button>
                </form>
                <p class="help">Encontre suas credenciais no painel do Parcele+, em Integrações.</p>
              </div>
            </body>
            </html>
            """;
    }

    public static string Error(string message) => $$"""
        <!DOCTYPE html>
        <html lang="pt-BR">
        <head>
          <meta charset="UTF-8" />
          <title>Erro — Parcele+</title>
          <style>
            body { font-family: sans-serif; display:flex; align-items:center; justify-content:center; min-height:100vh; background:#f5f6fb; }
            .box { background:#fff; border-radius:12px; padding:2rem; max-width:400px; text-align:center; box-shadow:0 4px 24px rgba(26,21,184,0.08); }
            h1 { color:#c0392b; margin-bottom:0.75rem; }
            p { color:#555; }
          </style>
        </head>
        <body>
          <div class="box">
            <h1>Erro de autorização</h1>
            <p>{{Esc(message)}}</p>
          </div>
        </body>
        </html>
        """;

    private static string Esc(string s) => WebUtility.HtmlEncode(s);
}
