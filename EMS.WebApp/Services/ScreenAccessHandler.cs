using EMS.WebApp.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace EMS.WebApp.Services
{
    public class ScreenAccessHandler : AuthorizationHandler<ScreenAccessRequirement>
    {
        private readonly IScreenAccessRepository _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ScreenAccessHandler(IScreenAccessRepository repository, IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context, ScreenAccessRequirement requirement)
        {
            var userEmail = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (string.IsNullOrEmpty(userEmail)) return;

            if (await _repository.HasScreenAccessAsync(userEmail, requirement.ScreenName))
            {
                context.Succeed(requirement);
            }
        }
    }
}
