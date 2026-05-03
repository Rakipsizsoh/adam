using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MyIRC.Web.Models;

namespace MyIRC.Web.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [Route("sohbet")]
        public IActionResult Sohbet(string nick, string? password)
        {
            if (string.IsNullOrWhiteSpace(nick))
            {
                return RedirectToAction("Index");
            }

            ViewBag.Nick = nick;
            ViewBag.Password = password;

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
            });
        }
    }
}