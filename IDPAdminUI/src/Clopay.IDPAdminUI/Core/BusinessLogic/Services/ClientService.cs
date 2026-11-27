using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using IdentityServer4.Models;
using Clopay.IDPAdminUI.Core.Auditing;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Configuration;
using Clopay.IDPAdminUI.Core.BusinessLogic.Dtos.Enums;
using Clopay.IDPAdminUI.Core.BusinessLogic.Events.Client;
using Clopay.IDPAdminUI.Core.BusinessLogic.Helpers;
using Clopay.IDPAdminUI.Core.BusinessLogic.Mappers;
using Clopay.IDPAdminUI.Core.BusinessLogic.Resources;
using Clopay.IDPAdminUI.Core.BusinessLogic.Services.Interfaces;
using Clopay.IDPAdminUI.Core.BusinessLogic.Shared.Dtos.Common;
using Clopay.IDPAdminUI.Core.BusinessLogic.Shared.ExceptionHandling;
using Clopay.IDPAdminUI.Infrastructure.Database.Helpers;
using Clopay.IDPAdminUI.Infrastructure.Database.Repositories.Interfaces;

namespace Clopay.IDPAdminUI.Core.BusinessLogic.Services
{
    public class ClientService : IClientService
    {
        protected readonly IClientRepository ClientRepository;
        protected readonly IClientServiceResources ClientServiceResources;
        protected readonly IAuditEventLogger AuditEventLogger;
        private const string SharedSecret = "SharedSecret";

        public ClientService(IClientRepository clientRepository, IClientServiceResources clientServiceResources, IAuditEventLogger auditEventLogger)
        {
            ClientRepository = clientRepository;
            ClientServiceResources = clientServiceResources;
            AuditEventLogger = auditEventLogger;
        }

        private void HashClientSharedSecret(ClientSecretsDto clientSecret)
        {
            if (clientSecret.Type != SharedSecret) return;

            if (clientSecret.HashTypeEnum == HashType.Sha256)
            {
                clientSecret.Value = clientSecret.Value.Sha256();
            }
            else if (clientSecret.HashTypeEnum == HashType.Sha512)
            {
                clientSecret.Value = clientSecret.Value.Sha512();
            }
        }

