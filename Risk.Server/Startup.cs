using MatBlazor;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Risk.Server.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Risk.Server
{
    public class Startup
    {

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor()
                .AddCircuitOptions(o =>
                {
                    o.DetailedErrors = true;
                });
            try
            {
                services.AddSignalR();
                services.AddCors(options =>
                {
                    options.AddDefaultPolicy(
                        builder =>
                        {
                            builder.AllowAnyOrigin()
                                   .AllowAnyMethod()
                                   .AllowAnyHeader();
                        });
                });

                services.AddSingleton<RiskHub>();
                services.AddSingleton<RiskBridge>();
                services.AddSingleton(services => GameInitializer.InitializeGame(
                    int.Parse(Configuration["height"] ?? "5"),
                    int.Parse(Configuration["width"] ?? "5"),
                    int.Parse(Configuration["startingArmies"] ?? "5"),
                    Configuration["StartGameCode"],
                    services.GetService<RiskBridge>()
                    ));
            } catch
            {
                throw;
            }
            services.AddMatBlazor();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            //Path base is needed for running behind a reverse proxy, otherwise the app will not be able to find the static files
            var pathBase = Configuration["PATH_BASE"];
            app.UsePathBase(pathBase);
    
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(builder =>
            {
                builder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
            });

            app.UseStaticFiles();
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapRazorPages();
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
                endpoints.MapHub<RiskHub>("riskhub");
            });
        }
    }
}
