namespace Amazon.Lambda.ApplicationLoadBalancerIdentity.Tests
{
    using System;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Mvc.Testing;

    public class CustomWebAppFactory<TStartup> : WebApplicationFactory<TStartup> where TStartup: class
    {
        protected override IWebHostBuilder CreateWebHostBuilder()
        {
            Environment.SetEnvironmentVariable("AWS_REGION", "us-west-2");
            return new WebHostBuilder()
                .UseEnvironment("Development")
                .UseStartup<TStartup>();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseContentRoot(".");
            base.ConfigureWebHost(builder);
        }
    }
}