        private void PrepareClientTypeForNewClient(ClientDto client)
        {
            switch (client.ClientType)
            {
                case ClientType.Empty:
                    break;
                case ClientType.Web:
                    client.AllowedGrantTypes.AddRange(GrantTypes.Code);
                    client.RequirePkce = true;
                    client.RequireClientSecret = true;
                    break;
                case ClientType.Spa:
                    client.AllowedGrantTypes.AddRange(GrantTypes.Code);
                    client.RequirePkce = true;
                    client.RequireClientSecret = false;
                    break;
                case ClientType.Native:
                    client.AllowedGrantTypes.AddRange(GrantTypes.Code);
                    client.RequirePkce = true;
                    client.RequireClientSecret = false;
                    break;
                case ClientType.Machine:
                    client.AllowedGrantTypes.AddRange(GrantTypes.ClientCredentials);
                    break;
                case ClientType.Device:
                    client.AllowedGrantTypes.AddRange(GrantTypes.DeviceFlow);
                    client.RequireClientSecret = false;
                    client.AllowOfflineAccess = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void PopulateClientRelations(ClientDto client)
        {
            ComboBoxHelpers.PopulateValuesToList(client.AllowedScopesItems, client.AllowedScopes);
            ComboBoxHelpers.PopulateValuesToList(client.PostLogoutRedirectUrisItems, client.PostLogoutRedirectUris);
            ComboBoxHelpers.PopulateValuesToList(client.IdentityProviderRestrictionsItems, client.IdentityProviderRestrictions);
            ComboBoxHelpers.PopulateValuesToList(client.RedirectUrisItems, client.RedirectUris);
            ComboBoxHelpers.PopulateValuesToList(client.AllowedCorsOriginsItems, client.AllowedCorsOrigins);
            ComboBoxHelpers.PopulateValuesToList(client.AllowedGrantTypesItems, client.AllowedGrantTypes);
            ComboBoxHelpers.PopulateValuesToList(client.AllowedIdentityTokenSigningAlgorithmsItems, client.AllowedIdentityTokenSigningAlgorithms);
        }

        public virtual ClientCloneDto BuildClientCloneViewModel(int id, ClientDto clientDto)
        {
            var client = new ClientCloneDto
            {
                CloneClientCorsOrigins = true,
                CloneClientGrantTypes = true,
                CloneClientIdPRestrictions = true,
                CloneClientPostLogoutRedirectUris = true,
                CloneClientRedirectUris = true,
                CloneClientScopes = true,
                CloneClientClaims = true,
                CloneClientProperties = true,
                ClientIdOriginal = clientDto.ClientId,
                ClientNameOriginal = clientDto.ClientName,
                Id = id
            };

            return client;
        }

        public virtual ClientSecretsDto BuildClientSecretsViewModel(ClientSecretsDto clientSecrets)
        {
            clientSecrets.HashTypes = GetHashTypes();
            clientSecrets.TypeList = GetSecretTypes();

            return clientSecrets;
        }

        public virtual ClientDto BuildClientViewModel(ClientDto client = null)
        {
            if (client == null)
            {
                var clientDto = new ClientDto
                {
                    AccessTokenTypes = GetAccessTokenTypes(),
                    RefreshTokenExpirations = GetTokenExpirations(),
                    RefreshTokenUsages = GetTokenUsage(),
                    ProtocolTypes = GetProtocolTypes(),
                    Id = 0
                };

                return clientDto;
            }

            client.AccessTokenTypes = GetAccessTokenTypes();
            client.RefreshTokenExpirations = GetTokenExpirations();
            client.RefreshTokenUsages = GetTokenUsage();
            client.ProtocolTypes = GetProtocolTypes();

            PopulateClientRelations(client);

            return client;
        }

        /// <summary>
        /// Add new client, this method doesn't save client secrets, client claims, client properties
        /// </summary>
        /// <param name="client"></param>
        /// <returns>This method return new client id</returns>
        public virtual async Task<int> AddClientAsync(ClientDto client)
        {
            var canInsert = await CanInsertClientAsync(client);
            if (!canInsert)
            {
                throw new UserFriendlyViewException(string.Format(ClientServiceResources.ClientExistsValue().Description, client.ClientId), ClientServiceResources.ClientExistsKey().Description, client);
            }

            PrepareClientTypeForNewClient(client);
            var clientEntity = client.ToEntity();

            var added = await ClientRepository.AddClientAsync(clientEntity);

            await AuditEventLogger.LogEventAsync(new ClientAddedEvent(client));

            return added;
        }

        public virtual async Task<int> UpdateClientAsync(ClientDto client, bool updateClientClaims = false, bool updateClientProperties = false)
        {
            var canInsert = await CanInsertClientAsync(client);
            if (!canInsert)
            {
                throw new UserFriendlyViewException(string.Format(ClientServiceResources.ClientExistsValue().Description, client.ClientId), ClientServiceResources.ClientExistsKey().Description, client);
            }

            var clientEntity = client.ToEntity();

            var originalClient = await GetClientAsync(client.Id);

            var updated = await ClientRepository.UpdateClientAsync(clientEntity, updateClientClaims, updateClientProperties);

            await AuditEventLogger.LogEventAsync(new ClientUpdatedEvent(originalClient, client));

            return updated;
        }

        public virtual async Task<int> RemoveClientAsync(ClientDto client)
        {
            var clientEntity = client.ToEntity();

            var deleted = await ClientRepository.RemoveClientAsync(clientEntity);

            await AuditEventLogger.LogEventAsync(new ClientDeletedEvent(client));

            return deleted;
        }

        public virtual async Task<int> CloneClientAsync(ClientCloneDto client)
        {
            var canInsert = await CanInsertClientAsync(client, true);
            if (!canInsert)
            {
                //If it failed you need get original clientid, clientname for view title
                var clientInfo = await ClientRepository.GetClientIdAsync(client.Id);
                client.ClientIdOriginal = clientInfo.ClientId;
                client.ClientNameOriginal = clientInfo.ClientName;

                throw new UserFriendlyViewException(string.Format(ClientServiceResources.ClientExistsValue().Description, client.ClientId), ClientServiceResources.ClientExistsKey().Description, client);
            }

            var clientEntity = client.ToEntity();

            var clonedClientId = await ClientRepository.CloneClientAsync(clientEntity, client.CloneClientCorsOrigins,
                client.CloneClientGrantTypes, client.CloneClientIdPRestrictions,
                client.CloneClientPostLogoutRedirectUris,
                client.CloneClientScopes, client.CloneClientRedirectUris, client.CloneClientClaims, client.CloneClientProperties);

            await AuditEventLogger.LogEventAsync(new ClientClonedEvent(client));

            return clonedClientId;
        }

        public virtual Task<bool> CanInsertClientAsync(ClientDto client, bool isCloned = false)
        {
            var clientEntity = client.ToEntity();

            return ClientRepository.CanInsertClientAsync(clientEntity, isCloned);
        }

        public virtual async Task<ClientDto> GetClientAsync(int clientId)
        {
            var client = await ClientRepository.GetClientAsync(clientId);

            if (client == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientId));

            var clientDto = client.ToModel();

            await AuditEventLogger.LogEventAsync(new ClientRequestedEvent(clientDto));

            return clientDto;
        }

        public virtual async Task<ClientsDto> GetClientsAsync(string search, int page = 1, int pageSize = 10)
        {
            var pagedList = await ClientRepository.GetClientsAsync(search, page, pageSize);
            var clientsDto = pagedList.ToModel();

            await AuditEventLogger.LogEventAsync(new ClientsRequestedEvent(clientsDto));

            return clientsDto;
        }

        public virtual async Task<List<string>> GetScopesAsync(string scope, int limit = 0)
        {
            var scopes = await ClientRepository.GetScopesAsync(scope, limit);

            return scopes;
        }

        public virtual List<string> GetGrantTypes(string grant, int limit = 0)
        {
            var grantTypes = ClientRepository.GetGrantTypes(grant, limit);

            return grantTypes;
        }

        public virtual List<SelectItemDto> GetAccessTokenTypes()
        {
            var accessTokenTypes = ClientRepository.GetAccessTokenTypes().ToModel();

            return accessTokenTypes;
        }

        public virtual List<string> GetSigningAlgorithms(string algorithm, int limit = 0)
        {
            var signingAlgorithms = ClientRepository.GetSigningAlgorithms(algorithm, limit);

            return signingAlgorithms;
        }

        public virtual List<SelectItemDto> GetTokenExpirations()
        {
            var tokenExpirations = ClientRepository.GetTokenExpirations().ToModel();

            return tokenExpirations;
        }

        public virtual List<SelectItemDto> GetTokenUsage()
        {
            var tokenUsage = ClientRepository.GetTokenUsage().ToModel();

            return tokenUsage;
        }

        public virtual List<SelectItemDto> GetHashTypes()
        {
            var hashTypes = ClientRepository.GetHashTypes().ToModel();

            return hashTypes;
        }

        public virtual List<SelectItemDto> GetSecretTypes()
        {
            var secretTypes = ClientRepository.GetSecretTypes().ToModel();

            return secretTypes;
        }

        public virtual List<SelectItemDto> GetProtocolTypes()
        {
            var protocolTypes = ClientRepository.GetProtocolTypes().ToModel();

            return protocolTypes;
        }

        public virtual List<string> GetStandardClaims(string claim, int limit = 0)
        {
            var standardClaims = ClientRepository.GetStandardClaims(claim, limit);

            return standardClaims;
        }

        public virtual async Task<int> AddClientSecretAsync(ClientSecretsDto clientSecret)
        {
            HashClientSharedSecret(clientSecret);

            var clientSecretEntity = clientSecret.ToEntity();
            var added = await ClientRepository.AddClientSecretAsync(clientSecret.ClientId, clientSecretEntity);

            await AuditEventLogger.LogEventAsync(new ClientSecretAddedEvent(clientSecret.ClientId, clientSecret.Type, clientSecret.Expiration));

            return added;
        }

        /// <summary>A 256-bit random secret, hex encoded (64 characters).</summary>
        private static string GenerateSecret()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        }

