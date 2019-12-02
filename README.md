# Amazon.ApplicationLoadBalancer.Identity.AspNetCore (Preview)

This package contains Middleware that can be used in conjunction with the [Application Load Balancer (ALB) OpenId Connect integration](https://aws.amazon.com/blogs/aws/built-in-authentication-in-alb/)

It can be used in any ASP.NET Core deployment scenario, including Fargate, EKS, ECS, EC2, and Lambda.

It will populate the "User" property in the request's HttpContext with a "ClaimsPrincipal" object that contains the username that is returned by the OpenId Connect Identity Provider (OIDC IDP) that the ALB is configured to use. It will also add "group" claims that are returned by the OIDC IDP to the ClaimsPrincipal as "Role" claims (you can change the claim name from "group" to something else in the configuration).

## Referencing the ALB-injected identity in your code
The following examples are all within a controller method, however, the user is populated in the HttpContext object.

Basic example for obtaining the caller's username (within a controller method):
```
[HttpGet("user")]
public string GetUser()
{
	return string.Format("Hello {0}", this.Request.HttpContext.User.Identity.Name);
}
```

When the user has at least one "group" claim returned from the user info endpoint, this feature an also be used for authorization.
The name of the "group" claim defaults to group, but this can be changed in the startup configuration.

Example for attribute-based group membership checking:
```
[HttpDelete("deletesomething/{id}")]
[Authorize("Administrators")]
public string SomePrivilegedOperation(string id)
{
	// Your code here won't be executed unless the user has a "group" name with the value "Administrators"
}
```


Example for dynamic group membership checking:
```
[HttpGet("membership/{groupName}")]
public bool CheckGroupMembership(string groupName)
{
	return this.Request.HttpContext.User.IsInRole(groupName);
}
```

# Building
The library is currently in preview, and does not have a NuGet package.

To use the library now, follow these steps:
- Clone the repo onto you developer machine
- Copy the "Amazon.ApplicationLoadBalancer.Identity.AspNetCore" directory to a subdirectory somewhere in your solution's directory tree
- Open your solution in your preferred IDE
- Right click on your solution (or a subdirectory), expand the "Add" menu item, then click "Existing Project"
- Select the "Amazon.ApplicationLoadBalancer.Identity.AspNetCore.csproj" file in the directory you copied.
- In your ASP.NET Core project, add a reference to the "Amazon.ApplicationLoadBalancer.Identity.AspNetCore" project you just added.
- Check in the code to your own repository.

If you plan on making any changes to the source, you should repeat the process for the "Tests" project.

# Configuring the Middleware
## ALBIdentityMiddlewareOptions class
This is the class used to configure the middleware. It has the following properties:

#### MaxCacheSizeMB
Gets or sets the maximum memory (in MB) that the identity cache will use.

The default value is 10MB.

If you set this property to null, in-memory caching will be disabled.

If you have set the ValidateTokenSignature property, enabling caching will usually reduce the overhead by double digit milliseconds on subsequent requests.

The size of each entry corresponds to the base64 encoded payload length in the JWT.
Since this data is provided by the userinfo endpoint, it could be of any size.
To calculate the *minimum* value for your traffic, identify your average userinfo payload length,
and multiply that by the expected number of concurrent users per [active] token lifetime interval.

For example:
Your average userinfo payload is 1.4KB, and you are expecting 10,000 concurrent users.
If a token is valid for 1 day, then:
1.4KB * 10,000 * 1 = 13.6MB minimum

If your concurrent users are rapidly cycling (e.g. 10,000 *different* users every 15 minutes for 1 hour each)
then you will want to alter the estimate to ensure that your cache is not evicting active entries.
1.4KB * 10,000 * (96 / 24) (15 min intervals in 1 day for 1 hour each) = 54.6MB minimum.

When the cache reaches it's maximum size, it will remove the oldest 20% of values, so it is
recommended you use a value at least 20% larger than the minimum value calculated above.
It does not allocate memory in advance, only when used, so if you have RAM to spare then estimate higher.

#### MaxCacheLifetime
Gets or sets the maximum amount of time an identity will remain in the in-memory cache.

If not specified, the default will be 24 hours.

#### CacheCompactionPercentage
Gets or sets the percentage of entries that will be removed when a cache hits the maximum size.

The default value is 10.

The minimum value you can set is 1, the maximum is 50.
If you specify a value that is out of range, the default value will be used.

#### ValidateTokenSignature
Gets or sets a flag indicating whether token signatures should be validated to ensure that they were issued by the Application Load Balancer.

The default value is true. If you set this to false, you will improve performance but will be sacrificing security.

When you enable token verification, you are validating that the token was issued by the Application Load Balancer.
This involves download the public signing key from the ALB key distribution endpoint (one time, then cached),
and using the signature section of the JWT to ensure that the JWT was signed using the ALB's key.

If you enable this, it is recommended that you enable identity caching by setting the MaxCacheSizeMB property.
If you do not enable caching, this validation will be performed on every request.

#### RoleClaimType
Gets or sets the name of the role claim type, for use in the "IsInRole" method and [Authorize] attributes.

The default value is "group".

#### ValidateTokenLifetime
Gets or sets a flag indicating whether token lifetime should be validated.

The default value is true. This should never be disabled except for testing purposes.

## Example Startup.cs

The middleware requires Logging services be registered to the service collection, as well as an
instance of the ALBIdentityMiddlewareOptions class (containing the configuration information).

The middleware needs to be registered before Mvc, but after health checks and exception handlers.

```csharp
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
		services
			.AddLogging()
			.AddHealthChecks()
			.AddSingleton(new ALBIdentityMiddlewareOptions
			{
				MaxCacheSizeMB = 10,
				MaxCacheLifetime = TimeSpan.FromHours(1),
				CacheCompactionPercentage = 10
			})
			.AddMvc();
    }

	public void Configure(IApplicationBuilder app)
	{
		app
			.UseExceptionHandler()
			.UseHealthChecks("/ping")
			.UseMiddleware<ALBIdentityMiddleware>()
			.UseMvc();
	}
}
```