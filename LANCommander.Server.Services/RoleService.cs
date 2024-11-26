﻿using LANCommander.Server.Data;
using LANCommander.Server.Data.Models;
using LANCommander.Server.Services.Factories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Data;
using ZiggyCreatures.Caching.Fusion;

namespace LANCommander.Server.Services
{
    public class RoleService : BaseDatabaseService<Role>
    {
        public const string AdministratorRoleName = "Administrator";

        private readonly IdentityContextFactory IdentityContextFactory;

        public RoleService(
            ILogger<RoleService> logger,
            IFusionCache cache,
            RepositoryFactory repositoryFactory,
            IdentityContextFactory identityContextFactory) : base(logger, cache, repositoryFactory)
        {
            IdentityContextFactory = identityContextFactory;
        }

        public override async Task<Role> AddAsync(Role role)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                var result = await identityContext.RoleManager.CreateAsync(role);

                if (result.Succeeded)
                    return await identityContext.RoleManager.FindByNameAsync(role.Name);
                else
                    return null;
            }
        }

        public async Task<Role> GetAsync(string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.RoleManager.FindByNameAsync(roleName);
            }
        }

        public async Task<IEnumerable<User>> GetUsersAsync(string roleName)
        {
            using (var identityContext = IdentityContextFactory.Create())
            {
                return await identityContext.UserManager.GetUsersInRoleAsync(roleName);
            }
        }
    }
}
