namespace MakelaarLeaderboard.Models;

public class House
{
    public string? Id { get; set; }
    public int MakelaarId { get; set; }
    public string? Woonplaats { get; set; }
    public bool HasTuin { get; set; }

    public Makelaar? Makelaar { get; set; }
}