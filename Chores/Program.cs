using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Chores.Data;
using Chores.Models;
using Chores.Services;
using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// SQLite — stored in /data when running in Docker, or local app data in development
var dataDir = builder.Configuration["DataDirectory"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "chores.db");

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<ScheduleAdherenceService>();
builder.Services.AddScoped<HouseholdInvitationService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromMinutes(5);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
});
builder.Services.AddFido2(opts =>
{
    opts.ServerDomain = builder.Configuration["Fido2:ServerDomain"] ?? "localhost";
    opts.ServerName = "Chores";
    opts.Origins = (builder.Configuration["Fido2:Origins"] ?? "https://localhost:5001")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet();
    opts.TimestampDriftTolerance = 300_000;
});

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddAuthentication("Cookies")
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Auth/Login";
        opt.LogoutPath = "/Auth/Logout";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Auto-apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapStaticAssets();

// --- FIDO2 attestation (registration) endpoints ---

app.MapPost("/api/auth/attestation/options", async (HttpContext httpContext) =>
{
    var body = await httpContext.Request.ReadFromJsonAsync<UsernameRequest>();
    if (!LoginNameValidator.TryNormalize(body?.Username, out var username))
        return Results.BadRequest("Invalid login name.");

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var fido2 = httpContext.RequestServices.GetRequiredService<IFido2>();

    var existingUser = await db.Users
        .FirstOrDefaultAsync(u => u.LoginName == username);

    if (existingUser is not null)
        return Results.Conflict("That login name is unavailable.");

    var excludeCredentials = new List<PublicKeyCredentialDescriptor>();

    var fidoUser = new Fido2User
    {
        Name = username,
        Id = Encoding.UTF8.GetBytes(username),
        DisplayName = username
    };

    var options = fido2.RequestNewCredential(new RequestNewCredentialParams
    {
        User = fidoUser,
        ExcludeCredentials = excludeCredentials,
        AuthenticatorSelection = AuthenticatorSelection.Default,
        AttestationPreference = AttestationConveyancePreference.None
    });

    httpContext.Session.SetString("fido2.attestation.options", options.ToJson());

    return Results.Content(options.ToJson(), "application/json");
});

app.MapPost("/api/auth/attestation/result", async (HttpContext httpContext) =>
{
    var attestationResponse = await httpContext.Request
        .ReadFromJsonAsync<AuthenticatorAttestationRawResponse>();
    if (attestationResponse is null)
        return Results.BadRequest("Invalid attestation response.");

    var storedJson = httpContext.Session.GetString("fido2.attestation.options");
    if (storedJson is null)
        return Results.BadRequest("No attestation options in session.");

    var storedOptions = CredentialCreateOptions.FromJson(storedJson);
    var username = storedOptions.User.Name;
    if (!LoginNameValidator.TryNormalize(username, out username))
        return Results.BadRequest("Invalid login name.");

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var fido2 = httpContext.RequestServices.GetRequiredService<IFido2>();
    var householdInvitations = httpContext.RequestServices.GetRequiredService<HouseholdInvitationService>();

    IsCredentialIdUniqueToUserAsyncDelegate isUnique = async (args, ct) =>
    {
        return !await db.FidoCredentials.AnyAsync(c => c.CredentialId.SequenceEqual(args.CredentialId), ct);
    };

    var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
    {
        AttestationResponse = attestationResponse,
        OriginalOptions = storedOptions,
        IsCredentialIdUniqueToUserCallback = isUnique
    });

    var existingUser = await db.Users
        .Include(u => u.Credentials)
        .FirstOrDefaultAsync(u => u.LoginName == username);
    if (existingUser is not null)
        return Results.Conflict("That login name is unavailable.");

    var pendingInvite = await householdInvitations.GetLatestPendingInviteAsync(username);
    AppUser user;
    if (pendingInvite is null)
    {
        var household = new Household { Name = $"{username}'s household" };
        db.Households.Add(household);
        user = new AppUser
        {
            LoginName = username,
            Household = household,
            IsHouseholdOwner = true
        };
    }
    else
    {
        user = new AppUser
        {
            LoginName = username,
            HouseholdId = pendingInvite.HouseholdId,
            IsHouseholdOwner = false
        };
        pendingInvite.AcceptedAtUtc = DateTime.UtcNow;
    }

    db.Users.Add(user);

    user.Credentials.Add(new FidoCredential
    {
        CredentialId = result.Id,
        PublicKey = result.PublicKey,
        UserHandle = storedOptions.User.Id,
        SignCount = result.SignCount,
        CredType = result.Type.ToString(),
        RegDate = DateTime.UtcNow,
        AaGuid = result.AaGuid
    });

    await db.SaveChangesAsync();

    var claims = new[] { new Claim(ClaimTypes.Name, username) };
    var identity = new ClaimsIdentity(claims, "Cookies");
    await httpContext.SignInAsync("Cookies", new ClaimsPrincipal(identity));

    return Results.Ok(new { status = "ok" });
});

// --- FIDO2 assertion (login) endpoints ---

app.MapPost("/api/auth/assertion/options", async (HttpContext httpContext) =>
{
    var body = await httpContext.Request.ReadFromJsonAsync<UsernameRequest>();
    if (!LoginNameValidator.TryNormalize(body?.Username, out var username))
        return Results.BadRequest("Invalid login name.");

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var fido2 = httpContext.RequestServices.GetRequiredService<IFido2>();

    var user = await db.Users
        .Include(u => u.Credentials)
        .FirstOrDefaultAsync(u => u.LoginName == username);

    var allowedCredentials = user?.Credentials
        .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
        .ToList() ?? [];

    var options = fido2.GetAssertionOptions(new GetAssertionOptionsParams
    {
        AllowedCredentials = allowedCredentials,
        UserVerification = UserVerificationRequirement.Preferred
    });

    httpContext.Session.SetString("fido2.assertion.options", options.ToJson());
    httpContext.Session.SetString("fido2.assertion.username", username);

    return Results.Content(options.ToJson(), "application/json");
});

