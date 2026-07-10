using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Rikud.DLL.AppDbContext;
using System.Security.Claims;

namespace Rikud.WebUI.Middlewares
{
    public class SessionGuardMiddleware
    {
        private readonly RequestDelegate _next;
        public SessionGuardMiddleware(RequestDelegate next) => _next = next;

        public async Task Invoke(HttpContext ctx, RikudDbContext db)
        {
            if (ctx.User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var sessionKeyClaim = ctx.User.FindFirst("SessionKey")?.Value;

                if (int.TryParse(userIdClaim, out var userId) && Guid.TryParse(sessionKeyClaim, out var sessionKey))
                {
                    // 1) Oturum limitini BURADA uygula (login'de değil).
                    //    İzinli oturum sayısından fazlası varsa EN ESKİleri sil.
                    //    Örn. limit=3 ve o an 5 oturum varsa, en eski 2 tanesi silinir.
                    int keepCount = await db.Users
                        .Where(x => x.Id == userId)
                        .Select(x => x.SessionCount)
                        .FirstOrDefaultAsync();
                    if (keepCount < 1) keepCount = 1;

                    // keepCount'uncu EN YENİ oturumun Id'si = eşik; bundan eski olan her şeyi tek DELETE ile sil.
                    // ExecuteDelete set-based çalışır: aynı anda poll eden tarayıcılar arasında çakışma/yarış olmaz.
                    var cutoffId = await db.UserSessions
                        .Where(x => x.UserId == userId)
                        .OrderByDescending(x => x.Id)
                        .Select(x => x.Id)
                        .Skip(keepCount - 1)
                        .Take(1)
                        .FirstOrDefaultAsync();

                    if (cutoffId > 0)
                    {
                        await db.UserSessions
                            .Where(x => x.UserId == userId && x.Id < cutoffId)
                            .ExecuteDeleteAsync();
                    }

                    // 2) Kendi oturumum hâlâ duruyor mu? (yukarıda limit nedeniyle silinmiş olabilir)
                    bool stillExists = await db.UserSessions
                        .AnyAsync(x => x.UserId == userId && x.SessionKey == sessionKey);

                    if (!stillExists)
                    {
                        await ctx.SignOutAsync("RikudUI");
                        ctx.Response.Redirect("/User/Login?reason=expired");
                        ctx.Session.SetString("Logoff", "1");
                        ctx.Session.SetString("UserId", userId.ToString());
                        return;
                    }
                }
            }

            await _next(ctx);
        }
    }

}