        public virtual async Task<ClientSecretRotationResultDto> RotateClientSecretAsync(int clientId, int gracePeriodDays)
        {
            var clientInfo = await ClientRepository.GetClientIdAsync(clientId);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientId));

            if (gracePeriodDays < 0) gracePeriodDays = 0;

            // Load the current secrets first, so the new one is never retired by mistake
            var existing = (await ClientRepository.GetClientSecretsAsync(clientId, 1, int.MaxValue)).Data.ToList();

            var plainSecret = GenerateSecret();
            var now = DateTime.UtcNow;

            await AddClientSecretAsync(new ClientSecretsDto
            {
                ClientId = clientId,
                ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName),
                Type = SharedSecret,
                HashType = HashType.Sha256.ToString(),
                Value = plainSecret,
                Description = $"Rotated {now:yyyy-MM-dd HH:mm} UTC"
            });

            DateTime? graceEnds = gracePeriodDays > 0 ? now.AddDays(gracePeriodDays) : (DateTime?)null;
            var retired = 0;

            foreach (var old in existing)
            {
                var oldId = old.Id;

                if (graceEnds == null)
                {
                    await ClientRepository.DeleteClientSecretAsync(old);
                    await AuditEventLogger.LogEventAsync(new ClientSecretDeletedEvent(clientId, oldId));
                    retired++;
                }
                else if (!old.Expiration.HasValue || old.Expiration.Value > graceEnds.Value)
                {
                    await ClientRepository.UpdateClientSecretExpirationAsync(oldId, graceEnds);
                    retired++;
                }
            }

            await AuditEventLogger.LogEventAsync(new ClientSecretRotatedEvent(clientId, retired, gracePeriodDays, graceEnds));

            return new ClientSecretRotationResultDto
            {
                ClientId = clientId,
                ClientIdentifier = clientInfo.ClientId,
                ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName),
                NewSecret = plainSecret,
                RetiredSecrets = retired,
                GracePeriodDays = gracePeriodDays,
                GracePeriodEndsUtc = graceEnds
            };
        }

        private static List<string> SplitLines(string value)
        {
            return (value ?? string.Empty)
                .Split(new[] { '\r', '\n', ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim())
                .Where(x => x.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public virtual async Task<ClientQuickCreateResultDto> QuickCreateClientAsync(ClientQuickCreateDto model)
        {
            var client = new ClientDto
            {
                ClientType = ClientType.Empty,
                ClientId = model.ClientId.Trim(),
                ClientName = string.IsNullOrWhiteSpace(model.ClientName) ? model.ClientId.Trim() : model.ClientName.Trim(),
                Description = model.Description,
                RequireConsent = false,
                RequireClientSecret = true,
                RequirePkce = false,
                // Clopay template (idpDBChanges/_templates/App-PKCE-Client.sql): user claims in the ID token, refresh tokens allowed
                AlwaysIncludeUserClaimsInIdToken = true,
                AllowOfflineAccess = model.Template != QuickClientTemplate.Machine
            };

            switch (model.Template)
            {
                case QuickClientTemplate.WebApp:
                    client.AllowedGrantTypes.Add("authorization_code");
                    client.RequirePkce = true;
                    break;
                case QuickClientTemplate.Spa:
                    client.AllowedGrantTypes.Add("authorization_code");
                    client.RequirePkce = true;
                    client.RequireClientSecret = false;
                    break;
                case QuickClientTemplate.Machine:
                    client.AllowedGrantTypes.Add("client_credentials");
                    break;
                case QuickClientTemplate.LegacyPassword:
                    client.AllowedGrantTypes.Add("password");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(model.Template));
            }

            var usesBrowser = model.Template == QuickClientTemplate.WebApp || model.Template == QuickClientTemplate.Spa;
            if (usesBrowser)
            {
                client.RedirectUris.AddRange(SplitLines(model.RedirectUris));
                client.PostLogoutRedirectUris.AddRange(SplitLines(model.PostLogoutRedirectUris));
            }

            if (model.Template != QuickClientTemplate.Machine)
            {
                client.AllowedCorsOrigins.AddRange(SplitLines(model.AllowedCorsOrigins));
            }

            var scopes = (model.SelectedScopes ?? new List<string>()).Concat(SplitLines(model.ExtraScopes))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            client.AllowedScopes.AddRange(scopes);

            var id = await AddClientAsync(client);

            string secret = null;
            if (client.RequireClientSecret)
            {
                secret = (await RotateClientSecretAsync(id, 0)).NewSecret;
            }

            return new ClientQuickCreateResultDto
            {
                Id = id,
                ClientId = client.ClientId,
                ClientName = client.ClientName,
                Secret = secret,
                Template = model.Template
            };
        }

        public virtual async Task<int> DeleteClientSecretAsync(ClientSecretsDto clientSecret)
        {
            var clientSecretEntity = clientSecret.ToEntity();

            var deleted = await ClientRepository.DeleteClientSecretAsync(clientSecretEntity);

            await AuditEventLogger.LogEventAsync(new ClientSecretDeletedEvent(clientSecret.ClientId, clientSecret.ClientSecretId));

            return deleted;
        }

        public virtual async Task<ClientSecretsDto> GetClientSecretsAsync(int clientId, int page = 1, int pageSize = 10)
        {
            var clientInfo = await ClientRepository.GetClientIdAsync(clientId);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientId));

            var pagedList = await ClientRepository.GetClientSecretsAsync(clientId, page, pageSize);
            var clientSecretsDto = pagedList.ToModel();
            clientSecretsDto.ClientId = clientId;
            clientSecretsDto.ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName);

            // remove secret value from dto
            clientSecretsDto.ClientSecrets.ForEach(x => x.Value = null);

            await AuditEventLogger.LogEventAsync(new ClientSecretsRequestedEvent(clientSecretsDto.ClientId, clientSecretsDto.ClientSecrets.Select(x => (x.Id, x.Type, x.Expiration)).ToList()));

            return clientSecretsDto;
        }

        public virtual async Task<ClientSecretsDto> GetClientSecretAsync(int clientSecretId)
        {
            var clientSecret = await ClientRepository.GetClientSecretAsync(clientSecretId);
            if (clientSecret == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientSecretDoesNotExist().Description, clientSecretId));

            var clientInfo = await ClientRepository.GetClientIdAsync(clientSecret.Client.Id);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientSecret.Client.Id));

            var clientSecretsDto = clientSecret.ToModel();
            clientSecretsDto.ClientId = clientSecret.Client.Id;
            clientSecretsDto.ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName);

            // remove secret value for dto
            clientSecretsDto.Value = null;

            await AuditEventLogger.LogEventAsync(new ClientSecretRequestedEvent(clientSecretsDto.ClientId, clientSecretsDto.ClientSecretId, clientSecretsDto.Type, clientSecretsDto.Expiration));

            return clientSecretsDto;
        }

        public virtual async Task<ClientClaimsDto> GetClientClaimsAsync(int clientId, int page = 1, int pageSize = 10)
        {
            var clientInfo = await ClientRepository.GetClientIdAsync(clientId);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientId));

            var pagedList = await ClientRepository.GetClientClaimsAsync(clientId, page, pageSize);
            var clientClaimsDto = pagedList.ToModel();
            clientClaimsDto.ClientId = clientId;
            clientClaimsDto.ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName);

            await AuditEventLogger.LogEventAsync(new ClientClaimsRequestedEvent(clientClaimsDto));

            return clientClaimsDto;
        }

        public virtual async Task<ClientPropertiesDto> GetClientPropertiesAsync(int clientId, int page = 1, int pageSize = 10)
        {
            var clientInfo = await ClientRepository.GetClientIdAsync(clientId);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientId));

            var pagedList = await ClientRepository.GetClientPropertiesAsync(clientId, page, pageSize);
            var clientPropertiesDto = pagedList.ToModel();
            clientPropertiesDto.ClientId = clientId;
            clientPropertiesDto.ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName);

            await AuditEventLogger.LogEventAsync(new ClientPropertiesRequestedEvent(clientPropertiesDto));

            return clientPropertiesDto;
        }

        public virtual async Task<ClientClaimsDto> GetClientClaimAsync(int clientClaimId)
        {
            var clientClaim = await ClientRepository.GetClientClaimAsync(clientClaimId);
            if (clientClaim == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientClaimDoesNotExist().Description, clientClaimId));

            var clientInfo = await ClientRepository.GetClientIdAsync(clientClaim.Client.Id);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientClaim.Client.Id));

            var clientClaimsDto = clientClaim.ToModel();
            clientClaimsDto.ClientId = clientClaim.Client.Id;
            clientClaimsDto.ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName);

            await AuditEventLogger.LogEventAsync(new ClientClaimRequestedEvent(clientClaimsDto));

            return clientClaimsDto;
        }

        public virtual async Task<ClientPropertiesDto> GetClientPropertyAsync(int clientPropertyId)
        {
            var clientProperty = await ClientRepository.GetClientPropertyAsync(clientPropertyId);
            if (clientProperty == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientPropertyDoesNotExist().Description, clientPropertyId));

            var clientInfo = await ClientRepository.GetClientIdAsync(clientProperty.Client.Id);
            if (clientInfo.ClientId == null) throw new UserFriendlyErrorPageException(string.Format(ClientServiceResources.ClientDoesNotExist().Description, clientProperty.Client.Id));

            var clientPropertiesDto = clientProperty.ToModel();
            clientPropertiesDto.ClientId = clientProperty.Client.Id;
            clientPropertiesDto.ClientName = ViewHelpers.GetClientName(clientInfo.ClientId, clientInfo.ClientName);

            await AuditEventLogger.LogEventAsync(new ClientPropertyRequestedEvent(clientPropertiesDto));

            return clientPropertiesDto;
        }

        public virtual async Task<int> AddClientClaimAsync(ClientClaimsDto clientClaim)
        {
            var clientClaimEntity = clientClaim.ToEntity();

            var saved = await ClientRepository.AddClientClaimAsync(clientClaim.ClientId, clientClaimEntity);

            await AuditEventLogger.LogEventAsync(new ClientClaimAddedEvent(clientClaim));

            return saved;
        }

        public virtual async Task<int> AddClientPropertyAsync(ClientPropertiesDto clientProperties)
        {
            var clientProperty = clientProperties.ToEntity();

            var saved = await ClientRepository.AddClientPropertyAsync(clientProperties.ClientId, clientProperty);

            await AuditEventLogger.LogEventAsync(new ClientPropertyAddedEvent(clientProperties));

            return saved;
        }

        public virtual async Task<int> DeleteClientClaimAsync(ClientClaimsDto clientClaim)
        {
            var clientClaimEntity = clientClaim.ToEntity();

            var deleted = await ClientRepository.DeleteClientClaimAsync(clientClaimEntity);

            await AuditEventLogger.LogEventAsync(new ClientClaimDeletedEvent(clientClaim));

            return deleted;
        }

        public virtual async Task<int> DeleteClientPropertyAsync(ClientPropertiesDto clientProperty)
        {
            var clientPropertyEntity = clientProperty.ToEntity();

            var deleted = await ClientRepository.DeleteClientPropertyAsync(clientPropertyEntity);

            await AuditEventLogger.LogEventAsync(new ClientPropertyDeletedEvent(clientProperty));

            return deleted;
        }
    }
}