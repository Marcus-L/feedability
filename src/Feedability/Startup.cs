using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using AspNetCoreRateLimit;

namespace Feedability
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
			// set up IP Rate limiting
			services.AddOptions(); // needed to load configuration from appsettings.json
			services.AddMemoryCache(); // needed to store rate limit counters and ip rules
			services.Configure<IpRateLimitOptions>(Configuration.GetSection("IpRateLimiting")); //load general configuration from appsettings.json
			services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>(); // inject counter 
			services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>(); // inject rule store

			// set up service options
			services.Configure<Controllers.FullFeedOptions>(Configuration.GetSection("FullFeed"));

			// Add framework services.
			services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

			// enable rate limiter
			app.UseIpRateLimiting();

			// shoe errors 
			app.UseDeveloperExceptionPage();

			// set up controller
            app.UseMvc(routes =>
			{
				routes.MapRoute(
					name: "default",
					template: "{controller=Home}/{action=Index}/{id?}");
			});
			app.UseStaticFiles();

			// configure feedability database 
			SqliteUtil.Init();
        }
	}
}
