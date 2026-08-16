namespace Nexus.Components;

public static class ThemeCookie
{
    public const string Name = "nexus-theme";
    public const string Light = "light";
    public const string Dark = "dark";

    public static string CssClassFrom(HttpContext? httpContext)
    {
        var value = httpContext?.Request.Cookies[Name];
        return value is Light or Dark ? value : string.Empty;
    }
}
