using Microsoft.AspNetCore.Mvc;

namespace MakelaarLeaderboard.Controllers;

public class LeaderboardController : Controller
{
    // GET /Leaderboard/
    public IActionResult Index()
    {
        return View();
    }

    // GET /Leaderboard/Welcome/
    public string Welcome()
    {
        return "Welcome Action";
    }


}