app.MapPost("/api/auth/assertion/result", async (HttpContext httpContext) =>
{
    var assertionResponse = await httpContext.Request
        .ReadFromJsonAsync<AuthenticatorAssertionRawResponse>();
    if (assertionResponse is null)
        return Results.BadRequest("Invalid assertion response.");

    var storedJson = httpContext.Session.GetString("fido2.assertion.options");
    if (storedJson is null)
        return Results.BadRequest("No assertion options in session.");

    var storedOptions = AssertionOptions.FromJson(storedJson);
    var username = httpContext.Session.GetString("fido2.assertion.username") ?? string.Empty;

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var fido2 = httpContext.RequestServices.GetRequiredService<IFido2>();

    var rawId = assertionResponse.RawId;
    var storedCred = await db.FidoCredentials
        .Include(c => c.User)
        .FirstOrDefaultAsync(c => c.CredentialId.SequenceEqual(rawId));
    if (storedCred is null)
        return Results.Unauthorized();

    IsUserHandleOwnerOfCredentialIdAsync isOwner = async (args, ct) =>
    {
        return await db.FidoCredentials.AnyAsync(c => c.CredentialId.SequenceEqual(args.CredentialId)
            && c.UserHandle.SequenceEqual(args.UserHandle), ct);
    };

    var result = await fido2.MakeAssertionAsync(new MakeAssertionParams
    {
        AssertionResponse = assertionResponse,
        OriginalOptions = storedOptions,
        StoredPublicKey = storedCred.PublicKey,
        StoredSignatureCounter = storedCred.SignCount,
        IsUserHandleOwnerOfCredentialIdCallback = isOwner
    });

    storedCred.SignCount = result.SignCount;
    await db.SaveChangesAsync();

    var loginName = storedCred.User.LoginName;
    var claims = new[] { new Claim(ClaimTypes.Name, loginName) };
    var identity = new ClaimsIdentity(claims, "Cookies");
    await httpContext.SignInAsync("Cookies", new ClaimsPrincipal(identity));

    return Results.Ok(new { status = "ok" });
});

// --- Profile passkey management endpoints (authenticated users only) ---

app.MapPost("/api/profile/passkey/options", async (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var username = httpContext.User.Identity!.Name!;
    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var fido2 = httpContext.RequestServices.GetRequiredService<IFido2>();

    var user = await db.Users
        .Include(u => u.Credentials)
        .FirstOrDefaultAsync(u => u.LoginName == username);

    var excludeCredentials = user?.Credentials
        .Select(c => new PublicKeyCredentialDescriptor(c.CredentialId))
        .ToList() ?? [];

    var fidoUser = new Fido2User
    {
        Name = username,
        Id = Encoding.UTF8.GetBytes(username),
        DisplayName = username
    };

    var options = fido2.RequestNewCredential(new RequestNewCredentialParams
    {
        User = fidoUser,
        ExcludeCredentials = excludeCredentials,
        AuthenticatorSelection = AuthenticatorSelection.Default,
        AttestationPreference = AttestationConveyancePreference.None
    });

    httpContext.Session.SetString("fido2.profile.attestation.options", options.ToJson());

    return Results.Content(options.ToJson(), "application/json");
});

app.MapPost("/api/profile/passkey/result", async (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
        return Results.Unauthorized();

    var attestationResponse = await httpContext.Request
        .ReadFromJsonAsync<AuthenticatorAttestationRawResponse>();
    if (attestationResponse is null)
        return Results.BadRequest("Invalid attestation response.");

    var storedJson = httpContext.Session.GetString("fido2.profile.attestation.options");
    if (storedJson is null)
        return Results.BadRequest("No attestation options in session.");

    var storedOptions = CredentialCreateOptions.FromJson(storedJson);
    var username = httpContext.User.Identity!.Name!;

    var db = httpContext.RequestServices.GetRequiredService<AppDbContext>();
    var fido2 = httpContext.RequestServices.GetRequiredService<IFido2>();

    IsCredentialIdUniqueToUserAsyncDelegate isUnique = async (args, ct) =>
    {
        return !await db.FidoCredentials.AnyAsync(c => c.CredentialId.SequenceEqual(args.CredentialId), ct);
    };

    var result = await fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
    {
        AttestationResponse = attestationResponse,
        OriginalOptions = storedOptions,
        IsCredentialIdUniqueToUserCallback = isUnique
    });

    var user = await db.Users
        .Include(u => u.Credentials)
        .FirstOrDefaultAsync(u => u.LoginName == username);

    if (user is null)
        return Results.NotFound("User not found.");

    user.Credentials.Add(new FidoCredential
    {
        CredentialId = result.Id,
        PublicKey = result.PublicKey,
        UserHandle = storedOptions.User.Id,
        SignCount = result.SignCount,
        CredType = result.Type.ToString(),
        RegDate = DateTime.UtcNow,
        AaGuid = result.AaGuid
    });

    await db.SaveChangesAsync();

    return Results.Ok(new { status = "ok" });
});

app.MapRazorPages()
   .WithStaticAssets();

app.Run();

record UsernameRequest(string Username);
