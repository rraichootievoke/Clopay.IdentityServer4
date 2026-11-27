using System.ComponentModel.DataAnnotations;
using Clopay.IdentityServer4.Shared.Configuration.Configuration.Identity;

namespace Clopay.IdentityServer4.STS.Identity.ViewModels.Account
{
    public class ForgotPasswordViewModel
    {
        [Required]
        public LoginResolutionPolicy? Policy { get; set; }
        
        [EmailAddress]
        public string Email { get; set; }

        public string Username { get; set; }
    }
}
