using Microsoft.AspNetCore.Mvc;

namespace maps4.Models
{
    public class ErrorController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
