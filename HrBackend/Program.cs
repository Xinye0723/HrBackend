using HrBackend.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace HrBackend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            //註冊SQL連線服務
            builder.Services.AddDbContext<HRContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
            //加入 CORS 服務策略
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAngularApp",
                    builder =>
                    {
                        builder.WithOrigins("http://localhost:4200")
                               .AllowAnyMethod()
                               .AllowAnyHeader()
                               .AllowCredentials(); // 允許攜帶 Cookie
                    });
            });
            //設定 JWT 驗證 (包含從 Cookie 讀取邏輯)
            builder.Services.AddAuthentication(option =>
            {
                option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                    builder.Configuration.GetSection("JwtSettings:Key").Value!)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
                // 【關鍵修改】使用正規事件來讀取 Cookie
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // 嘗試從 Cookie 中讀取名為 "token" 的值
                        var token = context.Request.Cookies["token"];

                        // 如果 Cookie 有值，就指定給 context.Token
                        // 這樣 JwtBearer 就會用這個 Token 進行驗證，而不用去 Header 找
                        if (!string.IsNullOrEmpty(token))
                        {
                            context.Token = token;
                        }

                        return Task.CompletedTask;
                    }
                };
            });
            //註冊controller服務
            builder.Services.AddControllers();
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseCors("AllowAngularApp");
            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
