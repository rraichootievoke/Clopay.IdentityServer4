using System.ComponentModel.DataAnnotations;
using Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity.Base;
using Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity.Interfaces;

namespace Clopay.IdentityServer4.Admin.BusinessLogic.Identity.Dtos.Identity
{
    public class RoleDto<TKey> : BaseRoleDto<TKey>, IRoleDto
    {      
        [Required]
        public string Name { get; set; }
    }
}