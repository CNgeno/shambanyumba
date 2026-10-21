using Microsoft.AspNetCore.Mvc;
using RealEstateERP.Core.Interfaces;
using RealEstateERP.Core.Models;

namespace RealEstateERP.Web.ViewComponents;

public sealed record BusinessSwitcherModel(UserBusinessSummary Current, IReadOnlyList<UserBusinessSummary> Others, string CurrentLabel);

public sealed class BusinessSwitcherViewComponent(IBusinessService businesses, IAgentScopeService agentScope, ICurrentUserService currentUser) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (currentUser.UserId is not string userId || currentUser.BusinessId is not int currentId)
            return Content(string.Empty);

        var all = await businesses.GetBusinessesForUserAsync(userId);
        var current = all.FirstOrDefault(b => b.BusinessId == currentId);
        if (current is null)
            return Content(string.Empty);

        var scope = await agentScope.GetAsync();
        var label = $"{RealEstateERP.Web.Formatting.EnumDisplay.Display(current.Type)} · {Core.Common.RoleLabels.For(current.Role, current.Type, scope.AgentId is not null)}";
        return View(new BusinessSwitcherModel(current, all.Where(b => b.BusinessId != currentId).ToList(), label));
    }
}
