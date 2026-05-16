namespace SiteDiary.Web.Middleware;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SkipResourceAuthorizationAttribute : Attribute { }
