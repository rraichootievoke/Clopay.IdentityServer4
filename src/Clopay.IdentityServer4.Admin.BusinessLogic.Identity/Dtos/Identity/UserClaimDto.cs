using System.ComponentModel.DataAnnotations;
using Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity.Base;
using Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity.Interfaces;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity
{
    public class UserClaimDto<TKey> : BaseUserClaimDto<TKey>, IUserClaimDto
    {
        [Required]
        public string ClaimType { get; set; }

        [Required]
        public string ClaimValue { get; set; }
    }
}