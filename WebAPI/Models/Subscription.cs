namespace ZohoPowerBIConnector.WebAPI.Models;

public class Subscription
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PlanId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime ExpiryDate { get; set; }
    public bool IsActive { get; set; } = true;
    public User? User { get; set; }
    public Plan? Plan { get; set; }
}
