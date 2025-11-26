using System.Diagnostics;
using MakelaarLeaderboard.Data;
using MakelaarLeaderboard.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakelaarLeaderboard.Controllers;

public class LeaderboardController(MakelaarLeaderboardContext context) : Controller
{
    // GET /Leaderboard/
    public async Task<IActionResult> Index()
    {
        var joinedQuery =
            from house in context.Houses
            join makelaar in context.Makelaars on house.MakelaarId equals makelaar.MakelaarId
            select new { house, makelaar };

        var result = await joinedQuery.GroupBy(e => new
        {
            e.makelaar.MakelaarId,
            e.makelaar.MakelaarNaam
        }).Select(g => new LeaderboardViewModel
        {
            MakelaarId = g.Key.MakelaarId,
            MakelaarNaam = g.Key.MakelaarNaam,
            HousesCount = g.Count(),
        }).OrderByDescending(x => x.HousesCount).Take(10).ToListAsync();

        return View(result);
    }

    // GET /Leaderboard/HasTuin/
    public async Task<IActionResult> HasTuin()
    {
        var joinedQuery =
            from house in context.Houses
            join makelaar in context.Makelaars on house.MakelaarId equals makelaar.MakelaarId
            select new { house, makelaar };

        var result = await joinedQuery.
            Where(w => w.house.HasTuin).
            GroupBy(e => new
            {
                e.makelaar.MakelaarId,
                e.makelaar.MakelaarNaam
            }).Select(g => new LeaderboardViewModel
            {
                MakelaarId = g.Key.MakelaarId,
                MakelaarNaam = g.Key.MakelaarNaam,
                HousesCount = g.Count(),
            }).OrderByDescending(x => x.HousesCount).Take(10).ToListAsync();

        return View(result);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}