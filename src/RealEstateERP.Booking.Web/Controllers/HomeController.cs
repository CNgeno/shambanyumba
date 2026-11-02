using Microsoft.AspNetCore.Mvc;

namespace RealEstateERP.Booking.Web.Controllers;

/// <summary>Error page. (The guest site's home is GuestController.Home.)</summary>
public class HomeController : Controller
{

    [Route("/error")]
    public IActionResult Error() => View();
}
