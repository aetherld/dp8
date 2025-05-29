using Microsoft.AspNetCore.Authentication.Cookies;
using NATS.Client;
using StackExchange.Redis;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var redisConfig = builder.Configuration.GetSection("Redis");
        var natsConfig = builder.Configuration.GetSection("Nats");

        // Add services to the container.
        builder.Services.AddRazorPages();

        var redis = ConnectionMultiplexer.Connect($"{redisConfig["Host"]},password={redisConfig["Password"]}");
        builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

        // Изменяем подключение к NATS с аутентификацией
        var natsOptions = ConnectionFactory.GetDefaultOptions();
        natsOptions.Url = natsConfig["Url"];
        natsOptions.User = natsConfig["User"];
        natsOptions.Password = natsConfig["Password"];
        builder.Services.AddSingleton<IConnection>(new ConnectionFactory().CreateConnection(natsOptions));

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Login";
                options.AccessDeniedPath = "/AccessDenied";
            });

       

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseStaticFiles();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapRazorPages();

        app.Run();
    }
}