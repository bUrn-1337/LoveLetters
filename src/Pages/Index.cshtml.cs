using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RazorLight;

namespace CupidsCode.Pages;

public class IndexModel : PageModel
{
    private readonly IRazorLightEngine _razorEngine;
    private readonly FlagService _flagService;
    private readonly ILogger<IndexModel> _logger;
    
    private const string UserTokenKey = "UserToken";

    [BindProperty]
    public string? Template { get; set; }

    [BindProperty]
    public string? RecipientName { get; set; }

    public string? RenderedLetter { get; set; }
    public string? ErrorMessage { get; set; }
    public string? UserToken { get; set; }

    public IndexModel(IRazorLightEngine razorEngine, FlagService flagService, ILogger<IndexModel> logger)
    {
        _razorEngine = razorEngine;
        _flagService = flagService;
        _logger = logger;
    }

    public void OnGet()
    {
        EnsureUserToken();
        
        Template = "My Dear,\n\nRoses are red,\nViolets are blue,\nThis Valentine's Day,\nI'm thinking of you! 💕\n\nWith all my love,\nYour Secret Admirer 💘";
        RecipientName = "My Beloved";
    }

    public async Task<IActionResult> OnPostAsync()
    {
        EnsureUserToken();
        
        if (string.IsNullOrWhiteSpace(Template))
        {
            ErrorMessage = "Please write something in your love letter!";
            return Page();
        }

        if (string.IsNullOrWhiteSpace(RecipientName))
        {
            RecipientName = "My Love";
        }

        try
        {
            var blockedPatterns = new[] { 
                "System.IO", "System.Diagnostics",   
                "File.Read", "File.Write", "File.Open",
                "ReadAllText", "ReadAllBytes", "ReadAllLines",
                "WriteAllText", "WriteAllBytes",
                "StreamReader", "StreamWriter",
                "Process.Start", "ProcessStartInfo",
                "string.Concat", "String.Concat", 
                "string.Join", "String.Join",
                "StringBuilder",  
                "Assembly.GetType", ".GetType(\"",
                "/tmp/flag", "flag_", "/flag",
                "WebClient", "HttpClient", "Environment"
            };
            
            foreach (var pattern in blockedPatterns)
            {
                if (Template.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    ErrorMessage = "💔 Cupid detected forbidden magic! Love letters must be pure...";
                    return Page();
                }
            }
            
            if (System.Text.RegularExpressions.Regex.IsMatch(Template, @"typeof\s*\(\s*\w+\s*\)\s*\.\s*Assembly", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                ErrorMessage = "💔 Cupid detected forbidden magic! Love letters must be pure...";
                return Page();
            }
            
            var model = new LoveLetterModel
            {
                Name = RecipientName,
                Date = DateTime.Now.ToString("MMMM dd, yyyy"),
                SessionToken = UserToken!,
                FlagPath = $"/tmp/flag_{UserToken}.txt"
            };

            var templateKey = Guid.NewGuid().ToString();
            RenderedLetter = await _razorEngine.CompileRenderStringAsync(
                templateKey, 
                Template, 
                model
            );

            _logger.LogInformation("Love letter generated for {Recipient}", RecipientName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to render love letter template");
            ErrorMessage = "Oops! Cupid's magic failed. Please check your template syntax. 💔";
        }

        return Page();
    }
    
    private void EnsureUserToken()
    {
        UserToken = HttpContext.Session.GetString(UserTokenKey);
        
        if (string.IsNullOrEmpty(UserToken))
        {
            UserToken = Guid.NewGuid().ToString("N");
            HttpContext.Session.SetString(UserTokenKey, UserToken);
            _logger.LogInformation("New user token generated: {Token}", UserToken);
        }
        
        var uniqueFlag = _flagService.GenerateFlag(UserToken);
        var userFlagPath = $"/tmp/flag_{UserToken}.txt";
        
        try
        {
            System.IO.File.WriteAllText(userFlagPath, uniqueFlag);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write flag file");
        }
    }
}

public class LoveLetterModel
{
    public string Name { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public string FlagPath { get; set; } = string.Empty;
}

