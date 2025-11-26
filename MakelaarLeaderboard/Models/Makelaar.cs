using System.ComponentModel.DataAnnotations;
namespace MakelaarLeaderboard.Models;

public class Makelaar
{
    public int MakelaarId { get; set; }
    public string? MakelaarNaam { get; set; }

    public ICollection<House>? Houses { get; set; }
}