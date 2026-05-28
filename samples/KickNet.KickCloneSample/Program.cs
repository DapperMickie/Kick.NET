using System.Security.Claims;
using Kick;
using KickNet.KickCloneSample.Components;
using KickNet.KickCloneSample.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<KickCloneOptions>(builder.Configuration.GetSection("Kick"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.LogoutPath = "/logout";
        options.AccessDeniedPath = "/";
        options.Cookie.Name = "KickCloneSample.Auth";
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddSingleton<KickBrowseService>();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet("/login", (HttpContext httpContext, IOptions<KickCloneOptions> options) =>
{
    var config = options.Value;
    if (!config.IsConfigured)
    {
        return Results.Redirect("/?setup=1");
    }

    var auth = CreateUserAuth(config, httpContext);
    var login = auth.CreateLoginRequest();
    var cookieOptions = new CookieOptions
    {
        HttpOnly = true,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Secure = httpContext.Request.IsHttps,
        MaxAge = TimeSpan.FromMinutes(10),
    };

    httpContext.Response.Cookies.Append("KickCloneSample.Pkce", login.CodeVerifier, cookieOptions);
    httpContext.Response.Cookies.Append("KickCloneSample.State", login.State, cookieOptions);
    return Results.Redirect(login.AuthorizationUri.ToString());
}).AllowAnonymous();

app.MapGet("/oauth/callback", async (HttpContext httpContext, IOptions<KickCloneOptions> options, CancellationToken cancellationToken) =>
{
    var code = httpContext.Request.Query["code"].ToString();
    var state = httpContext.Request.Query["state"].ToString();
    var savedState = httpContext.Request.Cookies["KickCloneSample.State"];
    var codeVerifier = httpContext.Request.Cookies["KickCloneSample.Pkce"];
    if (string.IsNullOrWhiteSpace(code) ||
        string.IsNullOrWhiteSpace(codeVerifier) ||
        !string.Equals(state, savedState, StringComparison.Ordinal))
    {
        return Results.Redirect("/?auth=failed");
    }

    var session = await CreateUserAuth(options.Value, httpContext)
        .ExchangeAuthorizationCodeAsync(code, codeVerifier, cancellationToken);
    var currentUser = (await session.Client.Users.GetAsync(cancellationToken: cancellationToken))?.Data?.FirstOrDefault();
    var claims = new List<Claim>
    {
        new(ClaimTypes.Name, currentUser?.Name ?? "Kick user"),
        new("kick:access_token", session.TokenInfo.AccessToken),
    };

    if (!string.IsNullOrWhiteSpace(session.TokenInfo.RefreshToken))
    {
        claims.Add(new Claim("kick:refresh_token", session.TokenInfo.RefreshToken));
    }

    if (!string.IsNullOrWhiteSpace(session.TokenInfo.Scope))
    {
        claims.Add(new Claim("kick:scope", session.TokenInfo.Scope));
    }

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

    httpContext.Response.Cookies.Delete("KickCloneSample.Pkce");
    httpContext.Response.Cookies.Delete("KickCloneSample.State");
    return Results.Redirect("/");
}).AllowAnonymous();

app.MapPost("/chat/send", async (HttpContext httpContext, CancellationToken cancellationToken) =>
{
    var accessToken = httpContext.User.FindFirstValue("kick:access_token");
    if (string.IsNullOrWhiteSpace(accessToken))
    {
        return Results.Redirect("/login");
    }

    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var message = form["message"].ToString();
    var slug = form["slug"].ToString();
    if (!int.TryParse(form["broadcasterUserId"].ToString(), out var broadcasterUserId) ||
        string.IsNullOrWhiteSpace(message))
    {
        return Results.Redirect("/?chat=invalid");
    }

    var kick = new KickClient(accessTokenProvider: new StaticAccessTokenProvider(accessToken));
    await kick.Chat.PostMessageAsync(new PostChatMessageRequestBuilder()
        .ForBroadcaster(broadcasterUserId)
        .WithContent(message)
        .AsUser()
        .Build(), cancellationToken);

    return Results.Redirect($"/?slug={Uri.EscapeDataString(slug)}&chat=sent");
}).RequireAuthorization();

app.MapPost("/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

static KickUserAuthClient CreateUserAuth(KickCloneOptions options, HttpContext httpContext) =>
    KickSdk.CreateUserAuthClient(new KickUserAuthOptions
    {
        ClientId = options.ClientId,
        ClientSecret = options.ClientSecret,
        RedirectUri = BuildRedirectUri(httpContext, options),
        DefaultScopes = [KickScopes.UserRead, KickScopes.ChannelRead, KickScopes.ChatWrite, KickScopes.StreamKeyRead],
    });

static string BuildRedirectUri(HttpContext httpContext, KickCloneOptions options)
{
    if (!string.IsNullOrWhiteSpace(options.RedirectUri))
    {
        return options.RedirectUri;
    }

    var request = httpContext.Request;
    return UriHelper.BuildAbsolute(request.Scheme, request.Host, path: "/oauth/callback");
}
